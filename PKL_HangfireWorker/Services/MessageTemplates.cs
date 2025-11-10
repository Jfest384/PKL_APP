using PKL_HangfireWorker.MessageTemplateModel;

namespace PKL_HangfireWorker.Services
{
    public static class MessageTemplates
    {
        private static readonly Dictionary<int, MessageTemplate> _templates = new()
    {
        {
            1,
            new MessageTemplate
            {
                Id = 1,
                Name = "Rekap Presensi Harian",
                Content = @"📊  Rekap Presensi PKL - {className}

🗓  {date}
🕓  {time} WIB


👨‍💻 Daftar Kehadiran :
{studentList}


💡 Keterangan :
✅ = Sudah mengisi presensi dan report harian
⚠ = Sudah presensi, tapi belum isi report harian
❌ = Belum mengisi presensi


Harap segera melakukan pengisian presensi dan report harian bagi yang belum."
            }
        },
        {
            2,
            new MessageTemplate
            {
                Id = 5,
                Name = "Test",
                Content = "Test!"
            }
        }
    };

        public static MessageTemplate? GetTemplate(int id)
        {
            _templates.TryGetValue(id, out var template);
            return template;
        }

        public static string FormatTemplate(int id, Dictionary<string, string> values)
        {
            var template = GetTemplate(id);
            if (template == null) return "";

            string content = template.Content;
            foreach (var pair in values)
            {
                content = content.Replace("{" + pair.Key + "}", pair.Value);
            }
            return content;
        }
    }
}
