using PKL_API.Models;

namespace PKL_API.Helpers
{
    public static class AuthHelper
    {
        public static async Task<User?> GetCurrentUser(HttpContext httpContext, PklContext db)
        {
            var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null)
                return null;

            if (!int.TryParse(userIdClaim.Value, out int userId))
                return null;

            return await db.Users.FindAsync(userId);
        }
    }
}
