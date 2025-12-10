using Blazored.LocalStorage;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service to manage the current organization context across the application.
/// </summary>
public class OrganizationContextService
{
    private readonly ILocalStorageService _localStorage;
    private const string StorageKey = "selected_organization_id";

    private int? _selectedOrganizationId;
    private bool _isInitialized;

    public event Action? OnOrganizationChanged;

    public OrganizationContextService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public int? SelectedOrganizationId => _selectedOrganizationId;

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            _selectedOrganizationId = await _localStorage.GetItemAsync<int?>(StorageKey);
        }
        catch
        {
            _selectedOrganizationId = null;
        }

        _isInitialized = true;
    }

    public async Task SetOrganizationAsync(int? organizationId)
    {
        _selectedOrganizationId = organizationId;

        if (organizationId.HasValue)
        {
            await _localStorage.SetItemAsync(StorageKey, organizationId.Value);
        }
        else
        {
            await _localStorage.RemoveItemAsync(StorageKey);
        }

        OnOrganizationChanged?.Invoke();
    }

    public async Task ClearAsync()
    {
        _selectedOrganizationId = null;
        await _localStorage.RemoveItemAsync(StorageKey);
        OnOrganizationChanged?.Invoke();
    }
}
