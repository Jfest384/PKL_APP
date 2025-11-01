using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace PKL_API.Helpers
{
    public class ChatTemplateService
    {
        private readonly PklContext _db;

        public ChatTemplateService(PklContext db)
        {
            _db = db;
        }

        public async Task<string> GenerateTemplate6Async(int contactId)
        {
            var classroom = await _db.Classrooms
                .FirstOrDefaultAsync(c => c.ChatContactid == contactId);

            if (classroom == null)
            {
                return MessageTemplates.FormatTemplate(6, new Dictionary<string, string>
                {
                    { "className", "-" },
                    { "date", DateTime.Now.ToString("dddd, dd MMMM yyyy", new CultureInfo("id-ID")) },
                    { "time", DateTime.Now.ToString("HH.mm") },
                    { "studentList", "Tidak ditemukan kelas dengan contactId tersebut." }
                });
            }

            var classId = classroom.id;
            var students = await (from s in _db.Students
                                  join u in _db.Users on s.Userid equals u.id
                                  where s.Classroomid == classId
                                  select new
                                  {
                                      s.id,
                                      u.fullname,
                                      s.StudentValidationid
                                  }).ToListAsync();

            if (!students.Any())
            {
                return Helpers.MessageTemplates.FormatTemplate(6, new Dictionary<string, string>
                {
                    { "className", classroom.name },
                    { "date", DateTime.Now.ToString("dddd, dd MMMM yyyy", new CultureInfo("id-ID")) },
                    { "time", DateTime.Now.ToString("HH.mm") },
                    { "studentList", "Tidak ada siswa pada kelas ini." }
                });
            }

            // Ambil data validasi presensi & report
            var validations = await _db.StudentValidations
                .ToDictionaryAsync(v => v.id, v => new { v.isPresence, v.isDailyReport });

            var list = new List<string>();
            int i = 1;

            foreach (var student in students)
            {
                if (!validations.TryGetValue(student.StudentValidationid, out var val))
                    continue;

                string status = val.isPresence && val.isDailyReport ? "✅"
                              : val.isPresence ? "⚠"
                              : "❌";

                list.Add($"{i}. {student.fullname}  {status}");
                i++;
            }

            string date = DateTime.Now.ToString("dddd, dd MMMM yyyy", new CultureInfo("id-ID"));
            string time = DateTime.Now.ToString("HH.mm");

            string text = MessageTemplates.FormatTemplate(6, new Dictionary<string, string>
            {
                { "className", classroom.name },
                { "date", date },
                { "time", time },
                { "studentList", string.Join("\n", list) }
            });

            return text.Trim();
        }
    }
}
