using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using Blazored.LocalStorage;
using LrmCloud.Web;
using LrmCloud.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// MudBlazor services
builder.Services.AddMudServices();

// Local storage for token persistence
builder.Services.AddBlazoredLocalStorage();

// Authentication services
builder.Services.AddScoped<TokenStorageService>();
builder.Services.AddScoped<LrmAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<LrmAuthStateProvider>());
builder.Services.AddAuthorizationCore();

// Configure HttpClient with API base URL and auth handler
// Use the origin (scheme + host + port) with /api/ path, not relative to Blazor's /app/ base
var baseUri = new Uri(builder.HostEnvironment.BaseAddress);
var apiBaseUrl = new Uri($"{baseUri.Scheme}://{baseUri.Authority}/api/");

builder.Services.AddScoped<AuthenticatedHttpHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthenticatedHttpHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = apiBaseUrl };
});

// Token refresh coordinator (singleton to coordinate across all service instances)
builder.Services.AddSingleton<TokenRefreshCoordinator>();

// Auth service (depends on HttpClient, so register after)
builder.Services.AddScoped<AuthService>();

// Application services
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ResourceService>();
builder.Services.AddScoped<TranslationService>();
builder.Services.AddScoped<SnapshotService>();
builder.Services.AddScoped<TranslationMemoryService>();
builder.Services.AddScoped<GlossaryService>();
builder.Services.AddScoped<CliApiKeyService>();
builder.Services.AddScoped<UsageService>();
builder.Services.AddScoped<LimitsService>();
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<OrganizationContextService>();
builder.Services.AddScoped<BillingService>();

await builder.Build().RunAsync();
