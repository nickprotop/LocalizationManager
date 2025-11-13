# LocalizationManager Configuration Guide

This guide explains all available configuration options for LocalizationManager (lrm).

## Configuration File

Create a file named `lrm.json` in the same directory as your resource files. You can copy and customize `lrm-sample.json` as a starting point.

## Priority Order

Configuration values are resolved in the following priority order:

1. **Command-line arguments** (highest priority)
2. **Configuration file** (`lrm.json`)
3. **Environment variables** (for API keys)
4. **Default values** (lowest priority)

---

## Configuration Options

### General Settings

#### `defaultLanguageCode`

- **Type:** `string`
- **Default:** `null` (displays as "default" for display, auto-detect for translations)
- **Description:** The language code for the default resource file (e.g., "en", "fr"). Used for both display output and as the source language for translations. If not set, displays "default" and translations use auto-detect.
- **Example:**
  ```json
  "defaultLanguageCode": "en"
  ```

---

### Translation Settings

Configuration for the machine translation feature.

#### `translation.defaultProvider`

- **Type:** `string`
- **Default:** `"google"`
- **Allowed values:** `"google"`, `"deepl"`, `"libretranslate"`, `"azuretranslator"`, `"ollama"`, `"openai"`, `"claude"`, `"azureopenai"`
- **Description:** The default translation provider to use when not explicitly specified.
- **Example:**
  ```json
  "defaultProvider": "deepl"
  ```

#### `translation.maxRetries`

- **Type:** `integer`
- **Default:** `3`
- **Description:** Maximum number of retry attempts for failed translation requests.
- **Example:**
  ```json
  "maxRetries": 5
  ```

#### `translation.timeoutSeconds`

- **Type:** `integer`
- **Default:** `30`
- **Description:** Timeout in seconds for translation requests.
- **Example:**
  ```json
  "timeoutSeconds": 60
  ```

#### `translation.batchSize`

- **Type:** `integer`
- **Default:** `10`
- **Description:** Number of keys to translate in a single batch request. Larger batches are faster but may exceed provider limits.
- **Example:**
  ```json
  "batchSize": 20
  ```

#### `translation.useSecureCredentialStore`

- **Type:** `boolean`
- **Default:** `false`
- **Description:** Whether to use the secure credential store for API keys. When enabled, API keys can be stored encrypted in the user's application data directory. Use the `lrm config` command to manage credentials securely.
- **Example:**
  ```json
  "useSecureCredentialStore": true
  ```

---

### API Keys

**⚠️ WARNING:** Do not commit API keys to version control! Add `lrm.json` to `.gitignore` if it contains keys.

**Priority order for API keys:**

1. Environment variables (most secure)
2. Secure credential store (if enabled)
3. Configuration file values (least secure)

#### `translation.apiKeys.google`

- **Type:** `string`
- **Environment variable:** `LRM_GOOGLE_API_KEY`
- **Description:** Google Cloud Translation API key
- **Get your key:** https://cloud.google.com/translate/docs/setup
- **Example:**
  ```json
  "google": "AIzaSyD..."
  ```

#### `translation.apiKeys.deepL`

- **Type:** `string`
- **Environment variable:** `LRM_DEEPL_API_KEY`
- **Description:** DeepL API key
- **Get your key:** https://www.deepl.com/pro-api
- **Example:**
  ```json
  "deepL": "abc123..."
  ```

#### `translation.apiKeys.libreTranslate`

- **Type:** `string`
- **Environment variable:** `LRM_LIBRETRANSLATE_API_KEY`
- **Description:** LibreTranslate API key (optional for public instances)
- **Learn more:** https://libretranslate.com/
- **Example:**
  ```json
  "libreTranslate": "xyz789..."
  ```

#### `translation.apiKeys.openAI`

- **Type:** `string`
- **Environment variable:** `LRM_OPENAI_API_KEY`
- **Description:** OpenAI API key for GPT models
- **Get your key:** https://platform.openai.com/api-keys
- **Example:**
  ```json
  "openAI": "sk-..."
  ```

#### `translation.apiKeys.claude`

- **Type:** `string`
- **Environment variable:** `LRM_CLAUDE_API_KEY`
- **Description:** Anthropic Claude API key
- **Get your key:** https://console.anthropic.com/
- **Example:**
  ```json
  "claude": "sk-ant-..."
  ```

#### `translation.apiKeys.azureOpenAI`

- **Type:** `string`
- **Environment variable:** `LRM_AZUREOPENAI_API_KEY`
- **Description:** Azure OpenAI API key
- **Get your key:** Azure Portal
- **Example:**
  ```json
  "azureOpenAI": "..."
  ```

