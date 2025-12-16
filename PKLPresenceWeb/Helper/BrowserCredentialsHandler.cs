using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace PKLPresenceWeb.Model
{
    public class BrowserCredentialsHandler : DelegatingHandler
    {
        public BrowserCredentialsHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            return await base.SendAsync(request, cancellationToken);
        }
    }

}
