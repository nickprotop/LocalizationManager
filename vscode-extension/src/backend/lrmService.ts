import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { spawn, ChildProcess } from 'child_process';
import { createServer } from 'net';
import axios from 'axios';

export interface LrmServiceConfig {
    extensionPath: string;
    resourcePath?: string;
}

export class LrmService implements vscode.Disposable {
    private process: ChildProcess | null = null;
    private port: number = 0;
    private extensionPath: string;
    private resourcePath: string | null = null;
    private healthCheckInterval: NodeJS.Timeout | null = null;
    public outputChannel: vscode.OutputChannel;

    constructor(config: LrmServiceConfig) {
        this.extensionPath = config.extensionPath;
        this.resourcePath = config.resourcePath || null;
        this.outputChannel = vscode.window.createOutputChannel('LRM Backend');
    }

    public getBaseUrl(): string {
        if (this.port === 0) {
            throw new Error('Backend is not running');
        }
        return `http://localhost:${this.port}`;
    }

    public getPort(): number {
        return this.port;
    }

    public isRunning(): boolean {
        return this.process !== null && this.port !== 0;
    }

    public getResourcePath(): string | null {
        return this.resourcePath;
    }

    public async start(): Promise<void> {
        if (this.isRunning()) {
            this.outputChannel.appendLine('Backend already running');
            return;
        }

        // Find or prompt for resource path
        const resourcePath = await this.resolveResourcePath();
        if (!resourcePath) {
            throw new Error('No resource path configured. Use "LRM: Set Resource Path" command to configure.');
        }

        this.resourcePath = resourcePath;

        // Get binary path
        const binaryPath = this.getBinaryPath();
        if (!fs.existsSync(binaryPath)) {
            throw new Error(`LRM binary not found at: ${binaryPath}`);
        }

        // Ensure binary is executable (Unix systems)
        if (process.platform !== 'win32') {
            await this.ensureExecutable(binaryPath);
        }

        // Find available port
        this.port = await this.findAvailablePort();

        this.outputChannel.appendLine(`Starting LRM backend on port ${this.port}...`);
        this.outputChannel.appendLine(`Binary: ${binaryPath}`);
        this.outputChannel.appendLine(`Resource path: ${this.resourcePath}`);

        // Start the backend
        this.process = spawn(binaryPath, [
            'web',
            '--port', this.port.toString(),
            '--no-open-browser',
            '--path', this.resourcePath
        ]);

        // Capture stdout
        this.process.stdout?.on('data', (data) => {
            this.outputChannel.appendLine(`[stdout] ${data.toString().trim()}`);
        });

        // Capture stderr
        this.process.stderr?.on('data', (data) => {
            this.outputChannel.appendLine(`[stderr] ${data.toString().trim()}`);
        });

        // Handle process exit
        this.process.on('exit', (code, signal) => {
            this.outputChannel.appendLine(`Backend exited with code ${code} and signal ${signal}`);
            this.cleanup();
        });

        // Handle process errors
        this.process.on('error', (error) => {
            this.outputChannel.appendLine(`Backend error: ${error.message}`);
            this.cleanup();
        });

        // Wait for backend to be ready
        await this.waitForReady();

        // Start health check
        this.startHealthCheck();

        this.outputChannel.appendLine('Backend started successfully');
    }

    public async stop(): Promise<void> {
        if (!this.isRunning()) {
            return;
        }

        this.outputChannel.appendLine('Stopping backend...');

        // Stop health check
        if (this.healthCheckInterval) {
            clearInterval(this.healthCheckInterval);
            this.healthCheckInterval = null;
        }

        // Kill process
        if (this.process) {
            this.process.kill('SIGTERM');

            // Wait for graceful shutdown
            await new Promise((resolve) => {
                const timeout = setTimeout(() => {
                    if (this.process && !this.process.killed) {
                        this.outputChannel.appendLine('Forcing backend shutdown...');
                        this.process.kill('SIGKILL');
                    }
                    resolve(undefined);
                }, 5000);

                this.process?.on('exit', () => {
                    clearTimeout(timeout);
                    resolve(undefined);
                });
            });
        }

        this.cleanup();
        this.outputChannel.appendLine('Backend stopped');
    }

