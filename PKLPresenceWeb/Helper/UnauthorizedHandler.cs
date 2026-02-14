using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net;

namespace PKLPresenceWeb.Helper
{
    public class UnauthorizedHandler : DelegatingHandler
    {
        private readonly NavigationManager _navigation;
        private readonly IJSRuntime _js;
        private static bool _isRedirecting = false;

        public UnauthorizedHandler(NavigationManager navigation, IJSRuntime js)
        {
            _navigation = navigation;
            _js = js;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var relativePath = _navigation.ToBaseRelativePath(_navigation.Uri);

                if (!relativePath.StartsWith("login"))
                {
                    _isRedirecting = true;

                    await _js.InvokeVoidAsync("localStorage.removeItem", "token");

                    _navigation.NavigateTo("/login", true);
                }
            }

            return response;
        }
    }
}
