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

            if (!user.Identity?.IsAuthenticated ?? true)
                return false;

            return _roles.Any(role => user.IsInRole(role));
        }
    }
}
