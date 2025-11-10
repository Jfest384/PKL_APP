using PKL_API.Models;

namespace PKL_API.Helpers
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


[COMMON]💡 Keterangan :
✅ = Sudah mengisi presensi dan report harian
⚠ = Sudah presensi, tapi belum isi report harian
❌ = Belum mengisi presensi


Harap segera melakukan pengisian presensi dan report harian bagi yang belum.[/COMMON]"
            }
        },
        {
            2,
            new MessageTemplate
            {
                Id = 2,
                Name = "Reminder Presensi",
                Content = @"[COMMON]‼ REMINDER ‼\n\nHarap segera melakukan pengisian presensi dan report harian bagi yang belum.[/COMMON]"
            }
        },
        {
            3,
            new MessageTemplate
            {
                Id = 3,
                Name = "Reminder Report",
                Content = @"[COMMON]‼ REMINDER ‼\n\nJika hari ini melakukan bimbingan laporan\ningat untuk mengisi presensi bimbingan laporan dan mengupload file laporan PKL nya.[/COMMON]"
            }
        },
        {
            4,
            new MessageTemplate
            {
                Id = 4,
                Name = "Reminder Training",
                Content = @"[COMMON]‼ REMINDER ‼

Ingat hari ini untuk belajar persiapan UKK dan Sertifikasi.[/COMMON]"
            }
        },
        {
            5,
            new MessageTemplate
            {
                Id = 5,
                Name = "Test",
                Content = "Test!"
            }
        },
        {
            6,
            new MessageTemplate
            {
                Id = 6,
                Name = "Rekap Presensi (Dari Database)",
                Content = @"📊  Rekap Presensi PKL - {className}

🗓  {date}
🕓  {time} WIB


👨‍💻 Daftar Kehadiran :
{studentList}


[COMMON]💡 Keterangan :
✅ = Sudah mengisi presensi dan report harian
⚠ = Sudah presensi, tapi belum isi report harian
❌ = Belum mengisi presensi


Harap segera melakukan pengisian presensi dan report harian bagi yang belum.[/COMMON]"
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
