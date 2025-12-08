using Hangfire.Dashboard;
using System.Text;

namespace PKL_API.Helpers
{
    public class BasicDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        private readonly string _username;
        private readonly string _password;
        private static readonly Dictionary<string, DateTime> ActiveSessions = new();

        private const int SESSION_MINUTES = 10;

        public BasicDashboardAuthorizationFilter(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public bool Authorize(DashboardContext context)
        {
            var http = context.GetHttpContext();

            http.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            http.Response.Headers["Pragma"] = "no-cache";
            http.Response.Headers["Expires"] = "-1";

            string auth = http.Request.Headers["Authorization"];

            if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                return Challenge(http);
            }

            var encoded = auth.Substring("Basic ".Length).Trim();
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var parts = decoded.Split(':', 2);

            if (parts.Length != 2)
                return Challenge(http);

            var username = parts[0];
            var password = parts[1];

            if (username != _username || password != _password)
                return Challenge(http);

            var sessionKey = encoded;
            if (ActiveSessions.TryGetValue(sessionKey, out var expiresAt))
            {
                if (expiresAt > DateTime.UtcNow)
                {
                    ActiveSessions[sessionKey] = DateTime.UtcNow.AddMinutes(SESSION_MINUTES);
                    return true;
                }

                ActiveSessions.Remove(sessionKey);
            }

            ActiveSessions[sessionKey] = DateTime.UtcNow.AddMinutes(SESSION_MINUTES);
            return true;
        }

        private bool Challenge(HttpContext http)
        {
            http.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire Dashboard\"";
            http.Response.StatusCode = 401;
            return false;
        }
    }


    public class SwaggerBasicAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _username;
        private readonly string _password;

        public SwaggerBasicAuthMiddleware(RequestDelegate next, string username, string password)
        {
            _next = next;
            _username = username;
            _password = password;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Lindungi semua endpoint swagger
            if (context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
            {
                string authHeader = context.Request.Headers["Authorization"];

                if (authHeader != null && authHeader.StartsWith("Basic "))
                {
                    var encodedUsernamePassword = authHeader.Substring("Basic ".Length).Trim();
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encodedUsernamePassword));
                    var parts = decoded.Split(':', 2);

                    if (parts.Length == 2)
                    {
                        var username = parts[0];
                        var password = parts[1];

                        if (username == _username && password == _password)
                        {
                            await _next(context);
                            return;
                        }
                    }
                }

                context.Response.Headers["WWW-Authenticate"] = "Basic";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }

            await _next(context);
        }
    }
}