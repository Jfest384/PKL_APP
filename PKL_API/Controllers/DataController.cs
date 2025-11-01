using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PKL_API.Helpers;
using PKL_API.Models;
using PKL_API.Models.DTO;
using System.Text;
using System.Text.Json;

namespace PKL_API.Controllers
{
    [Route("data")]
    [ApiController]
    public class DataController : ControllerBase
    {
        private readonly PklContext _db;
        private readonly ChatTemplateService _templateService;
        private readonly HttpClient _httpClient;

        public DataController(PklContext db, ChatTemplateService templateService, IHttpClientFactory factory)
        {
            _db = db;
            _templateService = templateService;
            _httpClient = factory.CreateClient("waha");
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

        [HttpGet("chat-services")]
        public IActionResult GetChatService()
        {
            var services = _db.ChatServices
                .Select(c => new
                {
                    c.id,
                    c.service_name
                })
                .ToList();
            return Ok(services);
        }

        [HttpGet("chat-contacts")]
        public IActionResult GetChatContact()
        {
            var contacts = _db.ChatContacts
                .Select(c => new
                {
                    c.id,
                    c.id_chat,
                    c.chat_name
                })
                .ToList();
            return Ok(contacts);
        }

        [HttpGet("default-chats")]
        public IActionResult GetDefaultChat()
        {
            // Ambil semua DefaultChat beserta relasi ChatService dan ChatContact
            var chats = _db.DefaultChats
                .Include(dc => dc.ChatService)
                .Include(dc => dc.ChatContact)
                .ToList();

            var result = chats
                .GroupBy(dc => dc.ChatService.service_name)
                .Select(group =>
                {
                    var first = group.First();
                    return new
                    {
                        id = first.ChatService.id,
                        serviceId = first.ChatServiceid,
                        service_name = first.ChatService.service_name,
                        contactId = group.Select(x => x.ChatContactid).ToList(),
                        id_chat = group.Select(x => x.ChatContact.id_chat).ToList(),
                        chat_name = group.Select(x => x.ChatContact.chat_name).ToList()
                    };
                })
                .ToList();

            return Ok(result);
        }

        [HttpGet("detail-default-chat")]
        public async Task<IActionResult> GetDetailDefaultChat([FromQuery] int contactId)
        {
            // Cari DefaultChat berdasarkan contactId
            var defaultChat = await _db.DefaultChats
                .Include(dc => dc.ChatService)
                .Include(dc => dc.ChatContact)
                .FirstOrDefaultAsync(dc => dc.ChatContactid == contactId);

            if (defaultChat == null)
                return NotFound("Default chat tidak ditemukan.");

            var templateId = defaultChat.ChatService.MessageTemplateId;
            object? templateContent;

            if (templateId == 6)
            {
                string dynamicContent = await _templateService.GenerateTemplate6Async(contactId);
                templateContent = new
                {
                    id = 6,
                    name = "Rekap Presensi Otomatis",
                    content = dynamicContent
                };
            }
            else
            {
                templateContent = MessageTemplates.GetTemplate(templateId);
            }

            // Response lengkap
            return Ok(new
            {
                contactId = defaultChat.ChatContactid,
                chat_name = defaultChat.ChatContact.chat_name,
                id_chat = defaultChat.ChatContact.id_chat,
                serviceId = defaultChat.ChatServiceid,
                service_name = defaultChat.ChatService.service_name,
                template = templateContent
            });
        }

        [HttpPost("default-chats/test-send")]
        public async Task<IActionResult> SendTestMessage([FromBody] TestSendRequest request)
        {
            // pastikan data dikirim dari FE
            if (string.IsNullOrWhiteSpace(request.ChatId))
                return BadRequest("ChatId tidak boleh kosong.");

            // ambil template id = 5 dari static class
            var template = MessageTemplates.GetTemplate(5);
            if (template == null)
                return NotFound("Template ID 5 tidak ditemukan.");

            // isi pesan
            string messageText = template.Content;

            // payload untuk dikirim ke WA gateway / API eksternal
            var payload = new
            {
                chatId = request.ChatId,
                reply_to = (string?)null,
                text = messageText,
                linkPreview = true,
                linkPreviewHighQuality = false,
                session = "default"
            };

            // kirim ke endpoint API WA
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("sendText", content);
            var respContent = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Gagal mengirim pesan test.");

            return Ok(new
            {
                message = "Pesan template 5 berhasil dikirim.",
                chatId = request.ChatId,
                content = messageText
            });
        }


        [Authorize]
        [HttpPost("default-chats/add")]
        public async Task<IActionResult> AddDefaultChat([FromBody] DefaultChatDTO dto)
        {
            // Validasi input
            if (dto == null || dto.ChatServiceid <= 0 || dto.ChatContactid == null || !dto.ChatContactid.Any())
                return BadRequest("Invalid request body.");

            // Ambil user dari token
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("User ID not found in token.");

            // Ambil role user
            var user = await _db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.id == userId);

            if (user == null)
                return Unauthorized("User not found.");

            var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
            if (!roleIds.Contains(1) && !roleIds.Contains(4))
                return Forbid("Hanya roleId 1/4 yang bisa melakukan aksi ini.");

            var oldChats = _db.DefaultChats.ToList();
            if (oldChats.Any())
            {
                _db.DefaultChats.RemoveRange(oldChats);
                await _db.SaveChangesAsync();
            }

            var newdchat = new List<DefaultChat>();
            foreach (var contactId in dto.ChatContactid)
            {
                var dchat = new DefaultChat
                {
                    ChatServiceid = dto.ChatServiceid,
                    ChatContactid = contactId
                };
                _db.DefaultChats.Add(dchat);
                newdchat.Add(dchat);
            }
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Default chat berhasil ditambahkan.",
                data = newdchat.Select(dchat => new
                {
                    dchat.ChatServiceid,
                    dchat.ChatContactid
                }).ToList()
            });
        }

        [Authorize]
        [HttpPut("default-chats/{defaultChatId}")]
        public async Task<IActionResult> EditDefaultChat(int defaultChatId, [FromBody] DefaultChatEditDTO dto)
        {
            // Validasi input
            if (dto == null || dto.ChatServiceid <= 0 || dto.ChatContactid == null || !dto.ChatContactid.Any())
                return BadRequest("ChatServiceid dan ChatContactid wajib diisi.");

            // Ambil user dari token
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("User ID not found in token.");

            // Ambil role user
            var user = await _db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.id == userId);

            if (user == null)
                return Unauthorized("User not found.");

            var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
            if (!roleIds.Contains(1) && !roleIds.Contains(4))
                return Forbid("Hanya roleId 1/4 yang bisa melakukan aksi ini.");

            // Hapus semua DefaultChat dengan ChatServiceid yang sama
            var oldChats = _db.DefaultChats.Where(dc => dc.ChatServiceid == dto.ChatServiceid).ToList();
            if (oldChats.Any())
            {
                _db.DefaultChats.RemoveRange(oldChats);
                await _db.SaveChangesAsync();
            }

            // Tambahkan DefaultChat baru sesuai kontak yang dipilih
            var newChats = new List<DefaultChat>();
            foreach (var contactId in dto.ChatContactid)
            {
                var dchat = new DefaultChat
                {
                    ChatServiceid = dto.ChatServiceid,
                    ChatContactid = contactId
                };
                _db.DefaultChats.Add(dchat);
                newChats.Add(dchat);
            }
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Default chat berhasil diubah.",
                data = newChats.Select(dchat => new
                {
                    dchat.ChatServiceid,
                    dchat.ChatContactid
                }).ToList()
            });
        }

        [Authorize]
        [HttpDelete("default-chats/delete")]
        public async Task<IActionResult> DeleteDefaultChat([FromBody] int chatServiceId)
        {
            // Validasi input
            if (chatServiceId <= 0)
                return BadRequest("Invalid defaultChatId.");

            // Ambil user dari token
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("User ID not found in token.");

            // Ambil role user
            var user = await _db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.id == userId);

            if (user == null)
                return Unauthorized("User not found.");

            var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
            if (!roleIds.Contains(1) && !roleIds.Contains(4))
                return Forbid("Hanya roleId 1/4 yang bisa melakukan aksi ini.");

            // Cari defaultChat
            var oldChats = _db.DefaultChats.Where(dc => dc.ChatServiceid == chatServiceId).ToList();
            if (!oldChats.Any())
                return NotFound("Default chat not found.");

            _db.DefaultChats.RemoveRange(oldChats);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Default chat berhasil dihapus."
            });
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
                    address = string.IsNullOrEmpty(c.address) ? "-" : c.address,
                    phone = string.IsNullOrEmpty(c.phone) ? "-" : c.phone,
                    //lat = c.lat.ToString()
                    //lat = presence?.Detail?.lat.ToString() ?? "-",
                    //longitude = presence?.Detail?.longitude?.ToString() ?? "-",
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

        [Authorize]
        [HttpPost("companies/add")]
        public async Task<IActionResult> AddCompany([FromBody] CompanyDTO dto)
        {
            // Validasi input
            if (dto == null || string.IsNullOrWhiteSpace(dto.name) || string.IsNullOrWhiteSpace(dto.address))
                return BadRequest("Invalid request body.");

            // Ambil user dari token
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("User ID not found in token.");

            // Ambil role user
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
                company.lat = Math.Round(dto.Lat.Value, 7, MidpointRounding.AwayFromZero);
            if (dto.Long.HasValue)
                company.longitude = Math.Round(dto.Long.Value, 7, MidpointRounding.AwayFromZero);

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
            // Validasi input
            if (companyId <= 0)
                return BadRequest("Invalid companyId.");

            // Ambil user dari token
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("User ID not found in token.");

            // Ambil role user
            var user = await _db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.id == userId);

            if (user == null)
                return Unauthorized("User not found.");

            var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
            if (!roleIds.Contains(1) && !roleIds.Contains(4))
                return Forbid("Hanya roleId 1/4 yang bisa melakukan aksi ini.");

            // Cari company
            var company = await _db.Companies.FindAsync(companyId);
            if (company == null)
                return NotFound("Company not found.");

            // Set id_company di tabel Student menjadi null
            var students = await _db.Students.Where(s => s.Companyid == companyId).ToListAsync();
            foreach (var student in students)
            {
                student.Companyid = null;
            }

            // Hapus company
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
        public async Task<IActionResult> EditClassroom(int companyId, [FromBody] CompanyEditDTO dto)
        {
            // Validasi classId
            if (companyId <= 0)
                return BadRequest("companyId is required and must be greater than 0.");

            // Ambil user id dari claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null)
                return Unauthorized("User ID not found in token.");

            int userId = int.Parse(userIdClaim.Value);

            // Ambil role user
            var userRole = await _db.Roles
                .Where(r => _db.UserRoles.Any(ur => ur.User.id == userId && ur.Role.id == r.id))
                .Select(r => r.id)
                .FirstOrDefaultAsync();

            if (userRole != 1 && userRole != 4)
                return StatusCode(403, "Only admin and kepala jurusan can edit a company.");

            // Cari company
            var company = await _db.Companies.FindAsync(companyId);
            if (company == null)
                return NotFound("Company not found.");

            // Validasi input
            if (string.IsNullOrWhiteSpace(dto.name) && string.IsNullOrWhiteSpace(dto.address))
                return BadRequest("Company name and address is required.");

            // Update data company
            company.name = dto.name;
            company.address = dto.address;
            if (dto.Lat.HasValue)
                company.lat = Math.Round(dto.Lat.Value, 7, MidpointRounding.AwayFromZero);
            if (dto.Long.HasValue)
                company.longitude = Math.Round(dto.Long.Value, 7, MidpointRounding.AwayFromZero);

            await _db.SaveChangesAsync();

            return Ok(new { message = "Company updated successfully." });
        }
    }
}
