// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.AspNetCore.Mvc;
using LocalizationManager.Core.Configuration;

namespace LocalizationManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly ConfigurationService _configService;

    public ConfigurationController(ConfigurationService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// Get current configuration (auto-reloads if file changed)
    /// </summary>
    [HttpGet]
    public ActionResult<object> GetConfiguration()
    {
        try
        {
            var config = _configService.GetConfiguration();

            return Ok(new
            {
                configuration = config,
                message = "Configuration loaded successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update configuration with validation (saves to lrm.json and triggers reload)
    /// </summary>
    [HttpPut]
    public ActionResult<object> UpdateConfiguration([FromBody] ConfigurationModel config)
    {
        try
        {
            // Validate configuration
            var (isValid, errors) = _configService.ValidateConfiguration(config);
            if (!isValid)
            {
                return BadRequest(new
                {
                    error = "Configuration validation failed",
                    validationErrors = errors
                });
            }

            // Save and reload
            _configService.SaveConfiguration(config);

            return Ok(new
            {
                success = true,
                message = "Configuration updated and reloaded successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create new configuration file with validation
    /// </summary>
    [HttpPost]
    public ActionResult<object> CreateConfiguration([FromBody] ConfigurationModel config)
    {
        try
        {
            // Validate configuration
            var (isValid, errors) = _configService.ValidateConfiguration(config);
            if (!isValid)
            {
                return BadRequest(new
                {
                    error = "Configuration validation failed",
                    validationErrors = errors
                });
            }

            // Create configuration
            _configService.CreateConfiguration(config);

            return Ok(new
            {
                success = true,
                message = "Configuration file created successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Validate configuration without saving
    /// </summary>
    [HttpPost("validate")]
    public ActionResult<object> ValidateConfiguration([FromBody] ConfigurationModel config)
    {
        try
        {
            var (isValid, errors) = _configService.ValidateConfiguration(config);

            return Ok(new
            {
                isValid,
                errors = errors.Any() ? errors : null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get configuration schema (for validation/UI generation)
    /// </summary>
    [HttpGet("schema")]
    public ActionResult<object> GetSchema()
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
}