    public async restart(): Promise<void> {
        await this.stop();
        await this.start();
    }

    // Synchronous dispose for VS Code's disposal mechanism
    // Used when VS Code closes and doesn't wait for async deactivate
    public dispose(): void {
        if (this.process && !this.process.killed) {
            this.process.kill('SIGKILL');
        }
        if (this.healthCheckInterval) {
            clearInterval(this.healthCheckInterval);
            this.healthCheckInterval = null;
        }
        this.cleanup();
    }

    private cleanup(): void {
        this.process = null;
        this.port = 0;
    }

    private getBinaryPath(): string {
        const platform = process.platform;
        const arch = process.arch;

        let binaryName: string;

        if (platform === 'win32') {
            // Windows
            if (arch === 'x64') {
                binaryName = 'win32-x64/lrm.exe';
            } else if (arch === 'arm64') {
                binaryName = 'win32-arm64/lrm.exe';
            } else {
                throw new Error(`Unsupported Windows architecture: ${arch}`);
            }
        } else if (platform === 'darwin') {
            // macOS
            if (arch === 'x64') {
                binaryName = 'darwin-x64/lrm';
            } else if (arch === 'arm64') {
                binaryName = 'darwin-arm64/lrm';
            } else {
                throw new Error(`Unsupported macOS architecture: ${arch}`);
            }
        } else if (platform === 'linux') {
            // Linux
            if (arch === 'x64') {
                binaryName = 'linux-x64/lrm';
            } else if (arch === 'arm64') {
                binaryName = 'linux-arm64/lrm';
            } else {
                throw new Error(`Unsupported Linux architecture: ${arch}`);
            }
        } else {
            throw new Error(`Unsupported platform: ${platform}`);
        }

        return path.join(this.extensionPath, 'bin', binaryName);
    }

    private async ensureExecutable(binaryPath: string): Promise<void> {
        return new Promise((resolve, reject) => {
            fs.chmod(binaryPath, 0o755, (err) => {
                if (err) {
                    reject(new Error(`Failed to make binary executable: ${err.message}`));
                } else {
                    resolve();
                }
            });
        });
    }

    private async findAvailablePort(): Promise<number> {
        const min = 49152;
        const max = 65535;

        // Try up to 100 random ports
        for (let i = 0; i < 100; i++) {
            const port = Math.floor(Math.random() * (max - min + 1)) + min;
            if (await this.isPortAvailable(port)) {
                return port;
            }
        }

        throw new Error('No available port found in ephemeral range (49152-65535)');
    }

    private async isPortAvailable(port: number): Promise<boolean> {
        return new Promise((resolve) => {
            const server = createServer();

            server.once('error', () => {
                resolve(false);
            });

            server.once('listening', () => {
                server.close();
                resolve(true);
            });

            server.listen(port, '127.0.0.1');
        });
    }

    private async waitForReady(timeout: number = 15000): Promise<void> {
        const startTime = Date.now();
        const interval = 500;
        let lastError: any = null;

        this.outputChannel.appendLine('Waiting for backend to be ready...');

        while (Date.now() - startTime < timeout) {
            try {
                const response = await axios.get(`${this.getBaseUrl()}/api/resources`, {
                    timeout: 1000
                });

                if (response.status === 200) {
                    this.outputChannel.appendLine('Backend is ready!');
                    return;
                }
            } catch (error: any) {
                lastError = error;
                // Backend not ready yet, continue waiting
            }

            await new Promise(resolve => setTimeout(resolve, interval));
        }

        const errorMsg = lastError ? `: ${lastError.message}` : '';
        throw new Error(`Backend failed to start within ${timeout}ms${errorMsg}. Check logs for details.`);
    }

    private startHealthCheck(): void {
        // Check health every 30 seconds
        this.healthCheckInterval = setInterval(async () => {
            try {
                await axios.get(`${this.getBaseUrl()}/api/resources`, {
                    timeout: 5000
                });
            } catch (error) {
                this.outputChannel.appendLine('Health check failed - backend appears to be down');

                // Attempt restart
                vscode.window.showWarningMessage(
                    'LRM backend stopped unexpectedly. Restart?',
                    'Restart',
                    'Dismiss'
                ).then(async (choice) => {
                    if (choice === 'Restart') {
                        try {
                            await this.restart();
                            vscode.window.showInformationMessage('LRM backend restarted successfully');
                        } catch (error: any) {
                            vscode.window.showErrorMessage(`Failed to restart backend: ${error.message}`);
                        }
                    }
                });

                // Stop health checks
                if (this.healthCheckInterval) {
                    clearInterval(this.healthCheckInterval);
                    this.healthCheckInterval = null;
                }
            }
        }, 30000);
    }

