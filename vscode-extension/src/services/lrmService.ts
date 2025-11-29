import * as vscode from 'vscode';
import { spawn, exec } from 'child_process';
import { promisify } from 'util';
import * as path from 'path';

const execAsync = promisify(exec);

export interface ResourceFile {
    path: string;
    name: string;
    language: string;
    isBase: boolean;
    keyCount: number;
}

export interface ResourceKey {
    name: string;
    value: string;
    comment?: string;
    translations: Map<string, string>;
}

export interface ResourceGroup {
    baseName: string;
    basePath: string;
    files: ResourceFile[];
    keys: ResourceKey[];
}

export interface ValidationIssue {
    type: 'missing' | 'duplicate' | 'empty' | 'placeholder' | 'unused';
    key: string;
    file: string;
    language?: string;
    message: string;
    severity: 'error' | 'warning' | 'info';
}

export interface TranslationStats {
    totalKeys: number;
    translatedKeys: number;
    missingKeys: number;
    percentage: number;
    byLanguage: Map<string, { translated: number; missing: number; percentage: number }>;
}

export interface ScanResult {
    unusedKeys: Array<{ key: string; file: string }>;
    missingKeys: Array<{ key: string; references: string[] }>;
}

export class LrmService {
    private outputChannel: vscode.OutputChannel;
    private lrmPath: string;
    private useWebApi: boolean;
    private webServerPort: number;

    constructor() {
        this.outputChannel = vscode.window.createOutputChannel('LRM');
        this.updateConfiguration();
    }

    public updateConfiguration(): void {
        const config = vscode.workspace.getConfiguration('lrm');
        this.lrmPath = config.get<string>('lrmPath') || 'lrm';
        this.useWebApi = config.get<boolean>('useWebApi') || false;
        this.webServerPort = config.get<number>('webServerPort') || 5000;
    }

    public async checkAvailability(): Promise<boolean> {
        try {
            const result = await this.runCommand(['--version']);
            return result.success;
        } catch {
            return false;
        }
    }

    public async getVersion(): Promise<string | null> {
        try {
            const result = await this.runCommand(['--version']);
            if (result.success) {
                return result.stdout.trim();
            }
            return null;
        } catch {
            return null;
        }
    }

    public async discoverResources(workspacePath: string): Promise<ResourceGroup[]> {
        const groups: ResourceGroup[] = [];

        try {
            const result = await this.runCommand(['list', '-p', workspacePath, '-f', 'json']);

            if (result.success && result.stdout) {
                const data = JSON.parse(result.stdout);
                // Parse the JSON output from LRM and build resource groups
                if (Array.isArray(data)) {
                    for (const item of data) {
                        groups.push(this.parseResourceGroup(item));
                    }
                }
            }
        } catch (error) {
            this.log(`Error discovering resources: ${error}`);
            // Fall back to file system scanning
            return this.discoverResourcesFromFileSystem(workspacePath);
        }

        return groups;
    }

    private async discoverResourcesFromFileSystem(workspacePath: string): Promise<ResourceGroup[]> {
        const groups: ResourceGroup[] = [];
        const config = vscode.workspace.getConfiguration('lrm');
        const excludePatterns = config.get<string[]>('excludePatterns') || [];

        // Find all .resx files
        const pattern = new vscode.RelativePattern(workspacePath, '**/*.resx');
        const files = await vscode.workspace.findFiles(pattern, `{${excludePatterns.join(',')}}`);

        // Group files by base name
        const groupMap = new Map<string, ResourceFile[]>();

        for (const file of files) {
            const fileName = path.basename(file.fsPath);
            const dirPath = path.dirname(file.fsPath);

            // Parse filename to extract base name and language
            // Pattern: BaseName.lang.resx or BaseName.resx
            const match = fileName.match(/^(.+?)(?:\.([a-z]{2}(?:-[A-Z]{2})?))?\.resx$/i);

            if (match) {
                const baseName = match[1];
                const language = match[2] || '';
                const basePath = path.join(dirPath, `${baseName}.resx`);

                if (!groupMap.has(basePath)) {
                    groupMap.set(basePath, []);
                }

                const resourceFile: ResourceFile = {
                    path: file.fsPath,
                    name: fileName,
                    language: language || 'default',
                    isBase: !language,
                    keyCount: 0 // Will be populated later
                };

                groupMap.get(basePath)!.push(resourceFile);
            }
        }

        // Convert map to array
        for (const [basePath, resourceFiles] of groupMap) {
            const baseName = path.basename(basePath, '.resx');
            groups.push({
                baseName,
                basePath,
                files: resourceFiles.sort((a, b) => {
                    if (a.isBase) { return -1; }
                    if (b.isBase) { return 1; }
                    return a.language.localeCompare(b.language);
                }),
                keys: []
            });
        }

        return groups;
    }

