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

export class LrmService {
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

        // Search for .resx files in workspace
        const resxFiles = await vscode.workspace.findFiles(
            '**/*.resx',
            '**/node_modules/**',
            10 // Limit to 10 files for performance
        );

        if (resxFiles.length === 0) {
            return null;
        }

        // Get directory of first .resx file found
        const firstResxPath = resxFiles[0].fsPath;
        const resourceDir = path.dirname(firstResxPath);

        return resourceDir;
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
