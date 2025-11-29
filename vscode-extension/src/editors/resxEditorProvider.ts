import * as vscode from 'vscode';
import { LrmService } from '../services/lrmService';

export class ResxEditorProvider implements vscode.CustomTextEditorProvider {
    public static readonly viewType = 'lrm.resxEditor';

    constructor(
        private readonly context: vscode.ExtensionContext,
        private readonly lrmService: LrmService
    ) {}

    public static register(
        context: vscode.ExtensionContext,
        lrmService: LrmService
    ): vscode.Disposable {
        const provider = new ResxEditorProvider(context, lrmService);
        return vscode.window.registerCustomEditorProvider(
            ResxEditorProvider.viewType,
            provider,
            {
                webviewOptions: {
                    retainContextWhenHidden: true
                },
                supportsMultipleEditorsPerDocument: false
            }
        );
    }

    public async resolveCustomTextEditor(
        document: vscode.TextDocument,
        webviewPanel: vscode.WebviewPanel,
        _token: vscode.CancellationToken
    ): Promise<void> {
        webviewPanel.webview.options = {
            enableScripts: true,
            localResourceRoots: [
                vscode.Uri.joinPath(this.context.extensionUri, 'media'),
                vscode.Uri.joinPath(this.context.extensionUri, 'webview-ui')
            ]
        };

        webviewPanel.webview.html = this.getHtmlForWebview(webviewPanel.webview);

        // Send initial data
        const updateWebview = async () => {
            const resources = this.parseResxDocument(document);
            webviewPanel.webview.postMessage({
                type: 'update',
                data: {
                    fileName: document.fileName,
                    resources
                }
            });
        };

        // Handle messages from the webview
        webviewPanel.webview.onDidReceiveMessage(async (message) => {
            switch (message.type) {
                case 'edit':
                    await this.handleEdit(document, message.key, message.value, message.comment);
                    break;
                case 'add':
                    await this.handleAdd(document, message.key, message.value, message.comment);
                    break;
                case 'delete':
                    await this.handleDelete(document, message.key);
                    break;
                case 'translate':
                    await this.handleTranslate(document, message.key, message.targetLanguage);
                    break;
                case 'ready':
                    await updateWebview();
                    break;
            }
        });

        // Update webview when document changes
        const changeDocumentSubscription = vscode.workspace.onDidChangeTextDocument((e) => {
            if (e.document.uri.toString() === document.uri.toString()) {
                updateWebview();
            }
        });

        webviewPanel.onDidDispose(() => {
            changeDocumentSubscription.dispose();
        });
    }

