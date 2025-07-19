using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PKL_API.Models.DTO;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PKL_API.Controllers
{
    [Route("api/authentication")]
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

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDTO login)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var hashedPassword = PasswordHash(login.password);
            var user = await _db.Users.FirstOrDefaultAsync(q => q.username == login.username && q.password == hashedPassword);

            if (user == null)
            {
                return Unauthorized("Invalid username or password");
            }

            var claims = new Claim[]
            {
                new Claim("id", user.id.ToString()),
                //new Claim("role", user.Roleid.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("70158f9055af67cb94c0175b73624a1b198135aeab541cc06c05da81452009a8"));
            var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                signingCredentials: cred,
                expires: DateTime.Now.AddMinutes(60));

            return Ok(new JwtSecurityTokenHandler().WriteToken(token));
        }

        //[Authorize]
        //[HttpPost("logout")]
        //public IActionResult Logout()
        //{
        //    // For JWT stateless authentication, logout is handled on the client by removing the token.
        //    // Optionally, you can implement token blacklisting here if needed.
        //    return Ok(new { message = "Logged out successfully." });
        //}
    }
}
