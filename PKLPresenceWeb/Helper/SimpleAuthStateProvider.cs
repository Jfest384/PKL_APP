using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;
using PKLPresenceWeb.Model;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

public class SimpleAuthStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;
    private readonly NavigationManager _navigation;
    private readonly IJSRuntime _js;

    private static readonly string[] PublicRoutes =
    {
        "/lapran-pkl/approval/"
    };

    public SimpleAuthStateProvider(HttpClient http, NavigationManager navigation, IJSRuntime js)
    {
        _http = http;
        _navigation = navigation;
        _js = js;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var currentPath = new Uri(_navigation.Uri).AbsolutePath.ToLower();
        if (PublicRoutes.Any(r => currentPath.StartsWith(r)))
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var response = await _http.GetAsync(APIUrl.Endpoint("me"));
        if (response.IsSuccessStatusCode)
        {
            var me = await response.Content.ReadFromJsonAsync<UserResponse>();
            if (me != null)
                return BuildState(me);
        }
        else if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var refreshResponse = await _http.PostAsync(APIUrl.Endpoint("authentication/refresh"), null);
            if (refreshResponse.IsSuccessStatusCode)
            {
                var meRequest = new HttpRequestMessage(HttpMethod.Get, APIUrl.Endpoint("me"));

                var meResponse = await _http.SendAsync(meRequest);
                if (meResponse.IsSuccessStatusCode)
                {
                    var me = await meResponse.Content.ReadFromJsonAsync<UserResponse>();
                    if (me != null)
                        return BuildState(me);
                }
            }
        }

        RedirectToLogin();
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    private AuthenticationState BuildState(UserResponse me)
    {
        var claims = new List<Claim>
        {
            new("id", me.id.ToString()),
            new(ClaimTypes.Name, me.fullname ?? "User"),
            new(ClaimTypes.Role, me.role ?? "-")
        };

        var identity = new ClaimsIdentity(claims, "cookie");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private void RedirectToLogin()
    {
        var currentUri = new Uri(_navigation.Uri).AbsolutePath.ToLower();
        if (currentUri != "/login")
            _navigation.NavigateTo("/login");
    }

    public async Task NotifyAuthenticationStateChanged()
    {
        base.NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}