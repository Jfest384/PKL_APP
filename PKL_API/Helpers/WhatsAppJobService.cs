using Dapper;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PKL_API.Helpers
{
    public class WhatsAppJobService
    {
        private readonly string _connectionString;
        private readonly HttpClient _httpClient;

        public WhatsAppJobService(IConfiguration config, IHttpClientFactory factory)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(_connectionString))
                throw new InvalidOperationException("Connection string is null!");
            _httpClient = factory.CreateClient("waha");
        }

        public async Task ExecuteAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            var students = (await conn.QueryAsync(@"
                SELECT 
                  s.id, 
                  u.fullname, 
                  s.id_validation, 
                  s.id_class, 
                  c.name AS className, 
                  c.id_contact,
                  co.id_chat
                FROM Students s
                JOIN Users u ON s.id_user = u.id
                JOIN Classrooms c ON s.id_class = c.id
                JOIN ChatContacts co ON c.id_contact = co.id
                WHERE s.id_class IN (6, 9)
            ")).ToList();

            if (!students.Any()) return;

            var validations = (await conn.QueryAsync(@"
                SELECT id, isPresence, isDailyReport
                FROM StudentValidations
            ")).ToDictionary(v => (int)v.id, v => v);

            var groups = students.GroupBy(s => new { s.id_class, s.className, s.id_contact, s.id_chat });

            foreach (var group in groups)
            {
                var list = new List<string>();
                int i = 1;

                foreach (var student in group)
                {
                    if (!validations.TryGetValue((int)student.id_validation, out var val))
                        continue;

                    string status = "❌";
                    if (val.isPresence && val.isDailyReport) status = "✅";
                    else if (val.isPresence && !val.isDailyReport) status = "⚠";

                    list.Add($"{i}. {student.fullname}  {status}");
                    i++;
                }

                string date = DateTime.Now.ToString("dddd, dd MMMM yyyy", new CultureInfo("id-ID"));
                string time = DateTime.Now.ToString("HH.mm");

                // 🧠 Gunakan template ID = 1
                string text = MessageTemplates.FormatTemplate(1, new Dictionary<string, string>
                {
                    { "className", group.Key.className },
                    { "date", date },
                    { "time", time },
                    { "studentList", string.Join("\n", list) }
                });

                string chatId = group.Key.id_chat ?? "-";
                if (!string.IsNullOrWhiteSpace(chatId))
                {
                    await SendMessage(chatId, text);
                    string followUp = MessageTemplates.FormatTemplate(2, new());
                    await SendMessage(chatId, followUp);
                }
            }
        }

        private async Task SendMessage(string chatId, string text)
        {
            var body = new
            {
                chatId = chatId,
                reply_to = (string?)null,
                text = text,
                linkPreview = true,
                linkPreviewHighQuality = false,
                session = "default"
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("sendText", content);
            var respContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine(respContent); // debug response server
            response.EnsureSuccessStatusCode();
        }
    }
}
