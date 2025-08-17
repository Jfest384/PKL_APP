using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Security.Claims;
using System.Threading.Tasks;

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
        return false;
    }

    public async Task NotifyUserAuthenticationChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}