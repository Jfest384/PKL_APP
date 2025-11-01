using Hangfire.Dashboard;

namespace PKL_API.Helpers
{
    public class RoleBasedAuthorizationFilter : IDashboardAuthorizationFilter
    {
        private readonly string[] _roles;

        public RoleBasedAuthorizationFilter(params string[] roles)
        {
            _roles = roles;
        }

        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            var user = httpContext.User;

            // Pastikan user login
            if (!user.Identity?.IsAuthenticated ?? true)
                return false;

            // Izinkan hanya role yang cocok
            foreach (var role in _roles)
            {
                if (user.IsInRole(role))
                    return true;
            }

            return false;
        }
    }
}
