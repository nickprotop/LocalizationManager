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
var apiBaseUrl = new Uri(new Uri(builder.HostEnvironment.BaseAddress), "api/");

builder.Services.AddScoped<AuthenticatedHttpHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthenticatedHttpHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = apiBaseUrl };
});

// Auth service (depends on HttpClient, so register after)
builder.Services.AddScoped<AuthService>();

// Application services
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ResourceService>();

await builder.Build().RunAsync();
