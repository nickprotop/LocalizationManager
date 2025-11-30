# Web API Reference

LRM provides a REST API when running in web server mode (`lrm web`). This allows programmatic access to all localization management features.

## Starting the Web Server

```bash
# Basic usage (localhost:5000)
lrm web --path /path/to/resources

# Custom port and bind address
lrm web --port 8080 --bind-address 0.0.0.0

# With HTTPS
lrm web --enable-https --cert-path /path/to/cert.pfx --cert-password mypassword

# Don't auto-open browser
lrm web --no-open-browser
```

## Web Server Options

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

## API Endpoints

Base URL: `http://localhost:5000/api`

### Resources

#### List Resource Files
```
GET /api/resources
```
Returns all discovered resource files.

**Response:**
```json
[
  {
    "fileName": "Resources.resx",
    "filePath": "/path/to/Resources.resx",
    "code": "default",
    "isDefault": true
  },
  {
    "fileName": "Resources.el.resx",
    "filePath": "/path/to/Resources.el.resx",
    "code": "el",
    "isDefault": false
  }
]
```

#### Get All Keys
```
GET /api/resources/keys
```
Returns all localization keys with values across all languages.

**Response:**
```json
[
  {
    "key": "SaveButton",
    "values": {
      "default": "Save",
      "el": "Αποθήκευση",
      "fr": "Enregistrer"
    },
    "occurrenceCount": 1,
    "hasDuplicates": false
  }
]
```

#### Get Key Details
```
GET /api/resources/keys/{keyName}
```
Returns detailed information for a specific key, including comments.

**Parameters:**
- `occurrence` (optional): Specific occurrence index for duplicate keys

**Response:**
```json
{
  "key": "SaveButton",
  "values": {
    "default": { "value": "Save", "comment": "Button text" },
    "el": { "value": "Αποθήκευση", "comment": null }
  },
  "occurrenceCount": 1,
  "hasDuplicates": false
}
```

#### Add Key
```
POST /api/resources/keys
```
Add a new localization key.

**Request Body:**
```json
{
  "key": "NewKey",
  "values": {
    "default": "Default value",
    "el": "Greek value"
  },
  "comment": "Optional comment"
}
```

#### Update Key
```
PUT /api/resources/keys/{keyName}
```
Update an existing key's values.

**Parameters:**
- `occurrence` (optional): Specific occurrence index for duplicate keys

**Request Body:**
```json
{
  "values": {
    "default": { "value": "Updated value", "comment": "Updated comment" }
  }
}
```

#### Delete Key
```
DELETE /api/resources/keys/{keyName}
```
Delete a key from all language files.

**Parameters:**
- `occurrence` (optional): Specific occurrence index for duplicate keys
- `allDuplicates` (optional): Delete all occurrences of duplicate key

---

### Search

#### Search Keys
```
POST /api/search
```
Search and filter resource keys with advanced options.

**Request Body:**
```json
{
  "pattern": "Error*",
  "filterMode": "wildcard",
  "caseSensitive": false,
  "searchScope": "keysAndValues",
  "statusFilters": ["missing", "duplicates"],
  "limit": 100,
  "offset": 0
}
```

**Filter Modes:**
- `substring` (default): Simple contains match
- `wildcard`: Supports `*` (any chars) and `?` (single char)
- `regex`: Full regular expression

**Search Scopes:**
- `keys`: Key names only
- `values`: Translation values only
- `keysAndValues` (default): Both keys and values
- `comments`: Comments only
- `all`: Keys, values, and comments

**Status Filters:**
- `missing`: Keys with missing translations
- `extra`: Keys in non-default but not in default
- `duplicates`: Keys with multiple occurrences

**Response:**
```json
{
  "results": [...],
  "totalCount": 150,
  "filteredCount": 25,
  "appliedFilterMode": "wildcard"
}
```

---

### Validation

#### Validate Resources
```
GET /api/validation
```
Run validation checks on all resource files.

**Response:**
```json
{
  "isValid": false,
  "missingKeys": {
    "el": ["NewKey", "AnotherKey"],
    "fr": ["NewKey"]
  },
  "extraKeys": {},
  "emptyValues": {},
  "duplicateKeys": ["SomeKey"]
}
```

---

### Translation

#### Get Translation Providers
```
GET /api/translation/providers
```
List available translation providers and their configuration status.

**Response:**
```json
[
  {
    "name": "google",
    "displayName": "Google Cloud Translation",
    "isConfigured": true,
    "requiresApiKey": true
  }
]
```

#### Translate Key
```
POST /api/translation/translate
```
Translate a specific key to target languages.

