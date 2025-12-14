using LrmCloud.Shared.Configuration;

namespace LrmCloud.Api.Services.Billing;

/// <summary>
/// Factory for creating and managing payment provider instances.
/// </summary>
public class PaymentProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CloudConfiguration _config;
    private readonly ILogger<PaymentProviderFactory> _logger;
    private readonly Dictionary<string, IPaymentProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public PaymentProviderFactory(
        IServiceProvider serviceProvider,
        CloudConfiguration config,
        ILogger<PaymentProviderFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Gets all registered payment providers.
    /// </summary>
    public IReadOnlyDictionary<string, IPaymentProvider> Providers => _providers;

    /// <summary>
    /// Gets the currently active payment provider based on configuration.
    /// </summary>
    public IPaymentProvider GetActiveProvider()
    {
        var activeProviderName = _config.Payment?.ActiveProvider ?? "stripe";

        if (!_providers.TryGetValue(activeProviderName, out var provider))
        {
            throw new InvalidOperationException(
                $"Payment provider '{activeProviderName}' is not registered. " +
                $"Available providers: {string.Join(", ", _providers.Keys)}");
        }

        if (!provider.IsEnabled)
        {
            throw new InvalidOperationException(
                $"Payment provider '{activeProviderName}' is configured as active but is not enabled. " +
                "Check the provider configuration.");
        }

        return provider;
    }

    /// <summary>
    /// Gets a specific payment provider by name.
    /// </summary>
    /// <param name="providerName">The provider name (e.g., "stripe", "paypal").</param>
    /// <returns>The payment provider.</returns>
    /// <exception cref="KeyNotFoundException">If the provider is not registered.</exception>
    public IPaymentProvider GetProvider(string providerName)
    {
        if (!_providers.TryGetValue(providerName, out var provider))
        {
            throw new KeyNotFoundException(
                $"Payment provider '{providerName}' is not registered. " +
                $"Available providers: {string.Join(", ", _providers.Keys)}");
        }

        return provider;
    }

    /// <summary>
    /// Tries to get a specific payment provider by name.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <param name="provider">The payment provider if found.</param>
    /// <returns>True if the provider was found.</returns>
    public bool TryGetProvider(string providerName, out IPaymentProvider? provider)
    {
        return _providers.TryGetValue(providerName, out provider);
    }

    /// <summary>
    /// Registers a payment provider.
    /// </summary>
    /// <param name="provider">The provider to register.</param>
    public void RegisterProvider(IPaymentProvider provider)
    {
        _providers[provider.ProviderName] = provider;
        _logger.LogInformation(
            "Registered payment provider: {ProviderName} (Enabled: {IsEnabled})",
            provider.ProviderName,
            provider.IsEnabled);
    }

    /// <summary>
    /// Gets all enabled payment providers.
    /// </summary>
    public IEnumerable<IPaymentProvider> GetEnabledProviders()
    {
        return _providers.Values.Where(p => p.IsEnabled);
    }

    /// <summary>
    /// Checks if any payment provider is enabled.
    /// </summary>
    public bool HasEnabledProvider()
    {
        return _providers.Values.Any(p => p.IsEnabled);
    }

    /// <summary>
    /// Gets the name of the active payment provider from configuration.
    /// </summary>
    public string ActiveProviderName => _config.Payment?.ActiveProvider ?? "stripe";
}
