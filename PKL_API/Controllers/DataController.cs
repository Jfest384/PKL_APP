using Microsoft.AspNetCore.Mvc;

namespace PKL_API.Controllers
{
    [Route("api/data")]
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

        [HttpGet("companies")]
        public IActionResult GetCompaniesData([FromQuery] string? search)
        {
            var query = _db.Companies.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.name.Contains(search));
            }

            var companies = query
                .Select(c => new
                {
                    c.id,
                    c.name,
                    address = string.IsNullOrEmpty(c.address) ? "-" : c.address,
                    phone = string.IsNullOrEmpty(c.phone) ? "-" : c.phone
                })
                .ToList();
            return Ok(companies);
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
    }
}
