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
            var defaultChats = await _db.DefaultChats
                .Include(dc => dc.ChatService)
                .Include(dc => dc.ChatContact)
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
                else
                {
                    templateContent = MessageTemplates.GetTemplate(templateId);
                }

                result.Add(new
                {
                    contactId = defaultChat.ChatContactid,
                    chat_name = defaultChat.ChatContact.chat_name,
                    id_chat = defaultChat.ChatContact.id_chat,
                    serviceId = defaultChat.ChatServiceid,
                    service_name = defaultChat.ChatService.service_name,
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
            if (dto == null || dto.ChatServiceid <= 0 || dto.ChatContactid == null || !dto.ChatContactid.Any())
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

            var existing = await _db.DefaultChats
                .AnyAsync(dc => dc.ChatServiceid == dto.ChatServiceid);

            if (existing)
            {
                return BadRequest("DefaultChat dengan service yang sama sudah ada.");
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
    }
}
