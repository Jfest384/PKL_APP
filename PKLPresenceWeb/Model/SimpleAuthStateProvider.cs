using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Security.Claims;

namespace PKLPresenceWeb.Model
{
    public class SimpleAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _js;

        public SimpleAuthStateProvider(IJSRuntime js)
        {
            _js = js;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _js.InvokeAsync<string>("localStorage.getItem", "authToken");
            ClaimsIdentity identity = string.IsNullOrWhiteSpace(token)
                ? new ClaimsIdentity()
                : new ClaimsIdentity(new[] { new Claim("token", token) }, "apiauth");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        // Tambahkan method ini
        public async Task NotifyUserAuthenticationChanged()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}