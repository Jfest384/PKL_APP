using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PKL_API.Models;
using PKL_API.Models.DTO;

namespace PKL_API.Controllers
{
    [Route("data")]
    [ApiController]
    public class DataController : ControllerBase
    {
        private readonly PklContext _db;

        public DataController(PklContext db)
        {
            _db = db;
        }

        [HttpGet("walas")]
        public IActionResult GetWalasData()
        {
            var waliKelas = _db.WaliKelas
                .Select(wk => new
                {
                    wk.id,
                    wk.Userid,
                    wk.User.fullname,
                    wk.Teacherid
                })
                .ToList();
            return Ok(waliKelas);
        }

        [HttpGet("presence-types")]
        public IActionResult GetPresenceTypesData()
        {
            var types = _db.PresenceTypes
                .Select(c => new
                {
                    c.id,
                    c.name
                })
                .ToList();
            return Ok(types);
        }

        [HttpGet("companies")]
        public async Task<IActionResult> GetCompaniesData([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? name = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.Companies.AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(c => c.name.Contains(name));
            }

            var companies = query
                .Select(c => new
                {
                    c.id,
                    c.name,
                    address = string.IsNullOrEmpty(c.address) ? "-" : c.address
                });

            var totalItems = await companies.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            var companiesList = await companies
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                totalItems,
                totalPages,
                companies = companiesList
            });
        }

        [HttpPost("companies/details")]
        public async Task<IActionResult> GetCompanyDetails([FromBody] int companyId)
        {
            if (companyId <= 0)
                return BadRequest("Invalid companyId.");

            var company = await _db.Companies
                .Where(c => c.id == companyId)
                .Select(c => new
                {
                    c.id,
                    c.name,
                    address = c.address ?? "",
                    phone = c.phone ?? "",
                    lat = c.lat,
                    lon = c.longitude
                })
                .FirstOrDefaultAsync();

            if (company == null)
                return NotFound("Company not found.");
            return Ok(company);
        }

        [Authorize]
        [HttpPost("companies/add")]
        public async Task<IActionResult> AddCompany([FromBody] CompanyDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.name) || string.IsNullOrWhiteSpace(dto.address))
                return BadRequest("Invalid request body.");

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("User ID not found in token.");

            var user = await _db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.id == userId);

            if (user == null)
                return Unauthorized("User not found.");

            var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
            if (!roleIds.Contains(1) && !roleIds.Contains(4))
                return Forbid("Hanya roleId 1/4 yang bisa melakukan aksi ini.");

            var company = new Company
            {
                name = dto.name,
                address = dto.address
            };

            if (dto.Lat.HasValue)
                company.lat = Math.Round(dto.Lat.Value, 12, MidpointRounding.AwayFromZero);
            if (dto.Long.HasValue)
                company.longitude = Math.Round(dto.Long.Value, 12, MidpointRounding.AwayFromZero);

            _db.Companies.Add(company);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Company berhasil ditambahkan.",
                data = new
                {
                    company.id,
                    company.name,
                    company.address,
                    company.lat,
                    company.longitude
                }
            });
        }

        [Authorize]
        [HttpDelete("companies/delete")]
        public async Task<IActionResult> DeleteCompany([FromBody] int companyId)
        {
            if (companyId <= 0)
                return BadRequest("Invalid companyId.");

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("User ID not found in token.");

            var user = await _db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.id == userId);

            if (user == null)
                return Unauthorized("User not found.");

            var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
            if (!roleIds.Contains(1) && !roleIds.Contains(4))
                return Forbid("Hanya roleId 1/4 yang bisa melakukan aksi ini.");

            var company = await _db.Companies.FindAsync(companyId);
            if (company == null)
                return NotFound("Company not found.");

            var students = await _db.Students.Where(s => s.Companyid == companyId).ToListAsync();
            foreach (var student in students)
            {
                student.Companyid = null;
            }

            _db.Companies.Remove(company);
            await _db.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                message = "Company berhasil dihapus dan relasi pada Student di-null-kan."
            });
        }

        [Authorize]
        [HttpPut("companies/{companyId}")]
        public async Task<IActionResult> EditCompany(int companyId, [FromBody] CompanyEditDTO dto)
        {
            if (companyId <= 0)
                return BadRequest("companyId is required and must be greater than 0.");

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null)
                return Unauthorized("User ID not found in token.");
            int userId = int.Parse(userIdClaim.Value);

            var userRole = await _db.Roles
                .Where(r => _db.UserRoles.Any(ur => ur.User.id == userId && ur.Role.id == r.id))
                .Select(r => r.id)
                .FirstOrDefaultAsync();

            if (userRole != 1 && userRole != 4)
                return StatusCode(403, "Only admin and kepala jurusan can edit a company.");

            var company = await _db.Companies.FindAsync(companyId);
            if (company == null)
                return NotFound("Company not found.");

            if (string.IsNullOrWhiteSpace(dto.name) && string.IsNullOrWhiteSpace(dto.address))
                return BadRequest("Company name and address is required.");

            company.name = dto.name;
            company.address = dto.address;
            if (dto.Lat.HasValue)
                company.lat = Math.Round(dto.Lat.Value, 12, MidpointRounding.AwayFromZero);
            if (dto.Long.HasValue)
                company.longitude = Math.Round(dto.Long.Value, 12, MidpointRounding.AwayFromZero);

            await _db.SaveChangesAsync();
            return Ok(new { message = "Company updated successfully." });
        }

        [Authorize]
        [HttpDelete("location/reset")]
        public async Task<IActionResult> ResetLocation([FromBody] int studentId)
        {
            if (studentId <= 0)
                return BadRequest("Invalid studentId.");

            var studentExists = await _db.Students.AnyAsync(s => s.id == studentId);
            if (!studentExists)
                return NotFound("Student not found.");

            var lockLocations = await _db.LockLocations
                .Where(l => l.Studentid == studentId)
                .ToListAsync();

            if (lockLocations.Count == 0)
                return NotFound("Tidak ada data LockLocations yang dihapus.");

            _db.LockLocations.RemoveRange(lockLocations);
            await _db.SaveChangesAsync();

            return Ok("Data LockLocations untuk siswa tersebut berhasil dihapus.");
        }
    }
}
