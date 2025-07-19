using Microsoft.EntityFrameworkCore;
using PKL_API;

public class UserAccessHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PklContext _dbContext;

    public UserAccessHelper(IHttpContextAccessor httpContextAccessor, PklContext dbContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    public async Task<(int userId, int roleId)> GetUserIdAndRoleAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        // 1. Ambil Klaim User ID dari Token
        var userIdClaim = user?.Claims.FirstOrDefault(c => c.Type == "id");
        if (userIdClaim == null)
            throw new UnauthorizedAccessException("User ID not found in token.");

        // 2. Validasi dan Parsing User ID
        if (!int.TryParse(userIdClaim.Value, out int userId))
            throw new UnauthorizedAccessException("Invalid user ID in token.");

        // 3. Ambil Data User dari Database
        var userEntity = await _dbContext.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.id == userId);
        if (userEntity == null)
            throw new UnauthorizedAccessException("User not found.");

        // 4. Ambil Role User
        var role = userEntity.UserRoles.FirstOrDefault();
        if (role == null)
            throw new UnauthorizedAccessException("User role not found.");

        return (userId, role.RoleId);
    }
}