#### `translation.apiKeys.azureTranslator`

- **Type:** `string`
- **Environment variable:** `LRM_AZURETRANSLATOR_API_KEY`
- **Description:** Azure AI Translator subscription key (Ocp-Apim-Subscription-Key)
- **Get your key:** Azure Portal → Cognitive Services → Translator → Keys and Endpoint
- **Example:**
  ```json
  "azureTranslator": "..."
  ```

---

### AI Provider Settings

Advanced configuration for AI-powered translation providers.

#### `translation.aiProviders.ollama`

Configuration for Ollama (local LLM) provider.

- **Type:** `object`
- **Properties:**
  - `apiUrl` (string, default: `"http://localhost:11434"`): Ollama server endpoint
  - `model` (string, default: `"llama3.2"`): Model to use (e.g., "llama3.2", "mistral", "phi")
  - `customSystemPrompt` (string, optional): Custom translation prompt
  - `rateLimitPerMinute` (integer, default: `10`): Request rate limit
- **Example:**
  ```json
  "ollama": {
    "apiUrl": "http://localhost:11434",
    "model": "llama3.2",
    "customSystemPrompt": null,
    "rateLimitPerMinute": 10
  }
  ```

#### `translation.aiProviders.openAI`

Configuration for OpenAI GPT provider.

- **Type:** `object`
- **Properties:**
  - `model` (string, default: `"gpt-4o-mini"`): Model to use (e.g., "gpt-4", "gpt-4o-mini", "gpt-3.5-turbo")
  - `customSystemPrompt` (string, optional): Custom translation prompt
  - `rateLimitPerMinute` (integer, default: `60`): Request rate limit
- **Example:**
  ```json
  "openAI": {
    "model": "gpt-4o-mini",
    "customSystemPrompt": null,
    "rateLimitPerMinute": 60
  }
  ```

#### `translation.aiProviders.claude`

Configuration for Anthropic Claude provider.

- **Type:** `object`
- **Properties:**
  - `model` (string, default: `"claude-3-5-sonnet-20241022"`): Model to use
  - `customSystemPrompt` (string, optional): Custom translation prompt
  - `rateLimitPerMinute` (integer, default: `50`): Request rate limit
- **Example:**
  ```json
  "claude": {
    "model": "claude-3-5-sonnet-20241022",
    "customSystemPrompt": null,
    "rateLimitPerMinute": 50
  }
  ```

#### `translation.aiProviders.azureOpenAI`

Configuration for Azure OpenAI provider.

- **Type:** `object`
- **Properties:**
  - `endpoint` (string, required): Azure OpenAI endpoint URL
  - `deploymentName` (string, required): Deployment name from Azure Portal
  - `customSystemPrompt` (string, optional): Custom translation prompt
  - `rateLimitPerMinute` (integer, default: `60`): Request rate limit
- **Example:**
  ```json
  "azureOpenAI": {
    "endpoint": "https://your-resource.openai.azure.com",
    "deploymentName": "gpt-4",
    "customSystemPrompt": null,
    "rateLimitPerMinute": 60
  }
  ```

#### `translation.aiProviders.azureTranslator`

Configuration for Azure AI Translator provider.

- **Type:** `object`
- **Properties:**
  - `region` (string, optional): Azure resource region (e.g., "eastus", "westus"). Required for multi-service or regional resources, optional for global resources.
  - `endpoint` (string, optional): Custom endpoint URL. Default: "https://api.cognitive.microsofttranslator.com"
  - `rateLimitPerMinute` (integer, default: `100`): Request rate limit
- **Example:**
  ```json
  "azureTranslator": {
    "region": "eastus",
    "endpoint": "https://api.cognitive.microsofttranslator.com",
    "rateLimitPerMinute": 100
  }
  ```

---

### Code Scanning Settings

Configuration for the code scanning feature that detects localization key usage in source files.

#### `scanning.resourceClassNames`

- **Type:** `array of strings`
- **Default:** `["Resources", "Strings", "AppResources"]`
- **Description:** Resource class names to detect in code. These are the class names whose property accesses will be detected as localization key references.
- **Detected patterns:**
  - C#: `Resources.KeyName`, `AppResources.WelcomeMessage`
  - XAML: `{x:Static res:Strings.PageTitle}`
  - Razor: `@Resources.ErrorMessage`
- **Example:**
  ```json
  "resourceClassNames": [
    "Resources",
    "Strings",
    "AppResources",
    "Loc"
  ]
  ```

#### `scanning.localizationMethods`

