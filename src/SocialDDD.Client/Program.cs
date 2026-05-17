using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SocialDDD.Client;
using SocialDDD.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// In Docker: nginx proxies /api/* to the API container, so BaseAddress is the client's own origin.
// For local dev without Docker, override by setting ApiBaseUrl in wwwroot/appsettings.json.
var apiBase = builder.Configuration["ApiBaseUrl"];
var baseAddress = string.IsNullOrEmpty(apiBase)
    ? builder.HostEnvironment.BaseAddress
    : apiBase;

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(baseAddress) });

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PostApiService>();

await builder.Build().RunAsync();