**Request Body:**
```json
{
  "key": "SaveButton",
  "provider": "google",
  "targetLanguages": ["el", "fr", "de"],
  "onlyMissing": true
}
```

**Response:**
```json
{
  "key": "SaveButton",
  "translations": {
    "el": "Αποθήκευση",
    "fr": "Enregistrer",
    "de": "Speichern"
  },
  "provider": "google",
  "cached": false
}
```

#### Translate All Missing
```
POST /api/translation/translate-all
```
Translate all missing values across all keys.

**Request Body:**
```json
{
  "provider": "google",
  "targetLanguages": ["el", "fr"],
  "onlyMissing": true,
  "dryRun": false
}
```

---

### Code Scanning

#### Run Code Scan
```
GET /api/scan
```
Scan source code for localization key usage.

**Response:**
```json
{
  "scannedFiles": 150,
  "totalReferences": 423,
  "unusedKeysCount": 5,
  "missingKeysCount": 2,
  "unused": ["OldKey1", "OldKey2"],
  "missing": ["NewCodeKey"],
  "references": [
    {
      "key": "SaveButton",
      "referenceCount": 12,
      "references": [...]
    }
  ]
}
```

#### Get Unused Keys
```
GET /api/scan/unused
```
Get keys that exist in resources but are not referenced in code.

#### Get Missing Keys
```
GET /api/scan/missing
```
Get keys referenced in code but not in resource files.

#### Get Key References
```
GET /api/scan/references/{keyName}
```
Get source code locations where a key is used.

**Response:**
```json
{
  "key": "SaveButton",
  "references": [
    {
      "file": "src/Views/MainWindow.cs",
      "line": 42,
      "pattern": "Resources.SaveButton",
      "confidence": "High"
    }
  ]
}
```

#### Scan Single File
```
POST /api/scan/file
```
Scan a single source code file for localization key references. Useful for editor integrations and real-time validation.

**Request:**
```json
{
  "filePath": "/path/to/Controllers/HomeController.cs",
  "content": "optional file content string"
}
```

**Parameters:**
- `filePath` (required): Absolute path to the file to scan (used for extension detection and result paths)
- `content` (optional): File content as a string. If provided, this content will be scanned instead of reading the file from disk. This is useful for scanning unsaved editor changes.

**Request Examples:**

Scan file from disk:
```json
{
  "filePath": "/path/to/Controllers/HomeController.cs"
}
```

Scan in-memory content (e.g., unsaved editor changes):
```json
{
  "filePath": "/path/to/Controllers/HomeController.cs",
  "content": "public class HomeController {\n  var msg = Resources.NewUnsavedKey;\n}"
}
```

**Response:**
Returns the same format as full codebase scan (`ScanResponse`), but with `scannedFiles: 1` and empty `unused` array (unused keys require full codebase scan).

```json
{
  "scannedFiles": 1,
  "totalReferences": 5,
  "uniqueKeysFound": 3,
  "unusedKeysCount": 0,
  "missingKeysCount": 2,
  "unused": [],
  "missing": ["NewKey", "AnotherMissingKey"],
  "references": [
    {
      "key": "WelcomeMessage",
      "referenceCount": 2,
      "references": [
        {
          "file": "/path/to/Controllers/HomeController.cs",
          "line": 23,
          "pattern": "Resources.WelcomeMessage",
          "confidence": "High"
        },
        {
          "file": "/path/to/Controllers/HomeController.cs",
          "line": 67,
          "pattern": "Resources.WelcomeMessage",
          "confidence": "High"
        }
      ]
    },
    {
      "key": "NewKey",
      "referenceCount": 1,
      "references": [
        {
          "file": "/path/to/Controllers/HomeController.cs",
          "line": 45,
          "pattern": "Resources.NewKey",
          "confidence": "High"
        }
      ]
    }
  ]
}
```

**Notes:**
- The `filePath` should be an absolute path to a supported file type (.cs, .razor, .xaml, .cshtml)
- When `content` is provided, the file doesn't need to exist on disk - only the extension is used for scanner selection
- The `content` parameter enables real-time scanning of unsaved editor changes
- Only supported file extensions will be scanned
- Response format is identical to full codebase scan for consistency
- `scannedFiles` will always be `1` for single-file scans
- `unused` will always be empty (unused keys can only be determined by scanning entire codebase)
- This consistent format makes it easy to add wildcard file support in the future

---

### Statistics

#### Get Stats
```
GET /api/stats
```
Get translation coverage statistics.