    private async resolveResourcePath(): Promise<string | null> {
        // 1. Check configuration setting
        const config = vscode.workspace.getConfiguration('lrm');
        const configuredPath = config.get<string>('resourcePath');

        if (configuredPath) {
            // Expand ~ to home directory
            let expandedPath = configuredPath;
            if (configuredPath.startsWith('~')) {
                const homeDir = process.env.HOME || process.env.USERPROFILE;
                if (homeDir) {
                    expandedPath = configuredPath.replace('~', homeDir);
                }
            }

            // Resolve to absolute path
            if (!path.isAbsolute(expandedPath)) {
                const workspaceFolders = vscode.workspace.workspaceFolders;
                if (workspaceFolders && workspaceFolders.length > 0) {
                    expandedPath = path.join(workspaceFolders[0].uri.fsPath, expandedPath);
                }
            }

            if (fs.existsSync(expandedPath)) {
                this.outputChannel.appendLine(`Using configured resource path: ${expandedPath}`);
                return expandedPath;
            } else {
                vscode.window.showWarningMessage(
                    `Configured resource path does not exist: ${expandedPath}. Will auto-detect.`
                );
            }
        }

        // 2. Auto-detect: Search workspace for .resx files
        const autoDetected = await this.autoDetectResourcePath();
        if (autoDetected) {
            this.outputChannel.appendLine(`Auto-detected resource path: ${autoDetected}`);

            // Ask user if they want to save this path (NON-MODAL with timeout)
            const choice = await this.promptForResourcePath(autoDetected);

            if (choice === 'Save') {
                await config.update('resourcePath', autoDetected, vscode.ConfigurationTarget.Workspace);
                this.outputChannel.appendLine(`Saved resource path to settings: ${autoDetected}`);
            } else if (choice === 'Cancel') {
                this.outputChannel.appendLine('Resource path selection cancelled by user');
                return null;
            } else if (choice === 'Use Once' || choice === null) {
                // 'Use Once' explicitly selected OR dismissed/timeout
                if (choice === null) {
                    this.outputChannel.appendLine(`Auto-selected resource path (timeout): ${autoDetected}`);
                    vscode.window.showInformationMessage(
                        `Auto-selected resource folder for this session: ${path.basename(autoDetected)}`,
                        'Configure'
                    ).then(action => {
                        if (action === 'Configure') {
                            vscode.commands.executeCommand('lrm.setResourcePath');
                        }
                    });
                } else {
                    this.outputChannel.appendLine(`Using resource path for this session: ${autoDetected}`);
                }
            }

            return autoDetected;
        }

        // 3. No resources found - prompt user
        return null;
    }

    private async promptForResourcePath(detectedPath: string): Promise<string | null> {
        // Show non-modal notification with timeout
        const timeoutMs = 15000; // 15 seconds
        let userChoice: string | undefined = undefined;
        let timedOut = false;

        // Create a promise that resolves after timeout
        const timeoutPromise = new Promise<null>((resolve) => {
            setTimeout(() => {
                if (userChoice === undefined) {
                    timedOut = true;
                    resolve(null);
                }
            }, timeoutMs);
        });

        // Show the dialog (NON-MODAL)
        const choicePromise = vscode.window.showInformationMessage(
            `Found resource folder: ${path.basename(detectedPath)}`,
            { modal: false }, // NON-MODAL - doesn't block the extension
            'Save to Settings',
            'Use Once',
            'Choose Different...'
        );

        // Race between user selection and timeout
        const choice = await Promise.race([choicePromise, timeoutPromise]);

        if (choice === 'Save to Settings') {
            return 'Save';
        } else if (choice === 'Use Once') {
            return 'Use Once';
        } else if (choice === 'Choose Different...') {
            // Let user manually select a folder
            const selected = await vscode.window.showOpenDialog({
                canSelectFiles: false,
                canSelectFolders: true,
                canSelectMany: false,
                title: 'Select Resource Folder'
            });

            if (selected && selected.length > 0) {
                const newPath = selected[0].fsPath;
                // Ask if they want to save this choice
                const saveChoice = await vscode.window.showInformationMessage(
                    `Save ${path.basename(newPath)} as default resource folder?`,
                    'Save',
                    'Use Once'
                );
                return saveChoice || 'Use Once';
            }
            return 'Cancel';
        } else if (choice === null || timedOut) {
            // Dialog dismissed or timeout - default to "Use Once"
            return null; // Will be treated as "Use Once" with notification
        }

        return 'Cancel';
    }

