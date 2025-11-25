using LocalizationManager.Core.Configuration;
using System.Text;
using System.Text.Json;

namespace LocalizationManager.Services;

/// <summary>
/// Service for generating schema-enriched configuration with inline documentation.
/// </summary>
public class ConfigurationSchemaService
{
    /// <summary>
    /// Generates a schema-enriched configuration string that includes:
    /// - All available configuration options (from schema)
    /// - Current user values (from actual config)
    /// - Inline comments with descriptions and defaults
    /// - Helpful documentation for discovering options
    /// </summary>
    /// <param name="userConfig">The user's current configuration (can be null for new configs)</param>
    /// <returns>A formatted JSON string with inline comments</returns>
    public string GenerateSchemaEnrichedConfig(ConfigurationModel? userConfig)
    {
        var sb = new StringBuilder();

        sb.AppendLine("{");
        sb.AppendLine("  // ================================================================");
        sb.AppendLine("  // LocalizationManager Configuration");
        sb.AppendLine("  // This file shows all available options with inline documentation.");
        sb.AppendLine("  // Uncomment and modify values as needed.");
        sb.AppendLine("  // ================================================================");
        sb.AppendLine();

        // Default Language Code
        AppendDefaultLanguageCode(sb, userConfig);

        // Translation Configuration
        AppendTranslationConfiguration(sb, userConfig);

        // Scanning Configuration
        AppendScanningConfiguration(sb, userConfig);

        // Validation Configuration
        AppendValidationConfiguration(sb, userConfig);

        // Web Configuration
        AppendWebConfiguration(sb, userConfig);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private void AppendDefaultLanguageCode(StringBuilder sb, ConfigurationModel? config)
    {
        sb.AppendLine("  // ----------------------------------------------------------------");
        sb.AppendLine("  // Default Language Code");
        sb.AppendLine("  // The language code for the default resource file (e.g., \"en\", \"fr\").");
        sb.AppendLine("  // Used for display output and as the source language for translations.");
        sb.AppendLine("  // If not set, displays \"default\" and translations use auto-detect.");
        sb.AppendLine("  // ----------------------------------------------------------------");

        if (config?.DefaultLanguageCode != null)
        {
            sb.AppendLine($"  \"defaultLanguageCode\": {JsonSerializer.Serialize(config.DefaultLanguageCode)},");
        }
        else
        {
            sb.AppendLine("  // \"defaultLanguageCode\": \"en\",");
        }
        sb.AppendLine();
    }

    private void AppendTranslationConfiguration(StringBuilder sb, ConfigurationModel? config)
    {
        var translation = config?.Translation;

        sb.AppendLine("  // ----------------------------------------------------------------");
        sb.AppendLine("  // Translation Configuration");
        sb.AppendLine("  // Settings for machine translation features.");
        sb.AppendLine("  // ----------------------------------------------------------------");

        if (translation != null)
        {
            sb.AppendLine("  \"translation\": {");

            // Default Provider
            sb.AppendLine("    // Supported providers: \"google\", \"deepl\", \"libretranslate\", \"ollama\", \"openai\", \"claude\", \"azureopenai\", \"azuretranslator\"");
            sb.AppendLine($"    \"defaultProvider\": {JsonSerializer.Serialize(translation.DefaultProvider)},");
            sb.AppendLine();

            // Max Retries
            sb.AppendLine("    // Maximum retry attempts for failed translation requests (default: 3)");
            sb.AppendLine($"    \"maxRetries\": {translation.MaxRetries},");
            sb.AppendLine();

            // Timeout
            sb.AppendLine("    // Timeout in seconds for translation requests (default: 30)");
            sb.AppendLine($"    \"timeoutSeconds\": {translation.TimeoutSeconds},");
            sb.AppendLine();

            // Batch Size
            sb.AppendLine("    // Number of keys to translate in a single batch (default: 10)");
            sb.AppendLine($"    \"batchSize\": {translation.BatchSize},");
            sb.AppendLine();

            // Use Secure Credential Store
            sb.AppendLine("    // Use encrypted secure credential store for API keys (default: false)");
            sb.AppendLine($"    \"useSecureCredentialStore\": {translation.UseSecureCredentialStore.ToString().ToLower()},");
            sb.AppendLine();

            // API Keys
            AppendApiKeys(sb, translation.ApiKeys);

            // AI Providers
            AppendAIProviders(sb, translation.AIProviders);

            sb.AppendLine("  },");
        }
        else
        {
            // When translation is null, show full expanded schema in comments
            sb.AppendLine("  // \"translation\": {");
            sb.AppendLine("  //   // Supported providers: \"google\", \"deepl\", \"libretranslate\", \"ollama\", \"openai\", \"claude\", \"azureopenai\", \"azuretranslator\"");
            sb.AppendLine("  //   \"defaultProvider\": \"google\",");
            sb.AppendLine("  //   // Default: google");
            sb.AppendLine("  //");
            sb.AppendLine("  //   \"maxRetries\": 3,");
            sb.AppendLine("  //   // Maximum retry attempts for failed translation requests");
            sb.AppendLine("  //   // Default: 3");
            sb.AppendLine("  //");
            sb.AppendLine("  //   \"timeoutSeconds\": 30,");
            sb.AppendLine("  //   // Timeout in seconds for translation requests");
            sb.AppendLine("  //   // Default: 30");
            sb.AppendLine("  //");
            sb.AppendLine("  //   \"batchSize\": 10,");
            sb.AppendLine("  //   // Number of keys to translate in a single batch");
            sb.AppendLine("  //   // Default: 10");
            sb.AppendLine("  //");
            sb.AppendLine("  //   \"useSecureCredentialStore\": false,");
            sb.AppendLine("  //   // Use encrypted secure credential store for API keys");
            sb.AppendLine("  //   // Default: false");
            sb.AppendLine("  //");

            // Inline expanded API Keys with proper comment prefixes
            AppendApiKeysCommented(sb);

            // Inline expanded AI Providers with proper comment prefixes
            AppendAIProvidersCommented(sb);

            sb.AppendLine("  // },");
        }
        sb.AppendLine();
    }

    private void AppendApiKeysCommented(StringBuilder sb)
    {
        sb.AppendLine("  //   // ---- API Keys ----");
        sb.AppendLine("  //   // WARNING: Do not commit API keys to version control!");
        sb.AppendLine("  //   // Priority: 1) Environment variables, 2) Secure store, 3) These values");
        sb.AppendLine("  //   // Get your API keys from the provider's website (see comments below)");
        sb.AppendLine("  //   // \"apiKeys\": {");
        sb.AppendLine("  //   //   // Google Cloud Translation API");
        sb.AppendLine("  //   //   \"google\": \"your-google-api-key\",");
        sb.AppendLine("  //   //   // Env: LRM_GOOGLE_API_KEY");
        sb.AppendLine("  //   //   // Get key: https://cloud.google.com/translate/docs/setup");
        sb.AppendLine("  //   //");
        sb.AppendLine("  //   //   // DeepL Translation API");
        sb.AppendLine("  //   //   \"deepL\": \"your-deepl-api-key\",");
        sb.AppendLine("  //   //   // Env: LRM_DEEPL_API_KEY");
        sb.AppendLine("  //   //   // Get key: https://www.deepl.com/pro-api");
        sb.AppendLine("  //   //");
        sb.AppendLine("  //   //   // LibreTranslate (optional for public instances)");
        sb.AppendLine("  //   //   \"libreTranslate\": \"your-libretranslate-api-key\",");
        sb.AppendLine("  //   //   // Env: LRM_LIBRETRANSLATE_API_KEY");
        sb.AppendLine("  //   //   // Learn more: https://libretranslate.com/");
        sb.AppendLine("  //   //");
        sb.AppendLine("  //   //   // OpenAI GPT models");
        sb.AppendLine("  //   //   \"openAI\": \"sk-...\",");
        sb.AppendLine("  //   //   // Env: LRM_OPENAI_API_KEY");
        sb.AppendLine("  //   //   // Get key: https://platform.openai.com/api-keys");
        sb.AppendLine("  //   //");
        sb.AppendLine("  //   //   // Anthropic Claude");
        sb.AppendLine("  //   //   \"claude\": \"sk-ant-...\",");
        sb.AppendLine("  //   //   // Env: LRM_CLAUDE_API_KEY");
        sb.AppendLine("  //   //   // Get key: https://console.anthropic.com/");
        sb.AppendLine("  //   //");
        sb.AppendLine("  //   //   // Azure OpenAI Service");
        sb.AppendLine("  //   //   \"azureOpenAI\": \"your-azure-openai-key\",");
        sb.AppendLine("  //   //   // Env: LRM_AZUREOPENAI_API_KEY");
        sb.AppendLine("  //   //   // Get from Azure Portal");
        sb.AppendLine("  //   //");
        sb.AppendLine("  //   //   // Azure AI Translator");
        sb.AppendLine("  //   //   \"azureTranslator\": \"your-azure-translator-key\"");
        sb.AppendLine("  //   //   // Env: LRM_AZURETRANSLATOR_API_KEY");
        sb.AppendLine("  //   //   // Get from Azure Portal (Cognitive Services - Translator)");
        sb.AppendLine("  //   // },");
        sb.AppendLine("  //");
    }

    private void AppendAIProvidersCommented(StringBuilder sb)
    {
        sb.AppendLine("  //   // ---- AI Provider Settings ----");
        sb.AppendLine("  //   // Customize models, prompts, and endpoints for AI translation providers.");
        sb.AppendLine("  //   // Uncomment and configure the providers you want to use.");
        sb.AppendLine("  //   // \"aiProviders\": {");
        sb.AppendLine("  //   //   // Ollama - Local LLM inference (free)");
        sb.AppendLine("  //   //   \"ollama\": {");
        sb.AppendLine("  //   //     \"apiUrl\": \"http://localhost:11434\",");
        sb.AppendLine("  //   //     \"model\": \"llama3.2\",  // Options: llama3.2, mistral, phi, qwen");
        sb.AppendLine("  //   //     \"customSystemPrompt\": \"...\",  // Optional");
        sb.AppendLine("  //   //     \"rateLimitPerMinute\": 10");
        sb.AppendLine("  //   //   },");
        sb.AppendLine("  //   //");
        sb.AppendLine("  //   //   // OpenAI GPT models");
        sb.AppendLine("  //   //   \"openAI\": {");
        sb.AppendLine("  //   //     \"model\": \"gpt-4o-mini\",  // Recommended: fast and cost-effective");
        sb.AppendLine("  //   //     \"customSystemPrompt\": \"...\",  // Optional");
        sb.AppendLine("  //   //     \"rateLimitPerMinute\": 60");
        sb.AppendLine("  //   //   },");
        sb.AppendLine("  //   //");
        sb.AppendLine("  //   //   // Anthropic Claude models");
        sb.AppendLine("  //   //   \"claude\": {");
        sb.AppendLine("  //   //     \"model\": \"claude-3-5-sonnet-20241022\",  // Recommended: excellent quality");
        sb.AppendLine("  //   //     \"customSystemPrompt\": \"...\",  // Optional");
        sb.AppendLine("  //   //     \"rateLimitPerMinute\": 50");
        sb.AppendLine("  //   //   },");
        sb.AppendLine("  //   //");
        sb.AppendLine("  //   //   // Azure OpenAI Service");
        sb.AppendLine("  //   //   \"azureOpenAI\": {");
        sb.AppendLine("  //   //     \"endpoint\": \"https://your-resource.openai.azure.com\",  // Required");
        sb.AppendLine("  //   //     \"deploymentName\": \"your-deployment\",  // Required");
        sb.AppendLine("  //   //     \"customSystemPrompt\": \"...\",  // Optional");
        sb.AppendLine("  //   //     \"rateLimitPerMinute\": 60");
        sb.AppendLine("  //   //   },");
        sb.AppendLine("  //   //");
        sb.AppendLine("  //   //   // Azure AI Translator");
        sb.AppendLine("  //   //   \"azureTranslator\": {");
        sb.AppendLine("  //   //     \"region\": \"westus\",  // Optional for global");
        sb.AppendLine("  //   //     \"endpoint\": \"https://api.cognitive.microsofttranslator.com\",");
        sb.AppendLine("  //   //     \"rateLimitPerMinute\": 100");
        sb.AppendLine("  //   //   }");
        sb.AppendLine("  //   // }");
    }

    private void AppendApiKeys(StringBuilder sb, TranslationApiKeys? apiKeys)
    {
        sb.AppendLine("    // ---- API Keys ----");
        sb.AppendLine("    // WARNING: Do not commit API keys to version control!");
        sb.AppendLine("    // Priority: 1) Environment variables, 2) Secure store, 3) These values");
        sb.AppendLine("    // Get your API keys from the provider's website (see comments below)");

        if (apiKeys != null && HasAnyApiKey(apiKeys))
        {
            sb.AppendLine("    \"apiKeys\": {");

            // Google
            if (apiKeys.Google != null)
                sb.AppendLine($"      \"google\": {JsonSerializer.Serialize(apiKeys.Google)},");
            else
                sb.AppendLine("      // \"google\": \"your-google-api-key\",");
            sb.AppendLine("      // Env: LRM_GOOGLE_API_KEY");
            sb.AppendLine("      // Get key: https://cloud.google.com/translate/docs/setup");
            sb.AppendLine();

            // DeepL
            if (apiKeys.DeepL != null)
                sb.AppendLine($"      \"deepL\": {JsonSerializer.Serialize(apiKeys.DeepL)},");
            else
                sb.AppendLine("      // \"deepL\": \"your-deepl-api-key\",");
            sb.AppendLine("      // Env: LRM_DEEPL_API_KEY");
            sb.AppendLine("      // Get key: https://www.deepl.com/pro-api");
            sb.AppendLine();

            // LibreTranslate
            if (apiKeys.LibreTranslate != null)
                sb.AppendLine($"      \"libreTranslate\": {JsonSerializer.Serialize(apiKeys.LibreTranslate)},");
            else
                sb.AppendLine("      // \"libreTranslate\": \"your-libretranslate-api-key\",");
            sb.AppendLine("      // Env: LRM_LIBRETRANSLATE_API_KEY");
            sb.AppendLine("      // Optional for public instances");
            sb.AppendLine("      // Learn more: https://libretranslate.com/");
            sb.AppendLine();

            // OpenAI
            if (apiKeys.OpenAI != null)
                sb.AppendLine($"      \"openAI\": {JsonSerializer.Serialize(apiKeys.OpenAI)},");
            else
                sb.AppendLine("      // \"openAI\": \"sk-...\",");
            sb.AppendLine("      // Env: LRM_OPENAI_API_KEY");
            sb.AppendLine("      // Get key: https://platform.openai.com/api-keys");
            sb.AppendLine();

            // Claude
            if (apiKeys.Claude != null)
                sb.AppendLine($"      \"claude\": {JsonSerializer.Serialize(apiKeys.Claude)},");
            else
                sb.AppendLine("      // \"claude\": \"sk-ant-...\",");
            sb.AppendLine("      // Env: LRM_CLAUDE_API_KEY");
            sb.AppendLine("      // Get key: https://console.anthropic.com/");
            sb.AppendLine();

            // Azure OpenAI
            if (apiKeys.AzureOpenAI != null)
                sb.AppendLine($"      \"azureOpenAI\": {JsonSerializer.Serialize(apiKeys.AzureOpenAI)},");
            else
                sb.AppendLine("      // \"azureOpenAI\": \"your-azure-openai-key\",");
            sb.AppendLine("      // Env: LRM_AZUREOPENAI_API_KEY");
            sb.AppendLine("      // Get from Azure Portal");
            sb.AppendLine();

            // Azure Translator
            if (apiKeys.AzureTranslator != null)
                sb.AppendLine($"      \"azureTranslator\": {JsonSerializer.Serialize(apiKeys.AzureTranslator)}");
            else
                sb.AppendLine("      // \"azureTranslator\": \"your-azure-translator-key\"");
            sb.AppendLine("      // Env: LRM_AZURETRANSLATOR_API_KEY");
            sb.AppendLine("      // Get from Azure Portal (Cognitive Services - Translator)");

            sb.AppendLine("    },");
        }
        else
        {
            sb.AppendLine("    // \"apiKeys\": {");
            sb.AppendLine("    //   // Google Cloud Translation API");
            sb.AppendLine("    //   \"google\": \"your-google-api-key\",");
            sb.AppendLine("    //   // Env: LRM_GOOGLE_API_KEY");
            sb.AppendLine("    //   // Get key: https://cloud.google.com/translate/docs/setup");
            sb.AppendLine("    //");
            sb.AppendLine("    //   // DeepL Translation API");
            sb.AppendLine("    //   \"deepL\": \"your-deepl-api-key\",");
            sb.AppendLine("    //   // Env: LRM_DEEPL_API_KEY");
            sb.AppendLine("    //   // Get key: https://www.deepl.com/pro-api");
            sb.AppendLine("    //");
            sb.AppendLine("    //   // LibreTranslate (optional for public instances)");
            sb.AppendLine("    //   \"libreTranslate\": \"your-libretranslate-api-key\",");
            sb.AppendLine("    //   // Env: LRM_LIBRETRANSLATE_API_KEY");
            sb.AppendLine("    //   // Learn more: https://libretranslate.com/");
            sb.AppendLine("    //");
            sb.AppendLine("    //   // OpenAI GPT models");
            sb.AppendLine("    //   \"openAI\": \"sk-...\",");
            sb.AppendLine("    //   // Env: LRM_OPENAI_API_KEY");
            sb.AppendLine("    //   // Get key: https://platform.openai.com/api-keys");
            sb.AppendLine("    //");
            sb.AppendLine("    //   // Anthropic Claude");
            sb.AppendLine("    //   \"claude\": \"sk-ant-...\",");
            sb.AppendLine("    //   // Env: LRM_CLAUDE_API_KEY");
            sb.AppendLine("    //   // Get key: https://console.anthropic.com/");
            sb.AppendLine("    //");
            sb.AppendLine("    //   // Azure OpenAI Service");
            sb.AppendLine("    //   \"azureOpenAI\": \"your-azure-openai-key\",");
            sb.AppendLine("    //   // Env: LRM_AZUREOPENAI_API_KEY");
            sb.AppendLine("    //   // Get from Azure Portal");
            sb.AppendLine("    //");
            sb.AppendLine("    //   // Azure AI Translator");
            sb.AppendLine("    //   \"azureTranslator\": \"your-azure-translator-key\"");
            sb.AppendLine("    //   // Env: LRM_AZURETRANSLATOR_API_KEY");
            sb.AppendLine("    //   // Get from Azure Portal (Cognitive Services - Translator)");
            sb.AppendLine("    // },");
        }
        sb.AppendLine();
    }

    private void AppendAIProviders(StringBuilder sb, AIProviderConfiguration? aiProviders)
    {
        sb.AppendLine("    // ---- AI Provider Settings ----");
        sb.AppendLine("    // Customize models, prompts, and endpoints for AI translation providers.");
        sb.AppendLine("    // Uncomment and configure the providers you want to use.");

        if (aiProviders != null && HasAnyAIProvider(aiProviders))
        {
            sb.AppendLine("    \"aiProviders\": {");

            // Ollama
            if (aiProviders.Ollama != null)
            {
                sb.AppendLine("      // Ollama - Local LLM inference (free, runs on your machine)");
                sb.AppendLine("      \"ollama\": {");
                sb.AppendLine($"        \"apiUrl\": {JsonSerializer.Serialize(aiProviders.Ollama.ApiUrl ?? "http://localhost:11434")},");
                sb.AppendLine("        // Default: http://localhost:11434");
                sb.AppendLine();
                sb.AppendLine($"        \"model\": {JsonSerializer.Serialize(aiProviders.Ollama.Model ?? "llama3.2")},");
                sb.AppendLine("        // Options: \"llama3.2\", \"llama3.1\", \"mistral\", \"phi\", \"qwen\", etc.");
                sb.AppendLine("        // Run 'ollama list' to see installed models");
                sb.AppendLine();
                if (aiProviders.Ollama.CustomSystemPrompt != null)
                {
                    sb.AppendLine($"        \"customSystemPrompt\": {JsonSerializer.Serialize(aiProviders.Ollama.CustomSystemPrompt)},");
                    sb.AppendLine("        // Override the default translation prompt");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("        // \"customSystemPrompt\": \"Translate the following text...\",");
                    sb.AppendLine("        // Optional: Override the default translation prompt");
                    sb.AppendLine();
                }
                sb.AppendLine($"        \"rateLimitPerMinute\": {aiProviders.Ollama.RateLimitPerMinute ?? 10}");
                sb.AppendLine("        // Default: 10 requests per minute");
                sb.AppendLine("      },");
            }
            else
            {
                sb.AppendLine("      // Ollama - Local LLM inference (free, runs on your machine)");
                sb.AppendLine("      // \"ollama\": {");
                sb.AppendLine("      //   \"apiUrl\": \"http://localhost:11434\",");
                sb.AppendLine("      //   // Default: http://localhost:11434");
                sb.AppendLine("      //");
                sb.AppendLine("      //   \"model\": \"llama3.2\",");
                sb.AppendLine("      //   // Options: \"llama3.2\", \"llama3.1\", \"mistral\", \"phi\", \"qwen\", etc.");
                sb.AppendLine("      //   // Run 'ollama list' to see installed models");
                sb.AppendLine("      //");
                sb.AppendLine("      //   \"customSystemPrompt\": \"Translate the following text...\",");
                sb.AppendLine("      //   // Optional: Override the default translation prompt");
                sb.AppendLine("      //");
                sb.AppendLine("      //   \"rateLimitPerMinute\": 10");
                sb.AppendLine("      //   // Default: 10 requests per minute");
                sb.AppendLine("      // },");
            }
            sb.AppendLine();

            // OpenAI
            if (aiProviders.OpenAI != null)
            {
                sb.AppendLine("      // OpenAI GPT models");
                sb.AppendLine("      \"openAI\": {");
                sb.AppendLine($"        \"model\": {JsonSerializer.Serialize(aiProviders.OpenAI.Model ?? "gpt-4o-mini")},");
                sb.AppendLine("        // Options: \"gpt-4o\", \"gpt-4o-mini\", \"gpt-4-turbo\", \"gpt-3.5-turbo\"");
                sb.AppendLine("        // Recommended: \"gpt-4o-mini\" (fast and cost-effective)");
                sb.AppendLine();
                if (aiProviders.OpenAI.CustomSystemPrompt != null)
                {
                    sb.AppendLine($"        \"customSystemPrompt\": {JsonSerializer.Serialize(aiProviders.OpenAI.CustomSystemPrompt)},");
                    sb.AppendLine("        // Override the default translation prompt");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("        // \"customSystemPrompt\": \"Translate the following text...\",");
                    sb.AppendLine("        // Optional: Override the default translation prompt");
                    sb.AppendLine();
                }
                sb.AppendLine($"        \"rateLimitPerMinute\": {aiProviders.OpenAI.RateLimitPerMinute ?? 60}");
                sb.AppendLine("        // Default: 60 requests per minute");
                sb.AppendLine("      },");
            }
            else
            {
                sb.AppendLine("      // OpenAI GPT models");
                sb.AppendLine("      // \"openAI\": {");
                sb.AppendLine("      //   \"model\": \"gpt-4o-mini\",");
                sb.AppendLine("      //   // Options: \"gpt-4o\", \"gpt-4o-mini\", \"gpt-4-turbo\", \"gpt-3.5-turbo\"");
                sb.AppendLine("      //   // Recommended: \"gpt-4o-mini\" (fast and cost-effective)");
                sb.AppendLine("      //");
                sb.AppendLine("      //   \"customSystemPrompt\": \"Translate the following text...\",");
                sb.AppendLine("      //   // Optional: Override the default translation prompt");
                sb.AppendLine("      //");
                sb.AppendLine("      //   \"rateLimitPerMinute\": 60");
                sb.AppendLine("      //   // Default: 60 requests per minute");
                sb.AppendLine("      // },");
            }
            sb.AppendLine();

            // Claude
            if (aiProviders.Claude != null)
            {
                sb.AppendLine("      // Anthropic Claude models");
                sb.AppendLine("      \"claude\": {");
                sb.AppendLine($"        \"model\": {JsonSerializer.Serialize(aiProviders.Claude.Model ?? "claude-3-5-sonnet-20241022")},");
                sb.AppendLine("        // Options: \"claude-3-5-sonnet-20241022\", \"claude-3-opus-20240229\", \"claude-3-haiku-20240307\"");
                sb.AppendLine("        // Recommended: \"claude-3-5-sonnet-20241022\" (excellent quality)");
                sb.AppendLine();
                if (aiProviders.Claude.CustomSystemPrompt != null)
                {
                    sb.AppendLine($"        \"customSystemPrompt\": {JsonSerializer.Serialize(aiProviders.Claude.CustomSystemPrompt)},");
                    sb.AppendLine("        // Override the default translation prompt");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("        // \"customSystemPrompt\": \"Translate the following text...\",");
                    sb.AppendLine("        // Optional: Override the default translation prompt");
                    sb.AppendLine();
                }
                sb.AppendLine($"        \"rateLimitPerMinute\": {aiProviders.Claude.RateLimitPerMinute ?? 50}");
                sb.AppendLine("        // Default: 50 requests per minute");
                sb.AppendLine("      },");
            }
            else
            {
                sb.AppendLine("      // Anthropic Claude models");
                sb.AppendLine("      // \"claude\": {");
                sb.AppendLine("      //   \"model\": \"claude-3-5-sonnet-20241022\",");
                sb.AppendLine("      //   // Options: \"claude-3-5-sonnet-20241022\", \"claude-3-opus-20240229\", \"claude-3-haiku-20240307\"");
                sb.AppendLine("      //   // Recommended: \"claude-3-5-sonnet-20241022\" (excellent quality)");
                sb.AppendLine("      //");
                sb.AppendLine("      //   \"customSystemPrompt\": \"Translate the following text...\",");
                sb.AppendLine("      //   // Optional: Override the default translation prompt");
                sb.AppendLine("      //");
                sb.AppendLine("      //   \"rateLimitPerMinute\": 50");
                sb.AppendLine("      //   // Default: 50 requests per minute");
                sb.AppendLine("      // },");
            }
            sb.AppendLine();

            // Azure OpenAI
            if (aiProviders.AzureOpenAI != null)
            {
                sb.AppendLine("      // Azure OpenAI Service");
                sb.AppendLine("      \"azureOpenAI\": {");
                sb.AppendLine($"        \"endpoint\": {JsonSerializer.Serialize(aiProviders.AzureOpenAI.Endpoint ?? "https://your-resource.openai.azure.com")},");
                sb.AppendLine("        // Required: Your Azure OpenAI endpoint URL");
                sb.AppendLine("        // Format: https://YOUR-RESOURCE-NAME.openai.azure.com");
                sb.AppendLine();
                sb.AppendLine($"        \"deploymentName\": {JsonSerializer.Serialize(aiProviders.AzureOpenAI.DeploymentName ?? "your-deployment")},");
                sb.AppendLine("        // Required: The name of your model deployment");
                sb.AppendLine("        // Find in Azure Portal under your OpenAI resource");
                sb.AppendLine();
                if (aiProviders.AzureOpenAI.CustomSystemPrompt != null)
                {
                    sb.AppendLine($"        \"customSystemPrompt\": {JsonSerializer.Serialize(aiProviders.AzureOpenAI.CustomSystemPrompt)},");
                    sb.AppendLine("        // Override the default translation prompt");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("        // \"customSystemPrompt\": \"Translate the following text...\",");
                    sb.AppendLine("        // Optional: Override the default translation prompt");
                    sb.AppendLine();
                }
                sb.AppendLine($"        \"rateLimitPerMinute\": {aiProviders.AzureOpenAI.RateLimitPerMinute ?? 60}");
                sb.AppendLine("        // Default: 60 requests per minute");
                sb.AppendLine("      },");
            }
            else
            {
                sb.AppendLine("      // Azure OpenAI Service");
                sb.AppendLine("      // \"azureOpenAI\": {");
                sb.AppendLine("      //   \"endpoint\": \"https://your-resource.openai.azure.com\",");
                sb.AppendLine("      //   // Required: Your Azure OpenAI endpoint URL");
                sb.AppendLine("      //   // Format: https://YOUR-RESOURCE-NAME.openai.azure.com");
                sb.AppendLine("      //");
                sb.AppendLine("      //   \"deploymentName\": \"your-deployment\",");
                sb.AppendLine("      //   // Required: The name of your model deployment");
                sb.AppendLine("      //   // Find in Azure Portal under your OpenAI resource");
                sb.AppendLine("      //");
                sb.AppendLine("      //   \"customSystemPrompt\": \"Translate the following text...\",");
                sb.AppendLine("      //   // Optional: Override the default translation prompt");
                sb.AppendLine("      //");
                sb.AppendLine("      //   \"rateLimitPerMinute\": 60");
                sb.AppendLine("      //   // Default: 60 requests per minute");
                sb.AppendLine("      // },");
            }
            sb.AppendLine();

            // Azure Translator
            if (aiProviders.AzureTranslator != null)
            {
                sb.AppendLine("      // Azure AI Translator (Cognitive Services)");
                sb.AppendLine("      \"azureTranslator\": {");
                if (aiProviders.AzureTranslator.Region != null)
                {
                    sb.AppendLine($"        \"region\": {JsonSerializer.Serialize(aiProviders.AzureTranslator.Region)},");
                    sb.AppendLine("        // Optional for global resources, required for regional");
                    sb.AppendLine("        // Examples: \"westus\", \"eastus\", \"westeurope\"");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("        // \"region\": \"westus\",");
                    sb.AppendLine("        // Optional for global resources, required for regional");
                    sb.AppendLine("        // Examples: \"westus\", \"eastus\", \"westeurope\"");
                    sb.AppendLine();
                }
                if (aiProviders.AzureTranslator.Endpoint != null)
                {
                    sb.AppendLine($"        \"endpoint\": {JsonSerializer.Serialize(aiProviders.AzureTranslator.Endpoint)},");
                    sb.AppendLine("        // Default: https://api.cognitive.microsofttranslator.com");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("        // \"endpoint\": \"https://api.cognitive.microsofttranslator.com\",");
                    sb.AppendLine("        // Default: https://api.cognitive.microsofttranslator.com");
                    sb.AppendLine();
                }
                sb.AppendLine($"        \"rateLimitPerMinute\": {aiProviders.AzureTranslator.RateLimitPerMinute ?? 100}");
                sb.AppendLine("        // Default: 100 requests per minute");
                sb.AppendLine("      }");
            }
            else
            {
                sb.AppendLine("      // Azure AI Translator (Cognitive Services)");
                sb.AppendLine("      // \"azureTranslator\": {");
                sb.AppendLine("      //   \"region\": \"westus\",");
                sb.AppendLine("      //   // Optional for global resources, required for regional");
                sb.AppendLine("      //   // Examples: \"westus\", \"eastus\", \"westeurope\"");
                sb.AppendLine("      //");
                sb.AppendLine("      //   \"endpoint\": \"https://api.cognitive.microsofttranslator.com\",");
                sb.AppendLine("      //   // Default: https://api.cognitive.microsofttranslator.com");
                sb.AppendLine("      //");
                sb.AppendLine("      //   \"rateLimitPerMinute\": 100");
                sb.AppendLine("      //   // Default: 100 requests per minute");
                sb.AppendLine("      // }");
            }

            sb.AppendLine("    }");
        }
        else
        {
            sb.AppendLine("    // \"aiProviders\": {");
            sb.AppendLine("    //   // Ollama - Local LLM inference (free)");
            sb.AppendLine("    //   \"ollama\": {");
            sb.AppendLine("    //     \"apiUrl\": \"http://localhost:11434\",");
            sb.AppendLine("    //     \"model\": \"llama3.2\",  // Options: llama3.2, mistral, phi, qwen");
            sb.AppendLine("    //     \"customSystemPrompt\": \"...\",  // Optional");
            sb.AppendLine("    //     \"rateLimitPerMinute\": 10");
            sb.AppendLine("    //   },");
            sb.AppendLine("    //");
            sb.AppendLine("    //   // OpenAI GPT models");
            sb.AppendLine("    //   \"openAI\": {");
            sb.AppendLine("    //     \"model\": \"gpt-4o-mini\",  // Options: gpt-4o, gpt-4o-mini, gpt-4-turbo");
            sb.AppendLine("    //     \"customSystemPrompt\": \"...\",  // Optional");
            sb.AppendLine("    //     \"rateLimitPerMinute\": 60");
            sb.AppendLine("    //   },");
            sb.AppendLine("    //");
            sb.AppendLine("    //   // Anthropic Claude models");
            sb.AppendLine("    //   \"claude\": {");
            sb.AppendLine("    //     \"model\": \"claude-3-5-sonnet-20241022\",");
            sb.AppendLine("    //     \"customSystemPrompt\": \"...\",  // Optional");
            sb.AppendLine("    //     \"rateLimitPerMinute\": 50");
            sb.AppendLine("    //   },");
            sb.AppendLine("    //");
            sb.AppendLine("    //   // Azure OpenAI Service");
            sb.AppendLine("    //   \"azureOpenAI\": {");
            sb.AppendLine("    //     \"endpoint\": \"https://your-resource.openai.azure.com\",  // Required");
            sb.AppendLine("    //     \"deploymentName\": \"your-deployment\",  // Required");
            sb.AppendLine("    //     \"customSystemPrompt\": \"...\",  // Optional");
            sb.AppendLine("    //     \"rateLimitPerMinute\": 60");
            sb.AppendLine("    //   },");
            sb.AppendLine("    //");
            sb.AppendLine("    //   // Azure AI Translator");
            sb.AppendLine("    //   \"azureTranslator\": {");
            sb.AppendLine("    //     \"region\": \"westus\",  // Optional for global");
            sb.AppendLine("    //     \"endpoint\": \"https://api.cognitive.microsofttranslator.com\",");
            sb.AppendLine("    //     \"rateLimitPerMinute\": 100");
            sb.AppendLine("    //   }");
            sb.AppendLine("    // }");
        }
    }

    private bool HasAnyAIProvider(AIProviderConfiguration aiProviders)
    {
        return aiProviders.Ollama != null
            || aiProviders.OpenAI != null
            || aiProviders.Claude != null
            || aiProviders.AzureOpenAI != null
            || aiProviders.AzureTranslator != null;
    }

    private void AppendScanningConfiguration(StringBuilder sb, ConfigurationModel? config)
    {
        var scanning = config?.Scanning;

        sb.AppendLine("  // ----------------------------------------------------------------");
        sb.AppendLine("  // Code Scanning Configuration");
        sb.AppendLine("  // Configure code scanning patterns for detecting localization key usage.");
        sb.AppendLine("  // The scanner searches your source code to find which keys are being used.");
        sb.AppendLine("  // ----------------------------------------------------------------");

        if (scanning != null && (scanning.ResourceClassNames?.Any() == true || scanning.LocalizationMethods?.Any() == true))
        {
            sb.AppendLine("  \"scanning\": {");

            // Resource Class Names
            if (scanning.ResourceClassNames?.Any() == true)
            {
                sb.AppendLine("    // Resource class names to detect in your code");
                sb.AppendLine("    // These are the class names whose property accesses will be tracked");
                sb.AppendLine("    // Example: If you have \"Resources\", it will detect: Resources.WelcomeMessage");
                sb.AppendLine($"    \"resourceClassNames\": {JsonSerializer.Serialize(scanning.ResourceClassNames)},");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("    // Resource class names to detect in your code");
                sb.AppendLine("    // Example: [\"Resources\", \"Strings\", \"AppResources\"]");
                sb.AppendLine("    // Will detect: Resources.WelcomeMessage, Strings.ErrorMessage, etc.");
                sb.AppendLine("    // \"resourceClassNames\": [\"Resources\", \"Strings\", \"AppResources\"],");
                sb.AppendLine();
            }

            // Localization Methods
            if (scanning.LocalizationMethods?.Any() == true)
            {
                sb.AppendLine("    // Localization method names to detect in your code");
                sb.AppendLine("    // These are methods that accept localization keys as string parameters");
                sb.AppendLine("    // Example: If you have \"GetString\", it will detect: GetString(\"WelcomeMessage\")");
                sb.AppendLine($"    \"localizationMethods\": {JsonSerializer.Serialize(scanning.LocalizationMethods)}");
                sb.AppendLine("    // Common examples: \"GetString\", \"Translate\", \"T\", \"L\", \"_\"");
            }
            else
            {
                sb.AppendLine("    // Localization method names to detect in your code");
                sb.AppendLine("    // Example: [\"GetString\", \"Translate\", \"T\", \"L\"]");
                sb.AppendLine("    // Will detect: GetString(\"Welcome\"), T(\"Error\"), L(\"Message\"), etc.");
                sb.AppendLine("    // \"localizationMethods\": [\"GetString\", \"Translate\", \"T\", \"L\"]");
                sb.AppendLine("    // Common in web frameworks: \"_\", \"__\", \"t\", \"i18n.t\"");
            }

            sb.AppendLine("  },");
        }
        else
        {
            sb.AppendLine("  // \"scanning\": {");
            sb.AppendLine("  //   // Resource class names to detect in your code");
            sb.AppendLine("  //   // Example: [\"Resources\", \"Strings\", \"AppResources\"]");
            sb.AppendLine("  //   // Will detect: Resources.WelcomeMessage, Strings.ErrorMessage, etc.");
            sb.AppendLine("  //   \"resourceClassNames\": [\"Resources\", \"Strings\", \"AppResources\"],");
            sb.AppendLine("  //");
            sb.AppendLine("  //   // Localization method names to detect in your code");
            sb.AppendLine("  //   // Example: [\"GetString\", \"Translate\", \"T\", \"L\"]");
            sb.AppendLine("  //   // Will detect: GetString(\"Welcome\"), T(\"Error\"), L(\"Message\"), etc.");
            sb.AppendLine("  //   \"localizationMethods\": [\"GetString\", \"Translate\", \"T\", \"L\"]");
            sb.AppendLine("  // },");
        }
        sb.AppendLine();
    }

    private void AppendValidationConfiguration(StringBuilder sb, ConfigurationModel? config)
    {
        var validation = config?.Validation;

        sb.AppendLine("  // ----------------------------------------------------------------");
        sb.AppendLine("  // Validation Configuration");
        sb.AppendLine("  // Configure resource validation settings.");
        sb.AppendLine("  // Validates that placeholder patterns match between default and translated values.");
        sb.AppendLine("  // ----------------------------------------------------------------");

        if (validation != null)
        {
            sb.AppendLine("  \"validation\": {");

            // Enable Placeholder Validation
            sb.AppendLine($"    \"enablePlaceholderValidation\": {validation.EnablePlaceholderValidation.ToString().ToLower()},");
            sb.AppendLine("    // Enable placeholder validation to check for mismatches");
            sb.AppendLine("    // Example: \"Hello {0}\" vs \"Bonjour {1}\" would be flagged as an error");
            sb.AppendLine("    // Default: true");
            sb.AppendLine();

            // Placeholder Types
            if (validation.PlaceholderTypes?.Any() == true)
            {
                sb.AppendLine("    // Placeholder types to validate:");
                sb.AppendLine("    // - \"dotnet\": {0}, {1}, {name} (.NET String.Format style)");
                sb.AppendLine("    // - \"printf\": %s, %d, %f (C-style printf)");
                sb.AppendLine("    // - \"icu\": {name}, {count, plural, ...} (ICU MessageFormat)");
                sb.AppendLine("    // - \"template\": ${variable}, {{variable}} (template literals)");
                sb.AppendLine("    // - \"all\": All of the above");
                sb.AppendLine($"    \"placeholderTypes\": {JsonSerializer.Serialize(validation.PlaceholderTypes)}");
                sb.AppendLine("    // Default: [\"dotnet\"]");
            }
            else
            {
                sb.AppendLine("    // Placeholder types to validate:");
                sb.AppendLine("    // - \"dotnet\": {0}, {1}, {name} (.NET String.Format style)");
                sb.AppendLine("    // - \"printf\": %s, %d, %f (C-style printf)");
                sb.AppendLine("    // - \"icu\": {name}, {count, plural, ...} (ICU MessageFormat)");
                sb.AppendLine("    // - \"template\": ${variable}, {{variable}} (template literals)");
                sb.AppendLine("    // - \"all\": All of the above");
                sb.AppendLine("    // \"placeholderTypes\": [\"dotnet\"]");
                sb.AppendLine("    // Default: [\"dotnet\"]");
            }

            sb.AppendLine("  },");
        }
        else
        {
            sb.AppendLine("  // \"validation\": {");
            sb.AppendLine("  //   \"enablePlaceholderValidation\": true,");
            sb.AppendLine("  //   // Enable placeholder validation to check for mismatches");
            sb.AppendLine("  //   // Example: \"Hello {0}\" vs \"Bonjour {1}\" would be flagged");
            sb.AppendLine("  //   // Default: true");
            sb.AppendLine("  //");
            sb.AppendLine("  //   // Placeholder types to validate:");
            sb.AppendLine("  //   // - \"dotnet\": {0}, {1}, {name} (.NET String.Format style)");
            sb.AppendLine("  //   // - \"printf\": %s, %d, %f (C-style printf)");
            sb.AppendLine("  //   // - \"icu\": {name}, {count, plural, ...} (ICU MessageFormat)");
            sb.AppendLine("  //   // - \"template\": ${variable}, {{variable}} (template literals)");
            sb.AppendLine("  //   // - \"all\": All of the above");
            sb.AppendLine("  //   \"placeholderTypes\": [\"dotnet\"]");
            sb.AppendLine("  //   // Default: [\"dotnet\"]");
            sb.AppendLine("  // },");
        }
        sb.AppendLine();
    }

    private void AppendWebConfiguration(StringBuilder sb, ConfigurationModel? config)
    {
        var web = config?.Web;

        sb.AppendLine("  // ----------------------------------------------------------------");
        sb.AppendLine("  // Web Server Configuration");
        sb.AppendLine("  // Configure the built-in web server settings.");
        sb.AppendLine("  // ----------------------------------------------------------------");

        if (web != null)
        {
            sb.AppendLine("  \"web\": {");

            // Port
            if (web.Port.HasValue)
                sb.AppendLine($"    \"port\": {web.Port.Value},  // Env: LRM_WEB_PORT (default: 5000)");
            else
                sb.AppendLine("    // \"port\": 5000,  // Env: LRM_WEB_PORT");

            // Bind Address
            if (web.BindAddress != null)
                sb.AppendLine($"    \"bindAddress\": {JsonSerializer.Serialize(web.BindAddress)},  // Env: LRM_WEB_BIND_ADDRESS (default: localhost)");
            else
                sb.AppendLine("    // \"bindAddress\": \"localhost\",  // Env: LRM_WEB_BIND_ADDRESS");

            // Auto Open Browser
            if (web.AutoOpenBrowser.HasValue)
                sb.AppendLine($"    \"autoOpenBrowser\": {web.AutoOpenBrowser.Value.ToString().ToLower()},  // Env: LRM_WEB_AUTO_OPEN_BROWSER (default: true)");
            else
                sb.AppendLine("    // \"autoOpenBrowser\": true,  // Env: LRM_WEB_AUTO_OPEN_BROWSER");

            // Enable HTTPS
            if (web.EnableHttps.HasValue)
            {
                sb.AppendLine($"    \"enableHttps\": {web.EnableHttps.Value.ToString().ToLower()},  // Env: LRM_WEB_HTTPS_ENABLED");
                if (web.EnableHttps.Value)
                {
                    if (web.HttpsCertificatePath != null)
                        sb.AppendLine($"    \"httpsCertificatePath\": {JsonSerializer.Serialize(web.HttpsCertificatePath)},  // Env: LRM_WEB_HTTPS_CERT_PATH");
                    if (web.HttpsCertificatePassword != null)
                        sb.AppendLine($"    \"httpsCertificatePassword\": {JsonSerializer.Serialize(web.HttpsCertificatePassword)},  // Env: LRM_WEB_HTTPS_CERT_PASSWORD");
                }
            }
            else
            {
                sb.AppendLine("    // \"enableHttps\": false,  // Env: LRM_WEB_HTTPS_ENABLED");
                sb.AppendLine("    // \"httpsCertificatePath\": \"path/to/cert.pfx\",  // Env: LRM_WEB_HTTPS_CERT_PATH");
                sb.AppendLine("    // \"httpsCertificatePassword\": \"password\",  // Env: LRM_WEB_HTTPS_CERT_PASSWORD");
            }

            // CORS
            sb.AppendLine();
            if (web.Cors != null && web.Cors.Enabled)
            {
                sb.AppendLine("    // CORS (Cross-Origin Resource Sharing) Configuration");
                sb.AppendLine("    \"cors\": {");
                sb.AppendLine($"      \"enabled\": {web.Cors.Enabled.ToString().ToLower()},");
                sb.AppendLine("      // Enable CORS to allow API access from other origins");
                sb.AppendLine("      // Default: false");
                sb.AppendLine();
                if (web.Cors.AllowedOrigins?.Any() == true)
                {
                    sb.AppendLine($"      \"allowedOrigins\": {JsonSerializer.Serialize(web.Cors.AllowedOrigins)},");
                    sb.AppendLine("      // List of allowed origins for CORS requests");
                    sb.AppendLine("      // Examples: [\"http://localhost:3000\", \"https://example.com\"]");
                    sb.AppendLine("      // Use [\"*\"] to allow all origins (not recommended for production)");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("      // \"allowedOrigins\": [\"http://localhost:3000\", \"https://example.com\"],");
                    sb.AppendLine("      // List of allowed origins for CORS requests");
                    sb.AppendLine("      // Use [\"*\"] to allow all origins (not recommended for production)");
                    sb.AppendLine();
                }
                sb.AppendLine($"      \"allowCredentials\": {web.Cors.AllowCredentials.ToString().ToLower()}");
                sb.AppendLine("      // Allow credentials (cookies, authorization headers) in CORS requests");
                sb.AppendLine("      // Default: false");
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine("    // CORS (Cross-Origin Resource Sharing) Configuration");
                sb.AppendLine("    // \"cors\": {");
                sb.AppendLine("    //   \"enabled\": false,");
                sb.AppendLine("    //   // Enable CORS to allow API access from other origins");
                sb.AppendLine("    //   // Default: false");
                sb.AppendLine("    //");
                sb.AppendLine("    //   \"allowedOrigins\": [\"http://localhost:3000\", \"https://example.com\"],");
                sb.AppendLine("    //   // List of allowed origins for CORS requests");
                sb.AppendLine("    //   // Examples: [\"http://localhost:3000\", \"https://example.com\"]");
                sb.AppendLine("    //   // Use [\"*\"] to allow all origins (not recommended for production)");
                sb.AppendLine("    //");
                sb.AppendLine("    //   \"allowCredentials\": false");
                sb.AppendLine("    //   // Allow credentials (cookies, authorization headers) in CORS requests");
                sb.AppendLine("    //   // Default: false");
                sb.AppendLine("    // }");
            }

            sb.AppendLine("  }");
        }
        else
        {
            sb.AppendLine("  // \"web\": {");
            sb.AppendLine("  //   \"port\": 5000,");
            sb.AppendLine("  //   // Env: LRM_WEB_PORT");
            sb.AppendLine("  //   // Default: 5000");
            sb.AppendLine("  //");
            sb.AppendLine("  //   \"bindAddress\": \"localhost\",");
            sb.AppendLine("  //   // Env: LRM_WEB_BIND_ADDRESS");
            sb.AppendLine("  //   // Options: \"localhost\", \"0.0.0.0\" (all interfaces), \"*\"");
            sb.AppendLine("  //   // Default: localhost");
            sb.AppendLine("  //");
            sb.AppendLine("  //   \"autoOpenBrowser\": true,");
            sb.AppendLine("  //   // Env: LRM_WEB_AUTO_OPEN_BROWSER");
            sb.AppendLine("  //   // Automatically open browser when starting web server");
            sb.AppendLine("  //   // Default: true");
            sb.AppendLine("  //");
            sb.AppendLine("  //   \"enableHttps\": false,");
            sb.AppendLine("  //   // Env: LRM_WEB_HTTPS_ENABLED");
            sb.AppendLine("  //   // Enable HTTPS (requires certificate configuration below)");
            sb.AppendLine("  //   // Default: false");
            sb.AppendLine("  //");
            sb.AppendLine("  //   \"httpsCertificatePath\": \"path/to/cert.pfx\",");
            sb.AppendLine("  //   // Env: LRM_WEB_HTTPS_CERT_PATH");
            sb.AppendLine("  //   // Required if enableHttps is true");
            sb.AppendLine("  //");
            sb.AppendLine("  //   \"httpsCertificatePassword\": \"password\",");
            sb.AppendLine("  //   // Env: LRM_WEB_HTTPS_CERT_PASSWORD");
            sb.AppendLine("  //   // Required if certificate is password-protected");
            sb.AppendLine("  //");
            sb.AppendLine("  //   // CORS (Cross-Origin Resource Sharing) Configuration");
            sb.AppendLine("  //   \"cors\": {");
            sb.AppendLine("  //     \"enabled\": false,");
            sb.AppendLine("  //     // Enable CORS to allow API access from other origins");
            sb.AppendLine("  //     // Default: false");
            sb.AppendLine("  //");
            sb.AppendLine("  //     \"allowedOrigins\": [\"http://localhost:3000\", \"https://example.com\"],");
            sb.AppendLine("  //     // List of allowed origins for CORS requests");
            sb.AppendLine("  //     // Examples: [\"http://localhost:3000\", \"https://example.com\"]");
            sb.AppendLine("  //     // Use [\"*\"] to allow all origins (not recommended for production)");
            sb.AppendLine("  //");
            sb.AppendLine("  //     \"allowCredentials\": false");
            sb.AppendLine("  //     // Allow credentials (cookies, authorization headers) in CORS requests");
            sb.AppendLine("  //     // Default: false");
            sb.AppendLine("  //   }");
            sb.AppendLine("  // }");
        }
    }

    private bool HasAnyApiKey(TranslationApiKeys apiKeys)
    {
        return apiKeys.Google != null
            || apiKeys.DeepL != null
            || apiKeys.LibreTranslate != null
            || apiKeys.OpenAI != null
            || apiKeys.Claude != null
            || apiKeys.AzureOpenAI != null
            || apiKeys.AzureTranslator != null;
    }
}