    private parseResxDocument(document: vscode.TextDocument): Array<{ name: string; value: string; comment?: string }> {
        const resources: Array<{ name: string; value: string; comment?: string }> = [];
        const text = document.getText();

        // Parse XML to extract data elements
        const dataRegex = /<data\s+name="([^"]+)"[^>]*>\s*<value>([^<]*)<\/value>(?:\s*<comment>([^<]*)<\/comment>)?/gi;

        let match;
        while ((match = dataRegex.exec(text)) !== null) {
            resources.push({
                name: match[1],
                value: this.decodeXmlEntities(match[2]),
                comment: match[3] ? this.decodeXmlEntities(match[3]) : undefined
            });
        }

        return resources;
    }

    private decodeXmlEntities(text: string): string {
        return text
            .replace(/&lt;/g, '<')
            .replace(/&gt;/g, '>')
            .replace(/&amp;/g, '&')
            .replace(/&quot;/g, '"')
            .replace(/&apos;/g, "'");
    }

    private encodeXmlEntities(text: string): string {
        return text
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&apos;');
    }

    private async handleEdit(
        document: vscode.TextDocument,
        key: string,
        value: string,
        comment?: string
    ): Promise<void> {
        const text = document.getText();
        const encodedValue = this.encodeXmlEntities(value);

        // Find and replace the data element
        let newText: string;
        const dataRegex = new RegExp(
            `(<data\\s+name="${this.escapeRegex(key)}"[^>]*>\\s*<value>)[^<]*(<\\/value>(?:\\s*<comment>)[^<]*(?:<\\/comment>)?)?`,
            'i'
        );

        if (comment !== undefined) {
            const encodedComment = this.encodeXmlEntities(comment);
            newText = text.replace(dataRegex, `$1${encodedValue}</value>\n    <comment>${encodedComment}</comment>`);
        } else {
            newText = text.replace(dataRegex, `$1${encodedValue}$2`);
        }

        if (newText !== text) {
            const edit = new vscode.WorkspaceEdit();
            edit.replace(
                document.uri,
                new vscode.Range(0, 0, document.lineCount, 0),
                newText
            );
            await vscode.workspace.applyEdit(edit);
        }
    }

    private async handleAdd(
        document: vscode.TextDocument,
        key: string,
        value: string,
        comment?: string
    ): Promise<void> {
        const text = document.getText();
        const encodedKey = this.encodeXmlEntities(key);
        const encodedValue = this.encodeXmlEntities(value);

        let newDataElement = `  <data name="${encodedKey}" xml:space="preserve">\n    <value>${encodedValue}</value>\n`;
        if (comment) {
            newDataElement += `    <comment>${this.encodeXmlEntities(comment)}</comment>\n`;
        }
        newDataElement += '  </data>\n';

        // Find the position to insert (before </root>)
        const insertPosition = text.lastIndexOf('</root>');
        if (insertPosition === -1) {
            vscode.window.showErrorMessage('Invalid .resx file format');
            return;
        }

        const newText = text.slice(0, insertPosition) + newDataElement + text.slice(insertPosition);

        const edit = new vscode.WorkspaceEdit();
        edit.replace(
            document.uri,
            new vscode.Range(0, 0, document.lineCount, 0),
            newText
        );
        await vscode.workspace.applyEdit(edit);
    }

    private async handleDelete(document: vscode.TextDocument, key: string): Promise<void> {
        const text = document.getText();

        // Remove the data element
        const dataRegex = new RegExp(
            `\\s*<data\\s+name="${this.escapeRegex(key)}"[^>]*>[\\s\\S]*?<\\/data>`,
            'i'
        );

        const newText = text.replace(dataRegex, '');

        if (newText !== text) {
            const edit = new vscode.WorkspaceEdit();
            edit.replace(
                document.uri,
                new vscode.Range(0, 0, document.lineCount, 0),
                newText
            );
            await vscode.workspace.applyEdit(edit);
        }
    }

    private async handleTranslate(
        document: vscode.TextDocument,
        key: string,
        targetLanguage: string
    ): Promise<void> {
        const result = await this.lrmService.translate(document.uri.fsPath, targetLanguage, {
            keyPattern: key,
            onlyMissing: false
        });

        if (!result.success) {
            vscode.window.showErrorMessage(`Translation failed: ${result.errors.join(', ')}`);
        }
    }

    private escapeRegex(str: string): string {
        return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    private getHtmlForWebview(webview: vscode.Webview): string {
        const nonce = this.getNonce();

        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${webview.cspSource} 'unsafe-inline'; script-src 'nonce-${nonce}';">
    <title>Resource Editor</title>
    <style>
        :root {
            --container-padding: 20px;
            --input-padding: 6px 10px;
        }

        body {
            padding: var(--container-padding);
            color: var(--vscode-foreground);
            font-size: var(--vscode-font-size);
            font-weight: var(--vscode-font-weight);
            font-family: var(--vscode-font-family);
            background-color: var(--vscode-editor-background);
        }

        .header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
            padding-bottom: 10px;
            border-bottom: 1px solid var(--vscode-panel-border);
        }

        .header h1 {
            margin: 0;
            font-size: 1.2em;
        }

        .toolbar {
            display: flex;
            gap: 10px;
        }

        button {
            background-color: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
            border: none;
            padding: var(--input-padding);
            cursor: pointer;
            font-family: inherit;
            font-size: inherit;
            border-radius: 2px;
        }

        button:hover {
            background-color: var(--vscode-button-hoverBackground);
        }

        button.secondary {
            background-color: var(--vscode-button-secondaryBackground);
            color: var(--vscode-button-secondaryForeground);
        }

        button.danger {
            background-color: var(--vscode-inputValidation-errorBackground);
        }

        .search-container {
            margin-bottom: 20px;
        }

        input[type="text"], input[type="search"] {
            background-color: var(--vscode-input-background);
            color: var(--vscode-input-foreground);
            border: 1px solid var(--vscode-input-border);
            padding: var(--input-padding);
            font-family: inherit;
            font-size: inherit;
            width: 100%;
            box-sizing: border-box;
            border-radius: 2px;
        }

        input:focus {
            outline: 1px solid var(--vscode-focusBorder);
        }

        textarea {
            background-color: var(--vscode-input-background);
            color: var(--vscode-input-foreground);
            border: 1px solid var(--vscode-input-border);
            padding: var(--input-padding);
            font-family: inherit;
            font-size: inherit;
            width: 100%;
            box-sizing: border-box;
            resize: vertical;
            min-height: 60px;
            border-radius: 2px;
        }

        .resources-table {
            width: 100%;
            border-collapse: collapse;
        }

        .resources-table th,
        .resources-table td {
            text-align: left;
            padding: 8px 12px;
            border-bottom: 1px solid var(--vscode-panel-border);
        }

        .resources-table th {
            background-color: var(--vscode-editor-background);
            font-weight: 600;
            position: sticky;
            top: 0;
            z-index: 1;
        }

        .resources-table tr:hover {
            background-color: var(--vscode-list-hoverBackground);
        }

        .resources-table .key-cell {
            font-family: var(--vscode-editor-font-family);
            font-weight: 500;
            width: 30%;
        }

        .resources-table .value-cell {
            width: 50%;
        }

        .resources-table .actions-cell {
            width: 20%;
            text-align: right;
        }

        .actions-cell button {
            margin-left: 5px;
            padding: 4px 8px;
            font-size: 0.9em;
        }

        .modal {
            display: none;
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background-color: rgba(0, 0, 0, 0.5);
            z-index: 100;
            justify-content: center;
            align-items: center;
        }

        .modal.active {
            display: flex;
        }

        .modal-content {
            background-color: var(--vscode-editor-background);
            padding: 20px;
            border-radius: 4px;
            min-width: 400px;
            max-width: 600px;
            border: 1px solid var(--vscode-panel-border);
        }

        .modal-content h2 {
            margin-top: 0;
        }

        .form-group {
            margin-bottom: 15px;
        }

        .form-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
        }

        .modal-actions {
            display: flex;
            justify-content: flex-end;
            gap: 10px;
            margin-top: 20px;
        }

        .empty-state {
            text-align: center;
            padding: 40px;
            color: var(--vscode-descriptionForeground);
        }

        .badge {
            display: inline-block;
            padding: 2px 6px;
            border-radius: 10px;
            font-size: 0.8em;
            margin-left: 8px;
        }

        .badge.count {
            background-color: var(--vscode-badge-background);
            color: var(--vscode-badge-foreground);
        }
    </style>
