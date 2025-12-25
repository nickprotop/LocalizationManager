using Microsoft.JSInterop;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for managing Radzen themes with localStorage persistence.
/// </summary>
public class LrmThemeService
{
    private readonly IJSRuntime _js;
    private readonly Radzen.ThemeService _radzenThemeService;

    public string CurrentTheme { get; private set; } = "default";

    /// <summary>
    /// Available free Radzen themes (no premium subscription required).
    /// </summary>
    public static readonly string[] AvailableThemes = new[]
    {
        "default",
        "dark",
        "material",
        "material-dark",
        "standard",
        "standard-dark",
        "humanistic",
        "humanistic-dark",
        "software",
        "software-dark"
    };

    /// <summary>
    /// Gets display-friendly theme names.
    /// </summary>
    public static readonly Dictionary<string, string> ThemeDisplayNames = new()
    {
        ["default"] = "Default",
        ["dark"] = "Default Dark",
        ["material"] = "Material",
        ["material-dark"] = "Material Dark",
        ["standard"] = "Standard",
        ["standard-dark"] = "Standard Dark",
        ["humanistic"] = "Humanistic",
        ["humanistic-dark"] = "Humanistic Dark",
        ["software"] = "Software",
        ["software-dark"] = "Software Dark"
    };

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
            var savedTheme = await _js.InvokeAsync<string?>("localStorage.getItem", "lrm-radzen-theme");
            if (!string.IsNullOrEmpty(savedTheme) && AvailableThemes.Contains(savedTheme))
            {
                CurrentTheme = savedTheme;
                _radzenThemeService.SetTheme(savedTheme);
            }
            else
            {
                // Check for legacy dark mode preference
                var legacyTheme = await _js.InvokeAsync<string?>("localStorage.getItem", "lrm-theme");
                if (legacyTheme == "dark")
                {
                    CurrentTheme = "dark";
                    _radzenThemeService.SetTheme("dark");
                }
                else
                {
                    CurrentTheme = "default";
                    _radzenThemeService.SetTheme("default");
                }
            }
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
        if (!AvailableThemes.Contains(theme))
        {
            theme = "default";
        }

        CurrentTheme = theme;
        _radzenThemeService.SetTheme(theme);

        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", "lrm-radzen-theme", theme);
        }
        catch
        {
            // Ignore localStorage errors
        }

        OnThemeChanged?.Invoke();
    }

    /// <summary>
    /// Check if the current theme is a dark variant.
    /// </summary>
    public bool IsDarkTheme => CurrentTheme.EndsWith("-dark") || CurrentTheme == "dark";

    /// <summary>
    /// Toggle between light and dark variant of current theme.
    /// </summary>
    public async Task ToggleDarkModeAsync()
    {
        string newTheme;

        if (IsDarkTheme)
        {
            // Switch to light variant
            newTheme = CurrentTheme.Replace("-dark", "");
            if (newTheme == "dark") newTheme = "default";
        }
        else
        {
            // Switch to dark variant
            newTheme = CurrentTheme == "default" ? "dark" : $"{CurrentTheme}-dark";
        }

        await SetThemeAsync(newTheme);
    }
}
