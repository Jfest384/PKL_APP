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
    [Route("chat")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly PklContext _db;
        private readonly ChatTemplateService _templateService;
        private readonly HttpClient _httpClient;

        public ChatController(PklContext db, ChatTemplateService templateService, IHttpClientFactory factory)
        {
            _db = db;
            _templateService = templateService;
            _httpClient = factory.CreateClient("waha");
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

        [HttpGet("default-chats")]
        public IActionResult GetDefaultChat()
        {
            // Ambil semua DefaultChat beserta relasi ChatService dan ChatContact
            var chats = _db.DefaultChats
                .Include(dc => dc.ChatService)
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
                        serviceName = first.ChatService.service_name,
                        contactId = group.Select(x => x.ChatContactid).ToList(),
                        contactName = group.Select(x => x.contact_name).ToList()
                    };
                })
                .ToList();

            return Ok(result);
        }

        [HttpGet("detail-default-chat")]
        public async Task<IActionResult> GetDetailDefaultChat([FromQuery] string contactId)
        {
            var defaultChats = await _db.DefaultChats
                .Include(dc => dc.ChatService)
                .Where(dc => dc.ChatContactid == contactId)
                .ToListAsync();

            if (defaultChats == null || defaultChats.Count == 0)
                return NotFound("Default chat tidak ditemukan.");

            var result = new List<object>();

            foreach (var defaultChat in defaultChats)
            {
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
                else templateContent = MessageTemplates.GetTemplate(templateId);

                result.Add(new
                {
                    contactId = defaultChat.ChatContactid,
                    contactName = defaultChat.contact_name,
                    serviceId = defaultChat.ChatServiceid,
                    serviceName = defaultChat.ChatService.service_name,
                    template = templateContent
                });
            }

            return Ok(result);
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
            if (dto == null)
                return BadRequest("Request body kosong.");

            if (dto.ChatServiceid <= 0)
                return BadRequest("ChatServiceid tidak valid.");

            if (dto.ChatContactid == null || dto.ContactName == null)
                return BadRequest("ChatContactid dan ContactName wajib diisi.");

            if (!dto.ChatContactid.Any() || !dto.ContactName.Any())
                return BadRequest("ChatContactid dan ContactName tidak boleh kosong.");

            if (dto.ChatContactid.Count != dto.ContactName.Count)
                return BadRequest("Jumlah ChatContactid dan ContactName harus sama.");

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("User ID tidak ditemukan di token.");

            var user = await _db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.id == userId);

            if (user == null)
                return Unauthorized("User tidak ditemukan.");

            var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
            if (!roleIds.Contains(1) && !roleIds.Contains(4))
                return Forbid("Hanya role Admin / Kepala Jurusan yang diizinkan.");

            bool existing = await _db.DefaultChats
                .AnyAsync(dc => dc.ChatServiceid == dto.ChatServiceid);

            if (existing)
                return BadRequest("DefaultChat dengan ChatService yang sama sudah ada.");

            var newChats = new List<DefaultChat>();

            for (int i = 0; i < dto.ChatContactid.Count; i++)
            {
                var contactId = dto.ChatContactid[i];
                var ContactName = dto.ContactName[i];

                if (string.IsNullOrWhiteSpace(contactId) || string.IsNullOrWhiteSpace(ContactName))
                    continue;

                var dchat = new DefaultChat
                {
                    ChatServiceid = dto.ChatServiceid,
                    ChatContactid = contactId,
                    contact_name = ContactName
                };

                _db.DefaultChats.Add(dchat);
                newChats.Add(dchat);
            }

            if (!newChats.Any())
                return BadRequest("Tidak ada data valid yang disimpan.");

            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = "Default chat berhasil ditambahkan.",
                data = newChats.Select(d => new
                {
                    d.ChatServiceid,
                    d.ChatContactid,
                    d.contact_name
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

            // Hapus semua DefaultChat dengan ChatServiceid yang sama
            var oldChats = _db.DefaultChats.Where(dc => dc.ChatServiceid == dto.ChatServiceid).ToList();
            if (oldChats.Any())
            {
                _db.DefaultChats.RemoveRange(oldChats);
                await _db.SaveChangesAsync();
            }

            // Tambahkan DefaultChat baru sesuai kontak yang dipilih
            var newChats = new List<DefaultChat>();
            for (int i = 0; i < dto.ChatContactid.Count; i++)
            {
                var contactId = dto.ChatContactid[i];
                var ContactName = dto.ContactName[i];

                if (string.IsNullOrWhiteSpace(contactId) || string.IsNullOrWhiteSpace(ContactName))
                    continue;

                var dchat = new DefaultChat
                {
                    ChatServiceid = dto.ChatServiceid,
                    ChatContactid = contactId,
                    contact_name = ContactName
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
                    dchat.ChatContactid,
                    dchat.contact_name
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
    }
}