**Response:**
```json
{
  "totalKeys": 150,
  "languages": [
    {
      "code": "default",
      "name": "Default",
      "translatedCount": 150,
      "missingCount": 0,
      "emptyCount": 0,
      "coverage": 100.0
    },
    {
      "code": "el",
      "name": "Greek",
      "translatedCount": 142,
      "missingCount": 8,
      "emptyCount": 0,
      "coverage": 94.67
    }
  ]
}
```

---

### Languages

#### List Languages
```
GET /api/languages
```
List all language files.

#### Add Language
```
POST /api/languages
```
Create a new language file.

**Request Body:**
```json
{
  "culture": "de",
  "copyFrom": "default"
}
```

#### Remove Language
```
DELETE /api/languages/{culture}
```
Remove a language file.

---

### Backup

#### List Backups
```
GET /api/backup
```
List all backup versions.

**Query Parameters:**
- `file`: Filter by resource file name
- `limit`: Maximum number to return

#### Create Backup
```
POST /api/backup
```
Create a manual backup.

**Request Body:**
```json
{
  "description": "Before major changes"
}
```

#### Restore Backup
```
POST /api/backup/restore
```
Restore from a backup version.

**Request Body:**
```json
{
  "file": "Resources.resx",
  "version": 5,
  "keys": ["Key1", "Key2"]
}
```

---

### Export/Import

#### Export Resources
```
GET /api/export?format=csv
```
Export resources to CSV or JSON format.

**Query Parameters:**
- `format`: `csv` or `json`

#### Import Resources
```
POST /api/import
```
Import translations from CSV.

**Request Body:** multipart/form-data with CSV file

---

### Merge Duplicates

#### Get Duplicates
```
GET /api/merge-duplicates
```
List all keys with duplicate entries.

#### Merge Key
```
POST /api/merge-duplicates/{keyName}
```
Merge duplicate occurrences of a key.

**Request Body:**
```json
{
  "keepOccurrence": 1
}
```

---

### Credentials

Manage API keys securely using the encrypted credential store.

#### List Providers with Status
```
GET /api/credentials/providers
```
Get all translation providers with their credential configuration status.

**Response:**
```json
{
  "providers": [
    {
      "provider": "google",
      "displayName": "Google Cloud Translation",
      "requiresApiKey": true,
      "source": "secure_store",
      "isConfigured": true
    },
    {
      "provider": "openai",
      "displayName": "OpenAI",
      "requiresApiKey": true,
      "source": "environment",
      "isConfigured": true
    },
    {
      "provider": "lingva",
      "displayName": "Lingva Translate",
      "requiresApiKey": false,
      "source": null,
      "isConfigured": true
    }
  ],
  "useSecureCredentialStore": true
}
```

**Source Values:**
- `environment`: API key from environment variable (`LRM_GOOGLE_API_KEY`, etc.)
- `secure_store`: API key from encrypted credential store
- `config_file`: API key from `lrm.json` (plain text)
- `null`: No API key configured

#### Set API Key
```
PUT /api/credentials/{provider}
```
Store an API key in the secure credential store (AES-256 encrypted).

**Request Body:**
```json
{
  "apiKey": "sk-your-api-key-here"
}
```

**Response:**
```json
{
  "success": true,
  "message": "API key for google stored securely"
}
```

#### Delete API Key
```
DELETE /api/credentials/{provider}
```
Remove an API key from the secure credential store.

**Response:**
```json
{
  "success": true,
  "message": "API key for google removed from secure store"
}
```

#### Get API Key Source
```
GET /api/credentials/{provider}/source
```
Get where an API key is configured (without revealing the actual key).

**Response:**
```json
{
  "provider": "google",
  "source": "secure_store",
  "isConfigured": true
}
```

#### Test Provider
```
POST /api/credentials/{provider}/test
```
Test provider connection by performing a sample translation.

**Response (Success):**
```json
{
  "success": true,
  "provider": "google",
  "message": "Connection successful! Test translation: 'Hello' -> 'Hola'"
}
```

**Response (Failure):**
```json
{
  "success": false,
  "provider": "google",
  "message": "Authentication failed - check your API key"
}
```

#### Enable/Disable Secure Store
```
PUT /api/credentials/secure-store
```
Enable or disable the secure credential store in configuration.

**Request Body:**
```json
{
  "enabled": true
}
```

---

## Swagger UI

Interactive API documentation is available at:
```
http://localhost:5000/swagger
```

## Configuration via lrm.json

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

## Error Responses

All endpoints return errors in this format:

```json
{
  "error": "Error message description"
}
```

HTTP Status Codes:
- `200`: Success
- `400`: Bad request (invalid parameters)
- `404`: Resource not found
- `500`: Server error