    private parseResourceGroup(data: Record<string, unknown>): ResourceGroup {
        const files: ResourceFile[] = [];
        const dataFiles = data.files as Array<Record<string, unknown>> || [];

        for (const file of dataFiles) {
            files.push({
                path: file.path as string,
                name: file.name as string,
                language: file.language as string,
                isBase: file.isBase as boolean,
                keyCount: file.keyCount as number || 0
            });
        }

        return {
            baseName: data.baseName as string,
            basePath: data.basePath as string,
            files,
            keys: []
        };
    }

    public async getKeys(resourcePath: string): Promise<ResourceKey[]> {
        const keys: ResourceKey[] = [];

        try {
            const result = await this.runCommand(['keys', '-p', resourcePath, '-f', 'json']);

            if (result.success && result.stdout) {
                const data = JSON.parse(result.stdout);
                if (Array.isArray(data)) {
                    for (const item of data) {
                        keys.push({
                            name: item.name || item.key,
                            value: item.value || '',
                            comment: item.comment,
                            translations: new Map(Object.entries(item.translations || {}))
                        });
                    }
                }
            }
        } catch (error) {
            this.log(`Error getting keys: ${error}`);
        }

        return keys;
    }

    public async validate(resourcePath: string): Promise<ValidationIssue[]> {
        const issues: ValidationIssue[] = [];

        try {
            const result = await this.runCommand(['validate', '-p', resourcePath, '-f', 'json']);

            if (result.stdout) {
                const data = JSON.parse(result.stdout);
                if (Array.isArray(data)) {
                    for (const item of data) {
                        issues.push({
                            type: item.type || 'missing',
                            key: item.key,
                            file: item.file,
                            language: item.language,
                            message: item.message,
                            severity: this.mapSeverity(item.severity || item.type)
                        });
                    }
                }
            }
        } catch (error) {
            this.log(`Error validating: ${error}`);
        }

        return issues;
    }

    private mapSeverity(type: string): 'error' | 'warning' | 'info' {
        switch (type) {
            case 'missing':
            case 'error':
                return 'error';
            case 'duplicate':
            case 'placeholder':
            case 'warning':
                return 'warning';
            default:
                return 'info';
        }
    }

    public async translate(
        resourcePath: string,
        targetLanguage: string,
        options: {
            provider?: string;
            onlyMissing?: boolean;
            keyPattern?: string;
        } = {}
    ): Promise<{ success: boolean; translatedCount: number; errors: string[] }> {
        const args = ['translate', '-p', resourcePath, '-t', targetLanguage];

        if (options.provider) {
            args.push('--provider', options.provider);
        }
        if (options.onlyMissing) {
            args.push('--only-missing');
        }
        if (options.keyPattern) {
            args.push('--pattern', options.keyPattern);
        }

        args.push('-f', 'json');

        try {
            const result = await this.runCommand(args);

            if (result.success && result.stdout) {
                const data = JSON.parse(result.stdout);
                return {
                    success: true,
                    translatedCount: data.translatedCount || 0,
                    errors: data.errors || []
                };
            }

            return {
                success: false,
                translatedCount: 0,
                errors: [result.stderr || 'Unknown error']
            };
        } catch (error) {
            return {
                success: false,
                translatedCount: 0,
                errors: [String(error)]
            };
        }
    }

    public async scan(workspacePath: string): Promise<ScanResult> {
        try {
            const result = await this.runCommand(['scan', '-p', workspacePath, '-f', 'json']);

            if (result.success && result.stdout) {
                const data = JSON.parse(result.stdout);
                return {
                    unusedKeys: data.unusedKeys || [],
                    missingKeys: data.missingKeys || []
                };
            }
        } catch (error) {
            this.log(`Error scanning: ${error}`);
        }

        return { unusedKeys: [], missingKeys: [] };
    }

    public async addKey(
        resourcePath: string,
        key: string,
        value: string,
        comment?: string
    ): Promise<boolean> {
        const args = ['add', '-p', resourcePath, '-k', key, '-v', value];

        if (comment) {
            args.push('-c', comment);
        }

        const result = await this.runCommand(args);
        return result.success;
    }

    public async updateKey(
        resourcePath: string,
        key: string,
        value: string,
        language?: string
    ): Promise<boolean> {
        const args = ['update', '-p', resourcePath, '-k', key, '-v', value];

        if (language) {
            args.push('-l', language);
        }

        const result = await this.runCommand(args);
        return result.success;
    }

    public async deleteKey(resourcePath: string, key: string): Promise<boolean> {
        const result = await this.runCommand(['delete', '-p', resourcePath, '-k', key]);
        return result.success;
    }

