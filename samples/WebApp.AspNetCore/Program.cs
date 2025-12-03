// Sample: ASP.NET Core Web API with IStringLocalizer
// This demonstrates using JsonLocalization with ASP.NET Core's built-in localization infrastructure.

using System.Globalization;
using LocalizationManager.JsonLocalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;

var builder = WebApplication.CreateBuilder(args);

// Add JSON localization services with IStringLocalizer support
builder.Services.AddJsonLocalization(options =>
{
    options.ResourcesPath = "Resources";
    options.BaseName = "strings";
});

// Also register direct JsonLocalizer for pluralization support
builder.Services.AddJsonLocalizerDirect(options =>
{
    options.ResourcesPath = "Resources";
    options.BaseName = "strings";
});

// Configure request localization
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en"),
        new CultureInfo("de"),
        new CultureInfo("zh")
    };

    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

var app = builder.Build();

// Enable request localization
app.UseRequestLocalization();

// Home endpoint - shows localized content
app.MapGet("/", (IStringLocalizer localizer) =>
{
    return new
    {
        Welcome = localizer["welcome"].Value,
        Navigation = new
        {
            Home = localizer["navigation.home"].Value,
            About = localizer["navigation.about"].Value,
            Contact = localizer["navigation.contact"].Value
        },
        Culture = CultureInfo.CurrentUICulture.Name
    };
});

// Greeting endpoint with parameter
app.MapGet("/greet/{name}", (string name, IStringLocalizer localizer) =>
{
    return new
    {
        Message = localizer["greeting", name].Value,
        Culture = CultureInfo.CurrentUICulture.Name
    };
});

// Items endpoint with pluralization (using direct JsonLocalizer)
app.MapGet("/items/{count:int}", (int count, JsonLocalizer localizer) =>
{
    return new
    {
        ItemCount = localizer.Plural("itemCount", count),
        Culture = CultureInfo.CurrentUICulture.Name
    };
});

// List all strings endpoint
app.MapGet("/strings", (IStringLocalizer localizer) =>
{
    var strings = new Dictionary<string, string>();
    foreach (var str in localizer.GetAllStrings())
    {
        strings[str.Name] = str.Value;
    }
    return strings;
});

app.Run();
