# Web UI Documentation

LRM includes a browser-based Web UI that provides a modern interface for managing your localization resources. The Web UI is built with Blazor Server and includes a full REST API.

## Starting the Web Server

```bash
# Basic usage - starts on localhost:5000
lrm web --path /path/to/resources

# Custom port
lrm web --port 8080

# Bind to all interfaces (for remote access)
lrm web --bind-address 0.0.0.0

# With HTTPS
lrm web --enable-https --cert-path /path/to/cert.pfx --cert-password mypassword

# Don't auto-open browser
lrm web --no-open-browser
```

## Command Options

| Option | Description | Default |
|--------|-------------|---------|
| `--path`, `-p` | Path to resources folder | Current directory |
| `--source-path` | Path to source code for scanning | Parent of resource path |
| `--port` | Port to bind to | 5000 |
| `--bind-address` | Address to bind to | localhost |
| `--no-open-browser` | Don't auto-open browser | false |
| `--enable-https` | Enable HTTPS | false |
| `--cert-path` | Path to .pfx certificate | - |
| `--cert-password` | Certificate password | - |

## Web UI Pages

### Dashboard

The dashboard provides an overview of your localization resources:

- Resource file count and total keys
- Translation coverage percentage
- Language coverage breakdown with progress bars
- Quick validation status
- Quick action buttons

![Dashboard](../assets/web-dashboard.png)

### Resource Editor

Browse and manage all localization keys:

- Search with wildcards, regex, and case sensitivity
- Filter by status (missing, extra, duplicates)
- Filter by language
- Quick actions (Edit, Refs, Delete) for each key
- Pagination for large resource files

![Editor](../assets/web-editor.png)

### Key Editor

Edit individual keys with full translation support:

- Edit values for all languages
- Show/hide comment fields
- Inline translation with provider selection
- Code reference lookup
- Delete key functionality
- **Plural key support** (JSON backend only):
  - Visual "Plural" badge on plural keys
  - Separate input fields for each plural form (one, other, zero)
  - CLDR plural forms: zero, one, two, few, many, other

![Key Editor](../assets/web-key-editor.png)

### Validation

Run validation checks on your resource files:

- Missing translations
- Extra keys (in non-default but not default)
- Empty values
- Duplicate keys
- Placeholder validation

![Validation](../assets/web-validation.png)

### Code Scanner

Scan source code for localization key usage:

- Find unused keys (in resources but not in code)
- Find missing keys (in code but not in resources)
- View code references for each key
- Cached results for performance

![Code Scanner](../assets/web-scan.png)

### Translation

Batch translate missing translations:

- Select translation provider
- Choose target languages
- Only translate missing values option
- Dry run preview mode

![Translation](../assets/web-translation.png)

### Backups

Manage resource file backups:

- List all backup versions
- Create manual backups
- Restore from backups
- View backup details

![Backups](../assets/web-backup.png)

### Settings

Configure languages and view configuration:

- Language management (add/remove)
- View lrm.json configuration
- Validate configuration
- Edit configuration

![Settings](../assets/web-settings.png)

### Export

Export resources to different formats:

- JSON export
- CSV export
- Include comments option

![Export](../assets/web-export.png)

## REST API

The Web UI is powered by a full REST API. All operations available in the UI can also be performed programmatically.

**API Base URL:** `http://localhost:5000/api`

**Swagger UI:** `http://localhost:5000/swagger`

![Swagger](../assets/web-swagger.png)

For complete API documentation, see [API.md](API.md).

## Configuration

Web server settings can be configured in `lrm.json`:

```json
{
  "web": {
    "port": 5000,
    "bindAddress": "localhost",
    "autoOpenBrowser": true,
    "enableHttps": false,
    "httpsCertificatePath": null,
    "httpsCertificatePassword": null,
    "cors": {
      "enabled": false,
      "allowedOrigins": ["http://localhost:3000"],
      "allowCredentials": false
    }
  }
}
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `LRM_WEB_PORT` | Override default port |
| `LRM_WEB_BIND_ADDRESS` | Override bind address |
| `LRM_WEB_AUTO_OPEN_BROWSER` | Enable/disable auto browser open |
| `LRM_WEB_HTTPS_ENABLED` | Enable HTTPS |
| `LRM_WEB_HTTPS_CERT_PATH` | Certificate path |
| `LRM_WEB_HTTPS_CERT_PASSWORD` | Certificate password |

## Security Considerations

- By default, the server binds to `localhost` only
- Use `--bind-address 0.0.0.0` with caution (exposes to network)
- Enable HTTPS for production use
- Consider firewall rules for remote access
- No built-in authentication (designed for local/trusted use)

## Keyboard Shortcuts

The Web UI supports common keyboard shortcuts:

| Shortcut | Action |
|----------|--------|
| `Ctrl+S` | Save changes |
| `Escape` | Close modal/cancel |
| `Enter` | Confirm action |

## Browser Support

The Web UI is tested on:
- Chrome/Chromium (recommended)
- Firefox
- Edge
- Safari

## Troubleshooting

### Port already in use
```bash
# Use a different port
lrm web --port 8080
```

### Can't access from another machine
```bash
# Bind to all interfaces
lrm web --bind-address 0.0.0.0
```

### HTTPS certificate issues
```bash
# Generate a development certificate
dotnet dev-certs https --export-path ./cert.pfx --password mypassword
lrm web --enable-https --cert-path ./cert.pfx --cert-password mypassword
```
