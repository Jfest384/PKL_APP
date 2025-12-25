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
                query = query.Where(c => c.name.Contains(name));

            var companies = query
                .Select(c => new
                {
                    c.id,
                    c.name
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

        [HttpGet("company-locations")]
        public async Task<IActionResult> GetAllCompanyLocation([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? name = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.CompanyLocations.AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(c => c.LocationName.Contains(name));

            var companies = query
                .Select(c => new
                {
                    c.id,
                    c.LocationName
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
                    c.name
                })
                .FirstOrDefaultAsync();

            if (company == null)
                return NotFound("Company not found.");

            var locations = await _db.CompanyLocations
                .Where(l => l.Companyid == companyId)
                .Select(l => new
                {
                    l.id,
                    l.Companyid,
                    l.LocationName,
                    l.address,
                    l.lat,
                    l.longitude,
                    l.radius_meter,
                    l.is_active
                })
                .ToListAsync();

            return Ok(new
            {
                company,
                locations
            });
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
                name = dto.name
            };

            _db.Companies.Add(company);
            await _db.SaveChangesAsync();

            var companyLocation = new CompanyLocation
            {
                Companyid = company.id,
                LocationName = dto.name,
                address = dto.address,
                radius_meter = 500,
                is_active = true
            };

            if (dto.Lat.HasValue)
                companyLocation.lat = Math.Round(dto.Lat.Value, 12, MidpointRounding.AwayFromZero);
            if (dto.Long.HasValue)
                companyLocation.longitude = Math.Round(dto.Long.Value, 12, MidpointRounding.AwayFromZero);

            _db.CompanyLocations.Add(companyLocation);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Company berhasil ditambahkan.",
                data = new
                {
                    company.id,
                    company.name,
                    companyLocation.address,
                    companyLocation.lat,
                    companyLocation.longitude
                }
            });
        }

        [Authorize]
        [HttpPost("company-location/add")]
        public async Task<IActionResult> AddCompanyLocation([FromBody] CompanyLocationDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.name) || string.IsNullOrWhiteSpace(dto.address))
                return BadRequest("Invalid request body.");

            var companyLocation = new CompanyLocation
            {
                Companyid = dto.Companyid,
                LocationName = dto.name,
                address = dto.address,
                radius_meter = 500,
                is_active = true
            };

            if (dto.Lat.HasValue)
                companyLocation.lat = Math.Round(dto.Lat.Value, 12, MidpointRounding.AwayFromZero);
            if (dto.Long.HasValue)
                companyLocation.longitude = Math.Round(dto.Long.Value, 12, MidpointRounding.AwayFromZero);

            _db.CompanyLocations.Add(companyLocation);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Company berhasil ditambahkan.",
                data = new
                {
                    companyLocation.id,
                    companyLocation.LocationName,
                    companyLocation.address,
                    companyLocation.lat,
                    companyLocation.longitude
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

            var companyLocations = await _db.CompanyLocations
                .Where(cl => cl.Companyid == companyId)
                .ToListAsync();
            var companyLocationIds = companyLocations.Select(cl => cl.id).ToList();

            var studentsWithLocation = await _db.Students
                .Where(s => s.CompanyLocationid.HasValue && companyLocationIds.Contains(s.CompanyLocationid.Value))
                .ToListAsync();

            foreach (var student in studentsWithLocation)
            {
                student.CompanyLocationid = null;
            }

            var students = await _db.Students.Where(s => s.Companyid == companyId).ToListAsync();
            foreach (var student in students)
            {
                student.Companyid = null;
            }

            if (companyLocations.Count > 0)
                _db.CompanyLocations.RemoveRange(companyLocations);
            _db.Companies.Remove(company);

            await _db.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                message = "Company dan semua lokasi terkait berhasil dihapus."
            });
        }

        [Authorize]
        [HttpDelete("company-location/delete")]
        public async Task<IActionResult> DeleteCompanyLocation([FromBody] int companyLocationId)
        {
            if (companyLocationId <= 0)
                return BadRequest("Invalid companyId.");

            var companyLocation = await _db.CompanyLocations.FindAsync(companyLocationId);
            if (companyLocation == null)
                return NotFound("Company not found.");

            var students = await _db.Students.Where(s => s.CompanyLocationid == companyLocationId).ToListAsync();
            foreach (var student in students)
            {
                student.Companyid = null;
            }

            _db.CompanyLocations.Remove(companyLocation);
            await _db.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                message = "Lokasi Company berhasil dihapus."
            });
        }

        [Authorize]
        [HttpPut("companies/{companyId}")]
        public async Task<IActionResult> EditCompany(int companyId, [FromBody] CompanyEditDTO dto)
        {
            if (companyId <= 0)
                return BadRequest("companyId is required and must be greater than 0.");

            var company = await _db.Companies.FindAsync(companyId);
            if (company == null)
                return NotFound("Company not found.");

            if (string.IsNullOrWhiteSpace(dto.name))
                return BadRequest("Company name is required.");

            company.name = dto.name;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Company updated successfully." });
        }

        [Authorize]
        [HttpPut("company-location/{companyLocationId}")]
        public async Task<IActionResult> EditCompanyLocation(int companyLocationId, [FromBody] CompanyLocationEditDTO dto)
        {
            if (companyLocationId <= 0)
                return BadRequest("companyLocationId is required and must be greater than 0.");

            var companyLocation = await _db.CompanyLocations.FindAsync(companyLocationId);
            if (companyLocation == null)
                return NotFound("Company not found.");

            if (string.IsNullOrWhiteSpace(dto.name) && string.IsNullOrWhiteSpace(dto.address))
                return BadRequest("Company name and address is required.");

            companyLocation.LocationName = dto.name;
            companyLocation.address = dto.address;
            if (dto.Lat.HasValue)
                companyLocation.lat = Math.Round(dto.Lat.Value, 12, MidpointRounding.AwayFromZero);
            if (dto.Long.HasValue)
                companyLocation.longitude = Math.Round(dto.Long.Value, 12, MidpointRounding.AwayFromZero);

            await _db.SaveChangesAsync();
            return Ok(new { message = "Company Location updated successfully." });
        }

        [Authorize]
        [HttpPut("company-location/status")]
        public async Task<IActionResult> EditStatusCompanyLocation([FromBody] CompanyLocationStatusDTO dto)
        {
            if (dto == null || dto.companyLocationId <= 0)
                return BadRequest("Invalid companyLocationId.");

            var companyLocation = await _db.CompanyLocations.FindAsync(dto.companyLocationId);
            if (companyLocation == null)
                return NotFound("CompanyLocation not found.");

            companyLocation.is_active = dto.value == 1;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Status Company Location berhasil diupdate.",
                companyLocationId = companyLocation.id,
                companyLocation.is_active
            });
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
