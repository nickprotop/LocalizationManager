using Microsoft.JSInterop;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for managing Radzen themes with localStorage persistence.
/// Simplified to support only default (light) and dark themes.
/// </summary>
public class LrmThemeService
{
    private readonly IJSRuntime _js;
    private readonly Radzen.ThemeService _radzenThemeService;

    private const string StorageKey = "lrm-radzen-theme";

    public string CurrentTheme { get; private set; } = "default";

    public event Action? OnThemeChanged;

    public LrmThemeService(IJSRuntime js, Radzen.ThemeService radzenThemeService)
    {
        _js = js;
        _radzenThemeService = radzenThemeService;
    }

    /// <summary>
    /// Initialize theme from localStorage on app startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var savedTheme = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);

            // Only allow "default" or "dark", normalize any other saved theme
            if (savedTheme == "dark" || savedTheme?.EndsWith("-dark") == true)
            {
                CurrentTheme = "dark";
            }
            else
            {
                CurrentTheme = "default";
            }

            _radzenThemeService.SetTheme(CurrentTheme);
        }
        catch
        {
            // Fallback to default if localStorage is not available
            CurrentTheme = "default";
            _radzenThemeService.SetTheme("default");
        }
    }

    /// <summary>
    /// Change the current theme and persist to localStorage.
    /// </summary>
    public async Task SetThemeAsync(string theme)
    {
        // Normalize to only "default" or "dark"
        CurrentTheme = (theme == "dark" || theme?.EndsWith("-dark") == true) ? "dark" : "default";
        _radzenThemeService.SetTheme(CurrentTheme);

        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, CurrentTheme);
        }
        catch
        {
            // Ignore localStorage errors
        }

        OnThemeChanged?.Invoke();
    }

    /// <summary>
    /// Check if the current theme is dark.
    /// </summary>
    public bool IsDarkTheme => CurrentTheme == "dark";

    /// <summary>
    /// Toggle between light and dark themes.
    /// </summary>
    public async Task ToggleDarkModeAsync()
    {
        await SetThemeAsync(IsDarkTheme ? "default" : "dark");
    }
}
