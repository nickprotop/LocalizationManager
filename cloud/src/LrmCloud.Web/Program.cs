using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using LrmCloud.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient with API base URL (same-origin, proxied by nginx)
var apiBaseUrl = new Uri(new Uri(builder.HostEnvironment.BaseAddress), "api/");
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = apiBaseUrl });

await builder.Build().RunAsync();