- **Type:** `array of strings`
- **Default:** `["GetString", "GetLocalizedString", "Translate", "L", "T"]`
- **Description:** Localization method names to detect. These are method names that accept localization keys as string parameters.
- **Detected patterns:**
  - `GetString("KeyName")`
  - `Translate("WelcomeMessage")`
  - `L("ErrorText")`
  - `T("ButtonLabel")`
- **Example:**
  ```json
  "localizationMethods": [
    "GetString",
    "Translate",
    "L",
    "T",
    "GetLocalizedString",
    "Loc"
  ]
  ```

---

## Complete Example

```json
{
  "defaultLanguageCode": "en",
  "translation": {
    "defaultProvider": "deepl",
    "maxRetries": 5,
    "timeoutSeconds": 60,
    "batchSize": 15,
    "useSecureCredentialStore": true,
    "apiKeys": {
      "google": "",
      "deepL": "",
      "libreTranslate": "",
      "azureTranslator": "",
      "openAI": "",
      "claude": "",
      "azureOpenAI": ""
    },
    "aiProviders": {
      "ollama": {
        "apiUrl": "http://localhost:11434",
        "model": "llama3.2",
        "customSystemPrompt": null,
        "rateLimitPerMinute": 10
      },
      "openAI": {
        "model": "gpt-4o-mini",
        "customSystemPrompt": null,
        "rateLimitPerMinute": 60
      },
      "claude": {
        "model": "claude-3-5-sonnet-20241022",
        "customSystemPrompt": null,
        "rateLimitPerMinute": 50
      },
      "azureOpenAI": {
        "endpoint": "https://your-resource.openai.azure.com",
        "deploymentName": "gpt-4",
        "customSystemPrompt": null,
        "rateLimitPerMinute": 60
      },
      "azureTranslator": {
        "region": "eastus",
        "endpoint": "https://api.cognitive.microsofttranslator.com",
        "rateLimitPerMinute": 100
      }
    }
  },
  "scanning": {
    "resourceClassNames": [
      "Resources",
      "Strings",
      "AppResources",
      "Loc"
    ],
    "localizationMethods": [
      "GetString",
      "GetLocalizedString",
      "Translate",
      "L",
      "T",
      "Localize"
    ]
  }
}
```

---

## Security Best Practices

### API Key Management

1. **Use Environment Variables (Recommended)**
   ```bash
   export LRM_GOOGLE_API_KEY="your-key-here"
   export LRM_DEEPL_API_KEY="your-key-here"
   export LRM_OPENAI_API_KEY="your-key-here"
   export LRM_CLAUDE_API_KEY="your-key-here"
   export LRM_AZUREOPENAI_API_KEY="your-key-here"
   ```

2. **Use Secure Credential Store**
   ```bash
   lrm config set-api-key google "your-key-here"
   lrm config set-api-key deepl "your-key-here"
   lrm config set-api-key openai "your-key-here"
   lrm config set-api-key claude "your-key-here"
   lrm config set-api-key azureopenai "your-key-here"
   ```

3. **Avoid Storing in Configuration File**
   - If you must store keys in `lrm.json`, ensure it's in `.gitignore`
   - Never commit API keys to version control

### .gitignore Entry

Add this to your `.gitignore`:

```
lrm.json
```

While still keeping the sample file tracked:

```
lrm.json
!lrm-sample.json
```

---

## Command-Line Overrides

Most configuration options can be overridden via command-line arguments. For example:

```bash
# Override translation provider
lrm translate --only-missing --provider deepl --target-language es

# Use AI provider
lrm translate --only-missing --provider openai --target-language es

# Use local Ollama
lrm translate --only-missing --provider ollama --target-language es

# Override scanning configuration
lrm scan --source-path ./src --resource-classes "Loc,Resources"
```

Command-line arguments always take priority over configuration file settings.

---

## Translation Provider Notes

### Traditional Machine Translation Providers
- **Google**: Requires API key, excellent for general-purpose translations
- **DeepL**: Requires API key, highest quality for European languages
- **LibreTranslate**: Open source, can run self-hosted, API key optional
- **Azure AI Translator**: Requires Azure subscription, enterprise-grade, supports 130+ languages, excellent for production workloads

### AI-Powered Providers
- **Ollama**: No API key needed, runs locally, completely private
- **OpenAI**: Requires API key, excellent quality, context-aware
- **Claude**: Requires API key, excellent for nuanced translations
- **Azure OpenAI**: Requires Azure subscription, enterprise-grade security

See [docs/TRANSLATION.md](docs/TRANSLATION.md) for detailed setup instructions for each provider.