    private async autoDetectResourcePath(): Promise<string | null> {
        const workspaceFolders = vscode.workspace.workspaceFolders;
        if (!workspaceFolders || workspaceFolders.length === 0) {
            return null;
        }

        // Search for both .resx AND JSON files in workspace
        const excludePattern = '{**/.lrm/**,**/.backup/**,**/node_modules/**,**/bin/**,**/obj/**,**/.git/**,**/.vscode/**,**/.github/**,**/.idea/**,**/.vs/**,**/dist/**,**/build/**,**/.devcontainer/**,**/.claude/**}';

        const [resxFiles, jsonFiles] = await Promise.all([
            vscode.workspace.findFiles('**/*.resx', excludePattern, 10),
            vscode.workspace.findFiles('**/*.json', excludePattern, 50) // More results for filtering
        ]);

        // Filter RESX files to exclude hidden directories (backup folders, etc.)
        const filteredResxFiles = resxFiles.filter(f => !this.isInHiddenDirectory(f.fsPath));

        // Filter JSON files to likely resource files
        const filteredJsonFiles = jsonFiles.filter(f => this.isLikelyResourceFile(f.fsPath));

        // Further filter JSON files: must have valid resource naming pattern
        const validJsonResources = filteredJsonFiles.filter(f => {
            const fileName = path.basename(f.fsPath);
            const { baseName, culture } = this.extractCultureFromFilename(fileName);
            // Accept if it has a culture code OR if it's a potential base file
            return culture !== null || baseName.length > 0;
        });

        // Group files by directory to find resource directories
        const jsonDirs = new Set(validJsonResources.map(f => path.dirname(f.fsPath)));
        const resxDirs = new Set(filteredResxFiles.map(f => path.dirname(f.fsPath)));

        const hasResx = filteredResxFiles.length > 0;
        const hasJson = validJsonResources.length > 0;

        if (!hasResx && !hasJson) {
            return null;
        }

        if (hasResx && hasJson) {
            // BOTH formats found - ask user which to use
            const choice = await this.promptFormatChoice(
                Array.from(resxDirs),
                Array.from(jsonDirs)
            );

            if (!choice || choice.format === null) {
                return null;
            }

            return choice.selectedDir || null;
        }

        // Only one format found
        if (hasResx) {
            return path.dirname(filteredResxFiles[0].fsPath);
        }

        return path.dirname(validJsonResources[0].fsPath);
    }

