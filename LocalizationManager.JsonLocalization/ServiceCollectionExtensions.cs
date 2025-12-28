// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.JsonLocalization.Ota;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace LocalizationManager.JsonLocalization;

/// <summary>
/// Extension methods for configuring JSON localization services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds JSON localization services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJsonLocalization(this IServiceCollection services)
    {
        return services.AddJsonLocalization(_ => { });
    }

    /// <summary>
    /// Adds JSON localization services to the service collection with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure JSON localization options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJsonLocalization(
        this IServiceCollection services,
        Action<JsonLocalizationOptions> configure)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        // Configure options
        services.Configure(configure);

        // Register the factory
        services.TryAddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();

        // Register generic IStringLocalizer<T> using the adapter that delegates to the factory
        services.TryAdd(ServiceDescriptor.Transient(typeof(IStringLocalizer<>), typeof(StringLocalizerAdapter<>)));

        // Register non-generic IStringLocalizer using a factory
        services.TryAddTransient<IStringLocalizer>(sp =>
        {
            var factory = sp.GetRequiredService<IStringLocalizerFactory>();
            return factory.Create(typeof(object));
        });

        return services;
    }

    /// <summary>
    /// Adds a scoped JsonLocalizer for direct injection without IStringLocalizer interface.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure JSON localization options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJsonLocalizerDirect(
        this IServiceCollection services,
        Action<JsonLocalizationOptions>? configure = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.Configure<JsonLocalizationOptions>(options =>
        {
            configure?.Invoke(options);
        });

        services.TryAddScoped(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JsonLocalizationOptions>>().Value;

            IResourceLoader loader;
            if (options.UseEmbeddedResources)
            {
                var assembly = options.ResourceAssembly ?? System.Reflection.Assembly.GetEntryAssembly()!;
                loader = new EmbeddedResourceLoader(assembly, options.ResourcesPath);
            }
            else
            {
                var resourcesPath = Path.Combine(AppContext.BaseDirectory, options.ResourcesPath);
                loader = new FileSystemResourceLoader(resourcesPath);
            }

            return new JsonLocalizer(loader, options.BaseName, options.GetFormatConfiguration());
        });

        return services;
    }

    /// <summary>
    /// Adds JSON localization with OTA (Over-The-Air) support.
    /// Translations are fetched from LRM Cloud at runtime and refreshed periodically.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure JSON localization and OTA options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddJsonLocalizationWithOta(options => {
    ///     options.UseOta(
    ///         endpoint: "https://lrm-cloud.com",
    ///         apiKey: "lrm_your_api_key",
    ///         project: "@username/my-project"
    ///     );
    ///     options.FallbackToLocal = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddJsonLocalizationWithOta(
        this IServiceCollection services,
        Action<JsonLocalizationOptions> configure)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        // Configure options
        var options = new JsonLocalizationOptions();
        configure(options);

        if (options.Ota == null)
            throw new InvalidOperationException("OTA options must be configured. Use options.UseOta(...) to configure.");

        // Register options
        services.AddSingleton(options.Ota);
        services.Configure(configure);

        // Register OTA client as singleton
        services.AddSingleton<OtaClient>();

        // Register background refresh service
        services.AddHostedService<OtaRefreshService>();

        // Register the factory with OTA support
        services.TryAddSingleton<IStringLocalizerFactory>(sp =>
        {
            var otaClient = sp.GetRequiredService<OtaClient>();
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JsonLocalizationOptions>>().Value;

            // Create fallback loader if configured
            IResourceLoader? fallbackLoader = null;
            if (opts.Ota?.FallbackToLocal == true)
            {
                if (opts.UseEmbeddedResources)
                {
                    var assembly = opts.ResourceAssembly ?? System.Reflection.Assembly.GetEntryAssembly()!;
                    fallbackLoader = new EmbeddedResourceLoader(assembly, opts.ResourcesPath);
                }
                else
                {
                    var resourcesPath = Path.Combine(AppContext.BaseDirectory, opts.ResourcesPath);
                    fallbackLoader = new FileSystemResourceLoader(resourcesPath);
                }
            }

            // Create OTA resource loader with logging
            var loggerFactory = sp.GetService<ILoggerFactory>();
            var otaLoaderLogger = loggerFactory?.CreateLogger<OtaResourceLoader>();
            var otaLoader = new OtaResourceLoader(otaClient, fallbackLoader, otaLoaderLogger);

            return new JsonStringLocalizerFactory(otaLoader, opts.BaseName, opts.GetFormatConfiguration());
        });

        // Register generic IStringLocalizer<T> using the adapter
        services.TryAdd(ServiceDescriptor.Transient(typeof(IStringLocalizer<>), typeof(StringLocalizerAdapter<>)));

        // Register non-generic IStringLocalizer
        services.TryAddTransient<IStringLocalizer>(sp =>
        {
            var factory = sp.GetRequiredService<IStringLocalizerFactory>();
            return factory.Create(typeof(object));
        });

        return services;
    }
}
