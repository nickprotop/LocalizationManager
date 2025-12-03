// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

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
}