    /**
     * Checks if a JSON file is likely a resource file (not a config file)
     */
    private isLikelyResourceFile(filePath: string): boolean {
        const fileName = path.basename(filePath).toLowerCase();
        const dirPath = path.dirname(filePath).toLowerCase();

        // EXCLUDE: Hidden directories (starting with .)
        const pathParts = filePath.split(path.sep);
        for (const part of pathParts) {
            if (part.startsWith('.') && part !== '.') {
                return false;
            }
        }

        // EXCLUDE: Known non-resource directory patterns
        const excludeDirPatterns = [
            /[/\\]wwwroot[/\\]/i,      // ASP.NET static files
            /[/\\]assets[/\\]/i,       // Static assets (often have JSON data files)
            /[/\\]public[/\\]/i,       // Public static files
            /[/\\]scripts[/\\]/i,      // Script directories
            /[/\\]models[/\\]/i,       // Model/schema directories
            /[/\\]data[/\\]/i,         // Data directories (not i18n)
            /[/\\]api[/\\]/i,          // API directories
            /[/\\]wasm[/\\]/i,         // WebAssembly output
            /[/\\]wwwroot$/i,          // ASP.NET root
        ];

        // Allow if in known resource directories
        const resourceDirPatterns = [
            /[/\\]locales?[/\\]/i,
            /[/\\]translations?[/\\]/i,
            /[/\\]i18n[/\\]/i,
            /[/\\]lang(uages?)?[/\\]/i,
            /[/\\]resources?[/\\]/i,
            /[/\\]strings?[/\\]/i,
        ];

        const inResourceDir = resourceDirPatterns.some(p => p.test(dirPath));
        if (!inResourceDir) {
            // Not in a known resource directory - apply stricter filtering
            for (const pattern of excludeDirPatterns) {
                if (pattern.test(dirPath)) {
                    return false;
                }
            }
        }

        // EXCLUDE: Known non-resource filename patterns
        const excludePatterns = [
            /^lrm.*\.json$/,           // LRM config
            /^appsettings.*\.json$/,   // ASP.NET config
            /^package(-lock)?\.json$/, // Node.js
            /^npm.*\.json$/,           // npm config
            /^tsconfig.*\.json$/,      // TypeScript
            /^tslint\.json$/,          // TSLint
            /^webpack.*\.json$/,       // Webpack
            /^babel.*\.json$/,         // Babel
            /^rollup.*\.json$/,        // Rollup
            /^vite.*\.json$/,          // Vite
            /^\.?eslint.*\.json$/,     // ESLint
            /^\.?prettier.*\.json$/,   // Prettier
            /^\.?stylelint.*\.json$/,  // Stylelint
            /^jest.*\.json$/,          // Jest
            /^karma.*\.json$/,         // Karma
            /^cypress\.json$/,         // Cypress
            /^settings\.json$/,        // VS Code settings
            /^extensions\.json$/,      // VS Code extensions
            /^launch\.json$/,          // VS Code launch
            /^tasks\.json$/,           // VS Code tasks
            /\.nuget\..*\.json$/,      // NuGet
            /project\.assets\.json$/,  // NuGet
            /packages\.lock\.json$/,   // NuGet
            /.*config\.json$/,         // Generic config
            /.*settings\.json$/,       // Generic settings
            /.*schema\.json$/,         // JSON schemas
            /.*manifest\.json$/,       // Manifests
            /^global\.json$/,          // .NET global.json
            /^launchSettings\.json$/,  // ASP.NET launch settings
            /^\..*\.json$/,            // Hidden JSON files
        ];

        for (const pattern of excludePatterns) {
            if (pattern.test(fileName)) {
                return false;
            }
        }

        return true;
    }

    /**
     * Checks if a file path is inside a hidden directory (starting with .)
     * This includes .lrm, .backup, .git, etc.
     */
    private isInHiddenDirectory(filePath: string): boolean {
        const pathParts = filePath.split(path.sep);
        return pathParts.some(part => part.startsWith('.') && part !== '.');
    }

    /**
     * Common culture codes for quick validation
     */
    private static readonly COMMON_CULTURE_CODES = new Set([
        'en', 'en-us', 'en-gb', 'fr', 'fr-fr', 'fr-ca',
        'de', 'de-de', 'es', 'es-es', 'it', 'it-it',
        'pt', 'pt-br', 'pt-pt', 'ru', 'ja', 'ko', 'zh',
        'zh-hans', 'zh-hant', 'zh-cn', 'zh-tw', 'ar', 'he',
        'nl', 'pl', 'tr', 'el', 'cs', 'sv', 'da', 'fi', 'no',
        'hu', 'th', 'vi', 'id', 'ms', 'uk', 'ro', 'bg', 'hr',
        'sk', 'sl', 'et', 'lv', 'lt', 'sr', 'hi', 'bn', 'ta'
    ]);

    /**
     * Validates if a string looks like a culture code
     */
    private isValidCultureCode(code: string): boolean {
        const lowerCode = code.toLowerCase();
        if (LrmService.COMMON_CULTURE_CODES.has(lowerCode)) {
            return true;
        }
        // Pattern: xx or xx-XX or xx-Xxxx (BCP 47)
        return /^[a-z]{2}(-[a-z]{2,4})?$/i.test(code);
    }

