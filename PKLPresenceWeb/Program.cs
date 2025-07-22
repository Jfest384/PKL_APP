using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PKLPresenceWeb;
using PKLPresenceWeb.Model;
using System.Globalization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Registrasi Authorization dan AuthenticationStateProvider
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, SimpleAuthStateProvider>();

// Registrasi HttpClient
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Set default culture
CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("id-ID");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("id-ID");

await builder.Build().RunAsync();