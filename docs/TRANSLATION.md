# Translation Features

LocalizationManager supports automatic translation of resource keys using multiple translation providers.

## Table of Contents

- [Overview](#overview)
- [Supported Providers](#supported-providers)
- [Configuration](#configuration)
- [Commands](#commands)
- [Examples](#examples)
- [Translation Cache](#translation-cache)
- [Troubleshooting](#troubleshooting)

## Overview

The translation feature allows you to automatically translate resource keys from a source language to one or more target languages. Translations are cached locally to reduce API costs and improve performance.

### Key Features

- **Multiple Providers**: Support for Google Cloud Translation, DeepL, LibreTranslate, Azure AI Translator, and AI services (OpenAI, Claude, Ollama, Azure OpenAI)
- **Flexible Configuration**: 3-tier priority for API keys (environment variables → secure store → config file)
- **Smart Caching**: SQLite-based translation cache with 30-day expiration
- **Rate Limiting**: Built-in rate limiting to protect against API quota exhaustion
- **Batch Processing**: Efficient batch translation to minimize API calls
- **Dry Run Mode**: Preview translations before applying them
- **Pattern Matching**: Translate specific keys using wildcard patterns
- **Only Missing**: Option to translate only missing or empty values

## Supported Providers

### Google Cloud Translation

**Provider name**: `google`

**Features**:
- Supports 100+ languages
- High-quality neural machine translation
- Auto-detect source language
- Batch translation support

**Requirements**:
- Google Cloud Platform account
- Translation API enabled
- API key or service account credentials

**Rate limits**: Configurable (default: 100 requests/minute)

### DeepL

**Provider name**: `deepl`

**Features**:
- Highest quality translations
- Supports 30+ languages
- Preserves formatting
- Glossary support (coming soon)

**Requirements**:
- DeepL API account (Free or Pro)
- API authentication key

**Rate limits**: Configurable (default: 20 requests/minute for free tier)

### LibreTranslate

**Provider name**: `libretranslate`

**Features**:
- Open-source translation
- Self-hostable
- No API key required for public instances
- Supports 30+ languages

**Requirements**:
- None for public instances
- API key for private instances

**Rate limits**: Configurable (default: 10 requests/minute)

### Azure AI Translator

**Provider name**: `azuretranslator`

**Features**:
- High-quality neural machine translation from Microsoft
- Supports 100+ languages
- Auto-detect source language
- Batch translation support (up to 100 text elements)
- Enterprise-grade security and compliance
- Regional deployment options
- Excellent performance and reliability

**Requirements**:
- Azure subscription
- Azure Cognitive Services - Translator resource
- Subscription key (Ocp-Apim-Subscription-Key)
- Optional: Region specification for multi-service resources

**Rate limits**: Configurable (default: 100 requests/minute, varies by tier)

**Pricing**:
- Free tier: Up to 2M characters/month
- Pay-as-you-go: $10 per million characters
- Volume discounts available

**Setup**:
```bash
# Create Translator resource in Azure Portal
# Navigate to: Azure Portal → Create Resource → Translator

# Get your API key and region
# In Azure Portal: Translator resource → Keys and Endpoint

# Set environment variable
export LRM_AZURETRANSLATOR_API_KEY="your-subscription-key"

# Or configure in lrm.json
lrm config set-api-key --provider azuretranslator --key "your-subscription-key"
```

### Ollama (Local LLM)

**Provider name**: `ollama`

**Features**:
- Runs locally on your machine - no API key needed
- Supports various open-source models (Llama, Mistral, Phi, etc.)
- Complete privacy - data never leaves your machine
- Customizable models and prompts
- Supports 100+ languages (model-dependent)

**Requirements**:
- Ollama installed and running (https://ollama.ai)
- At least one model downloaded (e.g., `ollama pull llama3.2`)
- Default endpoint: http://localhost:11434

**Rate limits**: Configurable (default: 10 requests/minute)

**Setup**:
```bash
# Install Ollama
# Visit https://ollama.ai for installation instructions

# Pull a model
ollama pull llama3.2

# Ollama is now ready to use with LocalizationManager
lrm translate --only-missing --provider ollama
```

### OpenAI GPT

**Provider name**: `openai`

**Features**:
- High-quality translations with GPT-4 and GPT-3.5-turbo
- Context-aware translations
- Supports 100+ languages
- Customizable models and prompts
- Excellent handling of technical terminology

**Requirements**:
- OpenAI API account
- API key from https://platform.openai.com/api-keys

**Rate limits**: Configurable (default: 60 requests/minute)

**Cost**: Pay-per-use based on tokens. GPT-4o-mini is recommended for cost-effective translations.

### Anthropic Claude

**Provider name**: `claude`

**Features**:
- Excellent translation quality
- Strong understanding of context and nuance
- Supports 100+ languages
- Customizable models and prompts
- Good at preserving tone and style

**Requirements**:
- Anthropic API account
- API key from https://console.anthropic.com/

**Rate limits**: Configurable (default: 50 requests/minute)

**Cost**: Pay-per-use based on tokens. Claude 3.5 Sonnet offers excellent quality/cost balance.

### Azure OpenAI

**Provider name**: `azureopenai`

**Features**:
- Enterprise-grade security and compliance
- Same GPT models as OpenAI
- Region-specific deployments
- Integration with Azure ecosystem
- Customizable models and prompts

**Requirements**:
- Azure subscription
- Azure OpenAI resource created
- Deployment created in Azure Portal
- API key and endpoint URL

**Rate limits**: Configurable (default: 60 requests/minute)

**Setup**:
1. Create Azure OpenAI resource in Azure Portal
2. Deploy a model (e.g., gpt-4, gpt-35-turbo)
3. Get the endpoint URL and API key
4. Configure in lrm.json or environment variables

## Configuration

### API Key Priority

API keys are resolved using a 3-tier priority system:

1. **Environment Variables** (highest priority)
   - `LRM_GOOGLE_API_KEY`
   - `LRM_DEEPL_API_KEY`
   - `LRM_LIBRETRANSLATE_API_KEY`
   - `LRM_AZURETRANSLATOR_API_KEY`
   - `LRM_OPENAI_API_KEY`
   - `LRM_CLAUDE_API_KEY`
   - `LRM_AZUREOPENAI_API_KEY`

2. **Secure Credential Store** (optional)
   - Encrypted storage in user profile
   - Use `lrm config set-api-key` to store
   - Enable with `UseSecureCredentialStore: true` in config

3. **Configuration File** (lowest priority)
   - Plain text in `lrm.json`
   - ⚠️ **WARNING**: Do not commit API keys to version control!

### Environment Variables (Recommended for CI/CD)

**Linux/macOS**:
```bash
export LRM_GOOGLE_API_KEY="your-google-api-key"
export LRM_DEEPL_API_KEY="your-deepl-api-key"
export LRM_LIBRETRANSLATE_API_KEY="your-libretranslate-api-key"
export LRM_AZURETRANSLATOR_API_KEY="your-azure-translator-key"
export LRM_OPENAI_API_KEY="your-openai-api-key"
export LRM_CLAUDE_API_KEY="your-claude-api-key"
export LRM_AZUREOPENAI_API_KEY="your-azure-openai-api-key"
```

**Windows PowerShell**:
```powershell
$env:LRM_GOOGLE_API_KEY="your-google-api-key"
$env:LRM_DEEPL_API_KEY="your-deepl-api-key"
$env:LRM_LIBRETRANSLATE_API_KEY="your-libretranslate-api-key"
$env:LRM_AZURETRANSLATOR_API_KEY="your-azure-translator-key"
$env:LRM_OPENAI_API_KEY="your-openai-api-key"
$env:LRM_CLAUDE_API_KEY="your-claude-api-key"
$env:LRM_AZUREOPENAI_API_KEY="your-azure-openai-api-key"
```

**Windows CMD**:
```cmd
set LRM_GOOGLE_API_KEY=your-google-api-key
set LRM_DEEPL_API_KEY=your-deepl-api-key
set LRM_LIBRETRANSLATE_API_KEY=your-libretranslate-api-key
set LRM_AZURETRANSLATOR_API_KEY=your-azure-translator-key
set LRM_OPENAI_API_KEY=your-openai-api-key
set LRM_CLAUDE_API_KEY=your-claude-api-key
set LRM_AZUREOPENAI_API_KEY=your-azure-openai-api-key
```

### Configuration File

Create or update `lrm.json` in your resource directory:

```json
{
  "Translation": {
    "DefaultProvider": "google",
    "MaxRetries": 3,
    "TimeoutSeconds": 30,
    "BatchSize": 10,
    "UseSecureCredentialStore": false,
    "ApiKeys": {
      "Google": "your-google-api-key",
      "DeepL": "your-deepl-api-key",
      "LibreTranslate": "your-libretranslate-api-key",
      "AzureTranslator": "your-azure-translator-key",
      "OpenAI": "your-openai-api-key",
      "Claude": "your-claude-api-key",
      "AzureOpenAI": "your-azure-openai-api-key"
    },
    "AIProviders": {
      "Ollama": {
        "ApiUrl": "http://localhost:11434",
        "Model": "llama3.2",
        "CustomSystemPrompt": null,
        "RateLimitPerMinute": 10
      },
      "OpenAI": {
        "Model": "gpt-4o-mini",
        "CustomSystemPrompt": null,
        "RateLimitPerMinute": 60
      },
      "Claude": {
        "Model": "claude-3-5-sonnet-20241022",
        "CustomSystemPrompt": null,
        "RateLimitPerMinute": 50
      },
      "AzureOpenAI": {
        "Endpoint": "https://your-resource.openai.azure.com",
        "DeploymentName": "gpt-4",
        "CustomSystemPrompt": null,
        "RateLimitPerMinute": 60
      },
      "AzureTranslator": {
        "Region": "eastus",
        "Endpoint": "https://api.cognitive.microsofttranslator.com",
        "RateLimitPerMinute": 100
      }
    }
  }
}
```

**AI Provider Settings Explained**:
- `ApiUrl` (Ollama only): The endpoint URL for your Ollama instance
- `Model`: The model to use for translations (can be customized per provider)
- `CustomSystemPrompt`: Override the default translation prompt with your own
- `RateLimitPerMinute`: Maximum requests per minute to avoid rate limiting
- `Endpoint` (Azure only): Your Azure OpenAI endpoint URL
- `DeploymentName` (Azure only): The deployment name in Azure Portal

⚠️ **IMPORTANT**: If you add API keys to `lrm.json`, add the file to `.gitignore` to prevent committing secrets to version control!

### Secure Credential Store (Optional)

Store API keys in an encrypted credential store:

```bash
# Store an API key
lrm config set-api-key --provider google --key "your-api-key"

# Check where an API key is configured
lrm config get-api-key --provider google

# Delete an API key from secure store
lrm config delete-api-key --provider google

# List all providers and their configuration status
lrm config list-providers
```

The secure credential store uses AES-256 encryption with machine-specific keys. Keys are stored in:
- **Linux**: `~/.local/share/LocalizationManager/credentials.json`
- **Windows**: `%LOCALAPPDATA%\LocalizationManager\credentials.json`

To enable the secure credential store, set `UseSecureCredentialStore: true` in your `lrm.json`.

## Commands

### Translate Command

```bash
lrm translate [KEY] [OPTIONS]
```

**Arguments**:
- `KEY`: Optional key pattern with wildcard support (e.g., `Error*`, `Button_*`)
  - **Required** unless `--only-missing` is used (safety feature)

**Options**:
- `--provider <PROVIDER>`: Translation provider (google, deepl, libretranslate, azuretranslator, ollama, openai, claude, azureopenai)
  - Default: From config or `google`
- `--source-language <LANG>`: Source language code (e.g., `en`, `fr`, or `default`)
  - Default: Uses default language file (auto-detect)
  - The default language file (without language code suffix) is always used as source unless explicitly specified
- `--target-languages <LANGS>`: Comma-separated target languages (e.g., `fr,de,es`)
  - Default: All non-default languages found in resource files
- `--only-missing`: Only translate keys with missing or empty values (safe)
- `--overwrite`: Allow overwriting existing translations when using KEY pattern
- `--dry-run`: Preview translations without saving
- `--no-cache`: Disable translation cache
- `--batch-size <SIZE>`: Batch size for processing (default: 10)
- `-p, --path <PATH>`: Path to Resources folder
- `--config-file <PATH>`: Path to configuration file
- `-f, --format <FORMAT>`: Output format (table, json, simple)

### Config Commands

#### Set API Key

```bash
lrm config set-api-key --provider <PROVIDER> --key <KEY>
```

Store an API key in the secure credential store.

**Options**:
- `-p, --provider <PROVIDER>`: Provider name (google, deepl, libretranslate, openai, claude, azureopenai)
- `-k, --key <KEY>`: API key to store

#### Get API Key

```bash
lrm config get-api-key --provider <PROVIDER>
```

Check where an API key is configured from.

**Options**:
- `--provider <PROVIDER>`: Provider name

#### Delete API Key

```bash
lrm config delete-api-key --provider <PROVIDER>
```

Delete an API key from the secure credential store.

**Options**:
- `-p, --provider <PROVIDER>`: Provider name

#### List Providers

```bash
lrm config list-providers
```

List all translation providers and their configuration status.

## Translation Safety

LocalizationManager includes two-level safety protection to prevent accidental overwrites of existing translations:

### Level 1: Execution Gate

Translation requires explicit intent and will only execute when:
- `--only-missing` flag is provided (translates only missing/empty keys), OR
- A KEY pattern is provided (translates specific keys)

Without either, the command will show an error:

```bash
# This will show an error
lrm translate --target-languages es

# These are valid
lrm translate --only-missing --target-languages es
lrm translate Welcome* --target-languages es
```

### Level 2: Overwrite Protection

When using a KEY pattern that matches existing translations:
- You'll be prompted for confirmation before overwriting
- Use `--overwrite` flag to skip the confirmation prompt
- Use `--only-missing` to safely skip existing translations

```bash
# Will prompt if Welcome* keys already have translations
lrm translate Welcome* --target-languages es

# Skip prompt with --overwrite
lrm translate Welcome* --target-languages es --overwrite

# Safe: only translate missing values
lrm translate Welcome* --target-languages es --only-missing
```

### Safe Usage Patterns

**Recommended for new translations:**
```bash
# Translate only missing keys across all languages
lrm translate --only-missing
```

**Recommended for specific updates:**
```bash
# Translate specific new feature keys
lrm translate NewFeature* --target-languages es,fr
```

**Use with caution:**
```bash
# Retranslate all keys (overwrites with confirmation)
lrm translate "*" --target-languages es

# Force retranslate without confirmation
lrm translate "*" --target-languages es --overwrite
```

## Examples

### Basic Translation

Translate only missing keys to all target languages:
```bash
lrm translate --only-missing
```

### Translate Specific Keys

Translate keys matching a pattern:
```bash
# Translate all error messages
lrm translate "Error*"

# Translate all button labels
lrm translate "Button_*"
```

### Specify Target Languages

Translate to specific languages:
```bash
lrm translate --only-missing --target-languages fr,de,es
```

### Use Specific Provider

Use DeepL for highest quality:
```bash
lrm translate --only-missing --provider deepl
```

Use Ollama for local, private translations:
```bash
lrm translate --only-missing --provider ollama
```

Use OpenAI GPT for high-quality AI translations:
```bash
lrm translate --only-missing --provider openai
```

Use Claude for nuanced, context-aware translations:
```bash
lrm translate --only-missing --provider claude
```

Use Azure OpenAI for enterprise deployments:
```bash
lrm translate --only-missing --provider azureopenai
```

### Only Translate Missing Values

Fill in missing translations:
```bash
lrm translate --only-missing
```

### Preview Translations (Dry Run)

Preview without saving:
```bash
lrm translate --dry-run
```

### Specify Source Language

Explicitly set source language:
```bash
lrm translate --source-language en --target-languages fr,de
```

### Batch Processing

Process in larger batches:
```bash
lrm translate --batch-size 20
```

### Disable Cache

Force fresh translations:
```bash
lrm translate --no-cache
```

### Combined Example

```bash
lrm translate "Welcome*" \
  --provider deepl \
  --source-language en \
  --target-languages fr,de,es,it \
  --only-missing \
  --dry-run
```

## Translation Cache

The translation cache stores previously translated text to reduce API costs and improve performance.

### Cache Location

- **Linux**: `~/.local/share/LocalizationManager/translation_cache.db`
- **Windows**: `%LOCALAPPDATA%\LocalizationManager\translation_cache.db`

### Cache Behavior

- **Expiration**: 30 days
- **Format**: SQLite database
- **Key**: SHA-256 hash of (provider + source text + source language + target language)
- **Thread-safe**: Multiple instances can safely access the cache

### Cache Management

The cache is automatically managed:
- Old entries (>30 days) are removed on startup
- Cache can be disabled with `--no-cache` flag
- Cache is provider-specific (Google translations won't be used for DeepL requests)

### Manual Cache Management

To clear the cache manually, delete the cache file:

**Linux**:
```bash
rm ~/.local/share/LocalizationManager/translation_cache.db
```

**Windows PowerShell**:
```powershell
Remove-Item "$env:LOCALAPPDATA\LocalizationManager\translation_cache.db"
```

## Troubleshooting

### "Provider not configured" Error

**Problem**: No API key found for the provider.

**Solution**:
1. Check which providers are configured:
   ```bash
   lrm config list-providers
   ```

2. Set API key using one of these methods:
   ```bash
   # Environment variable (recommended)
   export LRM_GOOGLE_API_KEY="your-key"

   # Secure store
   lrm config set-api-key --provider google --key "your-key"

   # Config file
   # Add to lrm.json (see Configuration section)
   ```

### "Rate limit exceeded" Error

**Problem**: Too many requests to the translation API.

**Solution**:
1. Wait for the rate limit window to reset
2. Reduce batch size: `--batch-size 5`
3. Use caching (enabled by default)
4. Upgrade your API plan for higher limits

### "Authentication failed" Error

**Problem**: Invalid or expired API key.

**Solution**:
1. Verify your API key is correct
2. Check API key status in provider dashboard
3. Regenerate API key if needed
4. Update the key in your configuration

### "Quota exceeded" Error

**Problem**: API usage quota exhausted.

**Solution**:
1. Check your API usage in provider dashboard
2. Wait for quota to reset (usually monthly)
3. Upgrade to a higher tier plan
4. Use caching to reduce API calls

### "Target language not supported" Error

**Problem**: Provider doesn't support the target language.

**Solution**:
1. Check provider's supported languages
2. Use a different provider
3. Remove unsupported languages from `--target-languages`

### Translations are Incorrect

**Problem**: Translation quality is poor.

**Solution**:
1. Try a different provider (DeepL often has highest quality)
2. Provide source language explicitly: `--source-language en`
3. Add context to resource keys (comments help future manual review)
4. Review and manually correct critical translations

### Cache Not Working

**Problem**: Same translations are being fetched repeatedly.

**Solution**:
1. Ensure cache is enabled (don't use `--no-cache`)
2. Check cache file exists and is writable
3. Verify file permissions on cache directory
4. Check disk space

### Cross-Platform Issues

**Problem**: Commands work on Linux but not Windows (or vice versa).

**Solution**:
1. Check file paths use correct separators
2. Verify environment variables are set correctly for your OS
3. Ensure cache directory is accessible
4. Check file permissions (Linux) or security settings (Windows)

## Best Practices

1. **Use Environment Variables for CI/CD**: Never commit API keys to version control
2. **Start with Dry Run**: Always test with `--dry-run` first
3. **Translate in Batches**: Use `--only-missing` for incremental updates
4. **Review Translations**: Machine translation is not perfect - review critical strings
5. **Use DeepL for Quality**: When quality matters, use DeepL
6. **Cache Aggressively**: Let the cache save you money
7. **Set Rate Limits**: Configure appropriate rate limits in config
8. **Monitor Costs**: Track API usage in provider dashboards
9. **Backup First**: Translation commands create backups automatically
10. **Test Thoroughly**: Test translations in your application UI

## Advanced Configuration

### Custom Rate Limits

Configure per-provider rate limits in `lrm.json`:

```json
{
  "Translation": {
    "RateLimits": {
      "Google": {
        "RequestsPerMinute": 100,
        "BurstSize": 10
      },
      "DeepL": {
        "RequestsPerMinute": 20,
        "BurstSize": 5
      }
    }
  }
}
```

### Custom LibreTranslate Instance

Use a self-hosted LibreTranslate instance:

```json
{
  "Translation": {
    "LibreTranslate": {
      "ApiUrl": "https://your-instance.com",
      "ApiKey": "your-api-key"
    }
  }
}
```

### Google Cloud Authentication

For Google Cloud Translation, you can use service account credentials:

1. Download service account JSON key from Google Cloud Console
2. Set environment variable:
   ```bash
   export GOOGLE_APPLICATION_CREDENTIALS="/path/to/service-account.json"
   ```

## Security Considerations

1. **Never Commit API Keys**: Always add `lrm.json` to `.gitignore` if it contains API keys
2. **Use Environment Variables**: Preferred method for production/CI environments
3. **Secure Credential Store**: Optional encrypted storage for local development
4. **Rotate Keys Regularly**: Periodically regenerate API keys
5. **Limit Key Permissions**: Use API keys with minimal required permissions
6. **Monitor Usage**: Set up alerts for unusual API usage
7. **File Permissions**: Ensure cache and credentials files have appropriate permissions

## Support

For issues, feature requests, or questions:
- GitHub Issues: https://github.com/nprotopapas/LocalizationManager/issues
- Documentation: https://github.com/nprotopapas/LocalizationManager/docs

## License

LocalizationManager is licensed under the MIT License. See LICENSE file for details.