    /**
     * Extracts culture code from filename
     * Returns baseName (without culture) and culture code (if present)
     */
    private extractCultureFromFilename(fileName: string): { baseName: string; culture: string | null } {
        // Remove .json extension
        const nameWithoutExt = fileName.replace(/\.json$/i, '');

        // Check if entire filename is a culture code (i18next style: en.json, fr.json)
        if (this.isValidCultureCode(nameWithoutExt)) {
            return { baseName: '', culture: nameWithoutExt };
        }

        // Check for basename.culture pattern (strings.fr.json)
        const lastDot = nameWithoutExt.lastIndexOf('.');
        if (lastDot > 0) {
            const potentialCulture = nameWithoutExt.substring(lastDot + 1);
            if (this.isValidCultureCode(potentialCulture)) {
                return {
                    baseName: nameWithoutExt.substring(0, lastDot),
                    culture: potentialCulture
                };
            }
        }

        // No culture code found - this is a base/default file
        return { baseName: nameWithoutExt, culture: null };
    }

    /**
     * Detects JSON sub-format (i18next vs standard) by analyzing files in directory
     */
    private async detectJsonSubFormat(dirPath: string): Promise<'i18next' | 'standard'> {
        try {
            const files = await vscode.workspace.fs.readDirectory(vscode.Uri.file(dirPath));
            const jsonFiles = files.filter(([name, type]) =>
                type === vscode.FileType.File && name.endsWith('.json')
            );

            // Check naming pattern: i18next uses culture-only names (en.json, fr.json)
            let i18nextScore = 0;
            let standardScore = 0;

            for (const [fileName] of jsonFiles) {
                const nameWithoutExt = fileName.replace(/\.json$/i, '');

                // i18next pattern: just a culture code (en, fr, de, etc.)
                if (this.isValidCultureCode(nameWithoutExt)) {
                    i18nextScore++;
                }
                // Standard pattern: baseName.culture.json or just baseName.json
                else if (nameWithoutExt.includes('.')) {
                    const parts = nameWithoutExt.split('.');
                    if (parts.length >= 2 && this.isValidCultureCode(parts[parts.length - 1])) {
                        standardScore++;
                    }
                } else {
                    // Base file like "strings.json" - could be either, slight preference for standard
                    standardScore += 0.5;
                }
            }

            return i18nextScore > standardScore ? 'i18next' : 'standard';
        } catch {
            return 'standard';
        }
    }

    /**
     * Prompts user to choose between RESX and JSON when both are found
     */
    private async promptFormatChoice(
        resxDirs: string[],
        jsonDirs: string[]
    ): Promise<{ format: 'resx' | 'json' | null; selectedDir?: string }> {
        interface FormatQuickPickItem extends vscode.QuickPickItem {
            format: 'resx' | 'json';
            dir: string;
        }

        const items: FormatQuickPickItem[] = [];

        // Add RESX options
        for (const dir of resxDirs) {
            items.push({
                label: '$(file-code) RESX',
                description: path.basename(dir),
                detail: dir,
                format: 'resx',
                dir: dir
            });
        }

        // Add JSON options with sub-format detection
        for (const dir of jsonDirs) {
            const subFormat = await this.detectJsonSubFormat(dir);
            const subFormatLabel = subFormat === 'i18next' ? 'i18next' : 'Standard';
            items.push({
                label: `$(json) JSON (${subFormatLabel})`,
                description: path.basename(dir),
                detail: dir,
                format: 'json',
                dir: dir
            });
        }

        const selected = await vscode.window.showQuickPick(items, {
            title: 'Multiple resource formats found',
            placeHolder: 'Select which resource folder to use',
            ignoreFocusOut: true
        });

        if (!selected) {
            return { format: null };
        }

        return {
            format: selected.format,
            selectedDir: selected.dir
        };
    }

    public async setResourcePath(newPath: string): Promise<void> {
        if (!fs.existsSync(newPath)) {
            throw new Error(`Path does not exist: ${newPath}`);
        }

        const config = vscode.workspace.getConfiguration('lrm');
        await config.update('resourcePath', newPath, vscode.ConfigurationTarget.Workspace);

        this.resourcePath = newPath;

        // Restart backend with new path
        if (this.isRunning()) {
            await this.restart();
        }
    }
}
