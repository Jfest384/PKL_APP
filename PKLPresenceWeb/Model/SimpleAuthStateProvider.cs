using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Security.Claims;

public class SimpleAuthStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _js;
    private readonly NavigationManager _navigation;

    public SimpleAuthStateProvider(IJSRuntime js, NavigationManager navigation)
    {
        _js = js;
        _navigation = navigation;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _js.InvokeAsync<string>("localStorage.getItem", "authToken");
        var identity = new ClaimsIdentity();

        if (!string.IsNullOrWhiteSpace(token) && !IsTokenExpired(token))
        {
            identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "User") }, "jwt");
        }
        else
        {
            var currentUri = new Uri(_navigation.Uri).AbsolutePath.ToLower();
            if (currentUri != "/login" && currentUri != "/")
            {
                _navigation.NavigateTo("/login", true);
            }
        }

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private bool IsTokenExpired(string token)
    {
        try
        {
            // JWT format: header.payload.signature
            var parts = token.Split('.');
            if (parts.Length != 3)
                return true;

            var payload = parts[1];
            // Pad base64 string if needed
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var obj = System.Text.Json.JsonDocument.Parse(json);
            if (!obj.RootElement.TryGetProperty("exp", out var expElement))
                return true;

            var exp = expElement.GetInt64();
            var expDate = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            return expDate < DateTime.UtcNow;
        }
        catch
        {
            // Jika gagal parsing, anggap expired
            return true;
        }
    }

    public async Task NotifyUserAuthenticationChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}