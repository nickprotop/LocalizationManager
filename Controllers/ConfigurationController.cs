// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Models.Api;
using LocalizationManager.Services;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly ConfigurationService _configService;
    private readonly ConfigurationSchemaService _schemaService;

    public ConfigurationController(ConfigurationService configService, ConfigurationSchemaService schemaService)
    {
        _configService = configService;
        _schemaService = schemaService;
    }

    /// <summary>
    /// Get current configuration (auto-reloads if file changed)
    /// </summary>
    [HttpGet]
    public ActionResult<ConfigurationResponse> GetConfiguration()
    {
        try
        {
            var config = _configService.GetConfiguration();

            return Ok(new ConfigurationResponse
            {
                Configuration = config,
                Message = "Configuration loaded successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Update configuration with validation (saves to lrm.json and triggers reload)
    /// </summary>
    [HttpPut]
    public ActionResult<OperationResponse> UpdateConfiguration([FromBody] ConfigurationModel config)
    {
        try
        {
            // Validate configuration
            var (isValid, errors) = _configService.ValidateConfiguration(config);
            if (!isValid)
            {
                return BadRequest(new ConfigValidationResponse
                {
                    IsValid = false,
                    Errors = errors
                });
            }

            // Save and reload
            _configService.SaveConfiguration(config);

            return Ok(new OperationResponse
            {
                Success = true,
                Message = "Configuration updated and reloaded successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Create new configuration file with validation
    /// </summary>
    [HttpPost]
    public ActionResult<OperationResponse> CreateConfiguration([FromBody] ConfigurationModel config)
    {
        try
        {
            // Validate configuration
            var (isValid, errors) = _configService.ValidateConfiguration(config);
            if (!isValid)
            {
                return BadRequest(new ConfigValidationResponse
                {
                    IsValid = false,
                    Errors = errors
                });
            }

            // Create configuration
            _configService.CreateConfiguration(config);

            return Ok(new OperationResponse
            {
                Success = true,
                Message = "Configuration file created successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponse { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Validate configuration without saving
    /// </summary>
    [HttpPost("validate")]
    public ActionResult<ConfigValidationResponse> ValidateConfiguration([FromBody] ConfigurationModel config)
    {
        try
        {
            var (isValid, errors) = _configService.ValidateConfiguration(config);

            return Ok(new ConfigValidationResponse
            {
                IsValid = isValid,
                Errors = errors.Any() ? errors : null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Get configuration schema (for validation/UI generation)
    /// </summary>
    [HttpGet("schema")]
    public ActionResult<ConfigSchemaResponse> GetSchema()
    {
        return Ok(new
        {
            schema = new
            {
                type = "object",
                properties = new
                {
                    resourcePath = new
                    {
                        type = "string",
                        description = "Path to resource files directory"
                    },
                    sourcePath = new
                    {
                        type = "string",
                        description = "Path to source code directory for scanning"
                    },
                    defaultLanguage = new
                    {
                        type = "string",
                        description = "Default language code (e.g., 'en')"
                    },
                    translation = new
                    {
                        type = "object",
                        properties = new
                        {
                            defaultProvider = new
                            {
                                type = "string",
                                description = "Default translation provider",
                                @enum = new[] { "google", "deepl", "azure", "azureopenai", "openai", "claude", "ollama", "libretranslate" }
                            },
                            sourceLanguage = new
                            {
                                type = "string",
                                description = "Default source language for translations"
                            },
                            providers = new
                            {
                                type = "object",
                                description = "Provider-specific configuration (API keys, endpoints, etc.)"
                            }
                        }
                    },
                    scanning = new
                    {
                        type = "object",
                        properties = new
                        {
                            excludePatterns = new
                            {
                                type = "array",
                                items = new { type = "string" },
                                description = "Glob patterns to exclude from code scanning (e.g., '**/bin/**', '**/obj/**')"
                            }
                        }
                    },
                    web = new
                    {
                        type = "object",
                        properties = new
                        {
                            port = new
                            {
                                type = "integer",
                                description = "Web server port (default: 5000)"
                            },
                            bindAddress = new
                            {
                                type = "string",
                                description = "Bind address (default: localhost)"
                            },
                            autoOpenBrowser = new
                            {
                                type = "boolean",
                                description = "Auto-open browser when starting web server (default: true)"
                            },
                            cors = new
                            {
                                type = "object",
                                properties = new
                                {
                                    enabled = new
                                    {
                                        type = "boolean",
                                        description = "Whether to enable CORS (default: false)"
                                    },
                                    allowedOrigins = new
                                    {
                                        type = "array",
                                        items = new { type = "string" },
                                        description = "Allowed origins for CORS (e.g., ['http://localhost:3000'])"
                                    },
                                    allowCredentials = new
                                    {
                                        type = "boolean",
                                        description = "Whether to allow credentials in CORS requests (default: false)"
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// Get schema-enriched configuration with inline documentation
    /// Returns a formatted JSON string with all available options and current values
    /// </summary>
    [HttpGet("enriched")]
    public ActionResult<SchemaEnrichedConfigResponse> GetSchemaEnrichedConfig()
    {
        try
        {
            var config = _configService.GetConfiguration();
            var enrichedJson = _schemaService.GenerateSchemaEnrichedConfig(config);

            return Ok(new SchemaEnrichedConfigResponse
            {
                EnrichedJson = enrichedJson,
                Message = "Schema-enriched configuration generated successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse { Error = ex.Message });
        }
    }
}
