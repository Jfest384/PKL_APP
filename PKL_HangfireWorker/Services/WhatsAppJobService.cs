using Dapper;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PKL_HangfireWorker.Services
{
    internal class WhatsAppJobService
    {
        private readonly string _connectionString;
        private readonly HttpClient _httpClient;

        public WhatsAppJobService(IConfiguration config, IHttpClientFactory factory)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string is null!");
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
                JOIN StudentValidations sv ON s.id_validation = sv.id AND sv.isPKL = 1
                JOIN Users u ON s.id_user = u.id
                LEFT JOIN Classrooms c ON s.id_class = c.id
                LEFT JOIN ChatContacts co ON c.id_contact = co.id
            ")).ToList();

            if (!students.Any()) return;

            var validations = (await conn.QueryAsync(@"
                SELECT id, isPresence, isDailyReport
                FROM StudentValidations
            ")).ToDictionary(v => (int)v.id, v => v);

            var groups = students.GroupBy(s => new { s.id_class, s.className, s.id_contact, s.id_chat });

            foreach (var group in groups)
            {
                // Jika class tidak punya id_contact atau chatId tidak tersedia, lewati pengiriman
                if (group.Key.id_contact == null) continue;
                var chatIdCandidate = group.Key.id_chat ?? "-";
                if (string.IsNullOrWhiteSpace(chatIdCandidate) || chatIdCandidate == "-") continue;

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

                if (list.Count == 0) continue;

                string date = DateTime.Now.ToString("dddd, dd MMMM yyyy", new CultureInfo("id-ID"));
                string time = DateTime.Now.ToString("HH.mm");

                string text = MessageTemplates.FormatTemplate(1, new Dictionary<string, string>
                {
                    { "className", group.Key.className },
                    { "date", date },
                    { "time", time },
                    { "studentList", string.Join("\n", list) }
                });

                string chatId = group.Key.id_chat ?? "-";
                if (!string.IsNullOrWhiteSpace(chatId) && chatId != "-")
                    await SendMessage(chatId, text);
            }
        }

        private async Task SendMessage(string chatId, string text)
        {
            var body = new
            {
                chatId = chatId,
                text = text,
                session = "default"
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("sendText", content);

            Console.WriteLine(await response.Content.ReadAsStringAsync());
            response.EnsureSuccessStatusCode();
        }
    }
}