    public async exportToCsv(resourcePath: string, outputPath: string): Promise<boolean> {
        const result = await this.runCommand(['export', '-p', resourcePath, '-o', outputPath, '--format', 'csv']);
        return result.success;
    }

    public async exportToJson(resourcePath: string, outputPath: string): Promise<boolean> {
        const result = await this.runCommand(['export', '-p', resourcePath, '-o', outputPath, '--format', 'json']);
        return result.success;
    }

    public async importFromCsv(resourcePath: string, inputPath: string): Promise<boolean> {
        const result = await this.runCommand(['import', '-p', resourcePath, '-i', inputPath]);
        return result.success;
    }

    public async getStats(resourcePath: string): Promise<TranslationStats | null> {
        try {
            const result = await this.runCommand(['stats', '-p', resourcePath, '-f', 'json']);

            if (result.success && result.stdout) {
                const data = JSON.parse(result.stdout);
                const byLanguage = new Map<string, { translated: number; missing: number; percentage: number }>();

                if (data.byLanguage) {
                    for (const [lang, stats] of Object.entries(data.byLanguage)) {
                        const langStats = stats as Record<string, number>;
                        byLanguage.set(lang, {
                            translated: langStats.translated || 0,
                            missing: langStats.missing || 0,
                            percentage: langStats.percentage || 0
                        });
                    }
                }

                return {
                    totalKeys: data.totalKeys || 0,
                    translatedKeys: data.translatedKeys || 0,
                    missingKeys: data.missingKeys || 0,
                    percentage: data.percentage || 0,
                    byLanguage
                };
            }
        } catch (error) {
            this.log(`Error getting stats: ${error}`);
        }

        return null;
    }

    public async findReferences(key: string, workspacePath: string): Promise<vscode.Location[]> {
        const locations: vscode.Location[] = [];

        try {
            const result = await this.runCommand(['refs', '-p', workspacePath, '-k', key, '-f', 'json']);

            if (result.success && result.stdout) {
                const data = JSON.parse(result.stdout);
                if (Array.isArray(data)) {
                    for (const ref of data) {
                        const uri = vscode.Uri.file(ref.file);
                        const position = new vscode.Position(ref.line - 1, ref.column - 1);
                        const range = new vscode.Range(position, position);
                        locations.push(new vscode.Location(uri, range));
                    }
                }
            }
        } catch (error) {
            this.log(`Error finding references: ${error}`);
        }

        return locations;
    }

    public async addLanguage(resourcePath: string, languageCode: string): Promise<boolean> {
        const result = await this.runCommand(['add-language', '-p', resourcePath, '-l', languageCode]);
        return result.success;
    }

    public async createBackup(resourcePath: string): Promise<boolean> {
        const result = await this.runCommand(['backup', '-p', resourcePath]);
        return result.success;
    }

    public async listBackups(resourcePath: string): Promise<Array<{ id: string; date: string; description: string }>> {
        try {
            const result = await this.runCommand(['backup', 'list', '-p', resourcePath, '-f', 'json']);

            if (result.success && result.stdout) {
                return JSON.parse(result.stdout);
            }
        } catch (error) {
            this.log(`Error listing backups: ${error}`);
        }

        return [];
    }

    public async restoreBackup(resourcePath: string, backupId: string): Promise<boolean> {
        const result = await this.runCommand(['backup', 'restore', '-p', resourcePath, '-b', backupId]);
        return result.success;
    }

    public async createConfig(workspacePath: string): Promise<boolean> {
        const result = await this.runCommand(['config', 'init', '-p', workspacePath]);
        return result.success;
    }

    private async runCommand(args: string[]): Promise<{ success: boolean; stdout: string; stderr: string }> {
        return new Promise((resolve) => {
            const workspaceFolders = vscode.workspace.workspaceFolders;
            const cwd = workspaceFolders?.[0]?.uri.fsPath || process.cwd();

            this.log(`Running: ${this.lrmPath} ${args.join(' ')}`);

            const process = spawn(this.lrmPath, args, {
                cwd,
                shell: true
            });

            let stdout = '';
            let stderr = '';

            process.stdout.on('data', (data) => {
                stdout += data.toString();
            });

            process.stderr.on('data', (data) => {
                stderr += data.toString();
            });

            process.on('close', (code) => {
                this.log(`Command finished with code ${code}`);
                if (stderr) {
                    this.log(`stderr: ${stderr}`);
                }

                resolve({
                    success: code === 0,
                    stdout,
                    stderr
                });
            });

            process.on('error', (error) => {
                this.log(`Command error: ${error.message}`);
                resolve({
                    success: false,
                    stdout: '',
                    stderr: error.message
                });
            });
        });
    }

    private log(message: string): void {
        this.outputChannel.appendLine(`[${new Date().toISOString()}] ${message}`);
    }

    public showOutput(): void {
        this.outputChannel.show();
    }
}
