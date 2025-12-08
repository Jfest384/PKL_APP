using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PKL_API.Models;
using PKL_API.Models.DTO;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PKL_API.Controllers
{
    [Route("authentication")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly PklContext _db;

        public AuthenticationController(PklContext db)
        {
            _db = db;
        }

        private string PasswordHash(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashbytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));

                return BitConverter.ToString(hashbytes).ToLower().Replace("-", "");
            }
        }

        private string GenerateAccessToken(int userId)
        {
            var claims = new[]
            {
                new Claim("id", userId.ToString())
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("70158f9055af67cb94c0175b73624a1b198135aeab541cc06c05da81452009a8"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDTO login)
        {
            var hashedPassword = PasswordHash(login.password);
            var user = await _db.Users.FirstOrDefaultAsync(q =>
                q.username == login.username && q.password == hashedPassword);

            if (user == null)
                return Unauthorized("Invalid username or password");

            string accessToken = GenerateAccessToken(user.id);
            string refreshToken = GenerateRefreshToken();

            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.id,
                token = refreshToken,
                expires = DateTime.UtcNow.AddDays(30),
                isRevoked = false
            };

            _db.RefreshTokens.Add(refreshTokenEntity);
            await _db.SaveChangesAsync();

            Response.Cookies.Append("access_token", accessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTime.UtcNow.AddHours(1),
                SameSite = SameSiteMode.None,
                Path = "/"
            });

            Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTime.UtcNow.AddDays(30),
                SameSite = SameSiteMode.None,
                Path = "/"
            });

            return Ok(new { message = "Login success" });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            // Ambil refresh token dari cookie
            if (!Request.Cookies.TryGetValue("refresh_token", out string refreshToken))
                return Unauthorized("Missing refresh token");

            var stored = await _db.RefreshTokens
                .FirstOrDefaultAsync(x => x.token == refreshToken && !x.isRevoked);

            if (stored == null || stored.expires < DateTime.UtcNow)
                return Unauthorized("Invalid or expired refresh token");

            int userId = stored.UserId;
            string newAccessToken = GenerateAccessToken(userId);

            Response.Cookies.Append("access_token", newAccessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTime.UtcNow.AddHours(1),
                SameSite = SameSiteMode.Strict
            });

            return Ok(new { message = "Token refreshed" });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("access_token");
            Response.Cookies.Delete("refresh_token");
            return Ok(new { message = "Logged out" });
        }

    }
}