</head>
<body>
    <div class="header">
        <h1>Resource Editor <span class="badge count" id="resourceCount">0</span></h1>
        <div class="toolbar">
            <button onclick="showAddModal()">+ Add Key</button>
        </div>
    </div>

    <div class="search-container">
        <input type="search" id="searchInput" placeholder="Search keys or values..." oninput="filterResources()">
    </div>

    <table class="resources-table">
        <thead>
            <tr>
                <th>Key</th>
                <th>Value</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody id="resourcesBody">
        </tbody>
    </table>

    <div class="empty-state" id="emptyState" style="display: none;">
        No resources found. Click "Add Key" to create your first resource.
    </div>

    <!-- Add/Edit Modal -->
    <div class="modal" id="editModal">
        <div class="modal-content">
            <h2 id="modalTitle">Add Resource</h2>
            <form id="editForm" onsubmit="saveResource(event)">
                <input type="hidden" id="editMode" value="add">
                <div class="form-group">
                    <label for="keyInput">Key</label>
                    <input type="text" id="keyInput" required placeholder="e.g., App.WelcomeMessage">
                </div>
                <div class="form-group">
                    <label for="valueInput">Value</label>
                    <textarea id="valueInput" required placeholder="Enter the text value"></textarea>
                </div>
                <div class="form-group">
                    <label for="commentInput">Comment (optional)</label>
                    <input type="text" id="commentInput" placeholder="Description of this resource">
                </div>
                <div class="modal-actions">
                    <button type="button" class="secondary" onclick="closeModal()">Cancel</button>
                    <button type="submit">Save</button>
                </div>
            </form>
        </div>
    </div>

    <script nonce="${nonce}">
        const vscode = acquireVsCodeApi();
        let resources = [];

        // Handle messages from extension
        window.addEventListener('message', event => {
            const message = event.data;
            switch (message.type) {
                case 'update':
                    resources = message.data.resources || [];
                    renderResources();
                    break;
            }
        });

        function renderResources() {
            const tbody = document.getElementById('resourcesBody');
            const emptyState = document.getElementById('emptyState');
            const countBadge = document.getElementById('resourceCount');
            const searchTerm = document.getElementById('searchInput').value.toLowerCase();

            const filtered = resources.filter(r =>
                r.name.toLowerCase().includes(searchTerm) ||
                r.value.toLowerCase().includes(searchTerm)
            );

            countBadge.textContent = resources.length;

            if (filtered.length === 0) {
                tbody.innerHTML = '';
                emptyState.style.display = 'block';
                return;
            }

            emptyState.style.display = 'none';

            tbody.innerHTML = filtered.map(r => \`
                <tr>
                    <td class="key-cell">\${escapeHtml(r.name)}</td>
                    <td class="value-cell">\${escapeHtml(r.value)}</td>
                    <td class="actions-cell">
                        <button onclick="editResource('\${escapeAttr(r.name)}')">Edit</button>
                        <button class="danger" onclick="deleteResource('\${escapeAttr(r.name)}')">Delete</button>
                    </td>
                </tr>
            \`).join('');
        }

        function filterResources() {
            renderResources();
        }

        function showAddModal() {
            document.getElementById('modalTitle').textContent = 'Add Resource';
            document.getElementById('editMode').value = 'add';
            document.getElementById('keyInput').value = '';
            document.getElementById('keyInput').disabled = false;
            document.getElementById('valueInput').value = '';
            document.getElementById('commentInput').value = '';
            document.getElementById('editModal').classList.add('active');
            document.getElementById('keyInput').focus();
        }

        function editResource(key) {
            const resource = resources.find(r => r.name === key);
            if (!resource) return;

            document.getElementById('modalTitle').textContent = 'Edit Resource';
            document.getElementById('editMode').value = 'edit';
            document.getElementById('keyInput').value = resource.name;
            document.getElementById('keyInput').disabled = true;
            document.getElementById('valueInput').value = resource.value;
            document.getElementById('commentInput').value = resource.comment || '';
            document.getElementById('editModal').classList.add('active');
            document.getElementById('valueInput').focus();
        }

        function closeModal() {
            document.getElementById('editModal').classList.remove('active');
        }

        function saveResource(event) {
            event.preventDefault();

            const mode = document.getElementById('editMode').value;
            const key = document.getElementById('keyInput').value;
            const value = document.getElementById('valueInput').value;
            const comment = document.getElementById('commentInput').value;

            vscode.postMessage({
                type: mode === 'add' ? 'add' : 'edit',
                key,
                value,
                comment: comment || undefined
            });

            closeModal();
        }

        function deleteResource(key) {
            if (confirm(\`Delete "\${key}"?\`)) {
                vscode.postMessage({
                    type: 'delete',
                    key
                });
            }
        }

        function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        function escapeAttr(text) {
            return text.replace(/'/g, "\\\\'").replace(/"/g, '\\\\"');
        }

        // Close modal on escape
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                closeModal();
            }
        });

        // Close modal on backdrop click
        document.getElementById('editModal').addEventListener('click', (e) => {
            if (e.target.id === 'editModal') {
                closeModal();
            }
        });

        // Tell extension we're ready
        vscode.postMessage({ type: 'ready' });
    </script>
</body>
</html>`;
    }

    private getNonce(): string {
        let text = '';
        const possible = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
        for (let i = 0; i < 32; i++) {
            text += possible.charAt(Math.floor(Math.random() * possible.length));
        }
        return text;
    }
}
