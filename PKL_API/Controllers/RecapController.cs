using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PKL_API.Helpers;
using PKL_API.Models;
using PKL_API.Models.DTO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PKL_API.Controllers
{
    [Route("recap")]
    [ApiController]
    public class RecapController : ControllerBase
    {
        private readonly PklContext _db;
        public RecapController(PklContext db)
        {
            _db = db;
        }

        private async Task<(string studentName, string kelas, string mentorName, string walasName)> GetStudentRelatedInfoAsync(Student student)
        {
            var studentUser = await _db.Users.FirstOrDefaultAsync(u => u.id == student.Userid);
            string studentName = studentUser?.fullname ?? "-";

            var classroom = await _db.Classrooms.FirstOrDefaultAsync(c => c.id == student.Classroomid);
            string kelas = classroom?.name ?? "-";

            string mentorName = "-";
            if (student.Mentorid.HasValue)
            {
                var mentor = await _db.Mentors.FirstOrDefaultAsync(m => m.id == student.Mentorid.Value);
                if (mentor != null)
                {
                    var mentorUser = await _db.Users.FirstOrDefaultAsync(u => u.id == mentor.Userid);
                    mentorName = mentorUser?.fullname ?? "-";
                }
            }

            string walasName = "-";
            if (classroom != null)
            {
                var walas = await _db.WaliKelas.FirstOrDefaultAsync(w => w.id == classroom.WaliKelasid);
                if (walas != null)
                {
                    var walasUser = await _db.Users.FirstOrDefaultAsync(u => u.id == walas.Userid);
                    walasName = walasUser?.fullname ?? "-";
                }
            }

            return (studentName, kelas, mentorName, walasName);
        }


        [HttpPost("publish")]
        public async Task<IActionResult> Publish([FromBody] RecapDTO request)
        {
            if (request.StudentIds == null || request.StudentIds.Count == 0)
                return BadRequest("StudentIds is required.");
            if (request.Date == default)
                return BadRequest("Date is required.");

            var startDate = new DateOnly(2025, 8, 15);

            // Tanggal batas
            var cutOff1 = new DateOnly(2025, 12, 19); // 19 Des 2025
            var cutOff2 = new DateOnly(2026, 1, 12);  // 12 Jan 2026

            var nowDate = DateOnly.FromDateTime(DateTime.Now);
            DateOnly endDate;
            Func<DateOnly, bool> dateFilter;

            if (nowDate <= cutOff1)
            {
                // 1. <= 19 Des 2025: normal
                endDate = nowDate;
                dateFilter = d => d >= startDate && d <= endDate;
            }
            else if (nowDate > cutOff1 && nowDate < cutOff2)
            {
                // 2. > 19 Des 2025 && < 12 Jan 2026: endDate = 19 Des 2025
                endDate = cutOff1;
                dateFilter = d => d >= startDate && d <= endDate;
            }
            else
            {
                // 3. >= 12 Jan 2026: endDate = now, exclude 20 Des 2025 - 11 Jan 2026
                endDate = nowDate;
                var excludeStart = cutOff1.AddDays(1); // 20 Des 2025
                var excludeEnd = cutOff2.AddDays(-1);  // 11 Jan 2026
                dateFilter = d =>
                    d >= startDate && d <= endDate &&
                    (d < excludeStart || d > excludeEnd);
            }

            var students = await _db.Students
                .Where(s => request.StudentIds.Contains(s.id))
                .ToListAsync();

            if (students.Count == 0)
                return NotFound("No students found.");

            var now = DateOnly.FromDateTime(DateTime.Now);
            var recapsToAdd = new List<PresenceRecap>();
            var reportRecapsToAdd = new List<ReportRecap>();
            var recapsToUpdate = new List<PresenceRecap>();
            var reportRecapsToUpdate = new List<ReportRecap>();

            foreach (var student in students)
            {
                var studentId = student.id;
                var nis = student.nis;

                // Hitung hari PKL (hanya hari kerja, dan sesuai filter tanggal)
                int pkl_days = 0;
                for (var dt = startDate; dt <= endDate; dt = dt.AddDays(1))
                {
                    if (!dateFilter(dt)) continue;
                    var dayOfWeek = dt.DayOfWeek;
                    if (dayOfWeek != DayOfWeek.Saturday && dayOfWeek != DayOfWeek.Sunday)
                        pkl_days++;
                }

                var presencesQuery = _db.Presences
                    .Where(p => p.Studentid == studentId && p.date >= startDate && p.date <= endDate)
                    .AsQueryable();

                if (nowDate > cutOff1 && nowDate >= cutOff2)
                {
                    // exclude 20 Des 2025 - 11 Jan 2026
                    var excludeStart = cutOff1.AddDays(1);
                    var excludeEnd = cutOff2.AddDays(-1);
                    presencesQuery = presencesQuery.Where(p => p.date < excludeStart || p.date > excludeEnd);
                }

                var presenceTotal = await presencesQuery.CountAsync();
                var absenTotal = pkl_days - presenceTotal;

                var presenceDetails = await presencesQuery
                    .Select(p => p.PresenceDetailid)
                    .ToListAsync();

                int sendTotal = 0, notSendTotal = 0;
                if (presenceDetails.Count > 0)
                {
                    var details = await _db.PresenceDetails
                        .Where(d => presenceDetails.Contains(d.id))
                        .ToListAsync();

                    sendTotal = await presencesQuery
                        .Join(_db.PresenceDetails,
                            p => p.PresenceDetailid,
                            d => d.id,
                            (p, d) => new { Presence = p, Detail = d })
                        .CountAsync(x =>
                            x.Detail.iscomplate == true
                            && ((x.Detail.update_at.HasValue &&
                                x.Detail.update_at.Value == x.Presence.date) || x.Detail.update_at == null)
                        );

                    notSendTotal = await presencesQuery
                        .Join(_db.PresenceDetails,
                            p => p.PresenceDetailid,
                            d => d.id,
                            (p, d) => new { Presence = p, Detail = d })
                        .CountAsync(x =>
                            x.Detail.iscomplate == false
                            || (x.Detail.update_at.HasValue &&
                                x.Detail.update_at.Value != x.Presence.date)
                        );
                }

                var typeCounts = await presencesQuery
                    .GroupBy(p => p.PresenceTypeid)
                    .Select(g => new { IdPresence = g.Key, Count = g.Count() })
                    .ToListAsync();

                int hadirCount = typeCounts.FirstOrDefault(x => x.IdPresence == 1)?.Count ?? 0;
                int sakitCount = typeCounts.FirstOrDefault(x => x.IdPresence == 2)?.Count ?? 0;
                int izinCount = typeCounts.FirstOrDefault(x => x.IdPresence == 3)?.Count ?? 0;
                int liburCount = typeCounts.FirstOrDefault(x => x.IdPresence == 4)?.Count ?? 0;
                int wfhCount = typeCounts.FirstOrDefault(x => x.IdPresence == 5)?.Count ?? 0;

                var existingPresenceRecap = await _db.PresenceRecaps
                    .FirstOrDefaultAsync(r => r.StudentId == studentId);

                if (existingPresenceRecap != null)
                {
                    existingPresenceRecap.nis = nis;
                    existingPresenceRecap.pkl_days = pkl_days;
                    existingPresenceRecap.presence_total = presenceTotal;
                    existingPresenceRecap.absen_total = absenTotal;
                    existingPresenceRecap.send_total = sendTotal;
                    existingPresenceRecap.not_send_total = notSendTotal;
                    existingPresenceRecap.hadir = hadirCount;
                    existingPresenceRecap.sakit = sakitCount;
                    existingPresenceRecap.izin = izinCount;
                    existingPresenceRecap.libur = liburCount;
                    existingPresenceRecap.wfh = wfhCount;
                    existingPresenceRecap.update_at = now;
                    recapsToUpdate.Add(existingPresenceRecap);
                }
                else
                {
                    recapsToAdd.Add(new PresenceRecap
                    {
                        StudentId = studentId,
                        nis = nis,
                        pkl_days = pkl_days,
                        presence_total = presenceTotal,
                        absen_total = absenTotal,
                        send_total = sendTotal,
                        not_send_total = notSendTotal,
                        hadir = hadirCount,
                        sakit = sakitCount,
                        izin = izinCount,
                        libur = liburCount,
                        wfh = wfhCount,
                        update_at = now
                    });
                }

                // Report Recap
                var reportsQuery = _db.Reports
                    .Where(r => r.Studentid == studentId && r.date >= startDate && r.date <= endDate)
                    .AsQueryable();

                if (nowDate > cutOff1 && nowDate >= cutOff2)
                {
                    var excludeStart = cutOff1.AddDays(1);
                    var excludeEnd = cutOff2.AddDays(-1);
                    reportsQuery = reportsQuery.Where(r => r.date < excludeStart || r.date > excludeEnd);
                }

                int totalWeeks = 0;
                var validDates = new List<DateOnly>();
                for (var dt = startDate; dt <= endDate; dt = dt.AddDays(1))
                {
                    if (dateFilter(dt))
                        validDates.Add(dt);
                }
                if (validDates.Count > 0)
                {
                    var minDate = validDates.Min();
                    var maxDate = validDates.Max();
                    totalWeeks = (int)Math.Ceiling((maxDate.DayNumber - minDate.DayNumber + 1) / 7.0);
                }

                var reportTotal = await reportsQuery.CountAsync();
                var existingReportRecap = await _db.ReportRecaps
                    .FirstOrDefaultAsync(r => r.StudentId == studentId);

                if (existingReportRecap != null)
                {
                    existingReportRecap.nis = nis;
                    existingReportRecap.total_weeks = totalWeeks;
                    existingReportRecap.report_total = reportTotal;
                    existingReportRecap.update_at = now;
                    reportRecapsToUpdate.Add(existingReportRecap);
                }
                else
                {
                    reportRecapsToAdd.Add(new ReportRecap
                    {
                        StudentId = studentId,
                        nis = nis,
                        total_weeks = totalWeeks,
                        report_total = reportTotal,
                        update_at = now
                    });
                }
            }

            if (recapsToAdd.Count > 0)
                await _db.PresenceRecaps.AddRangeAsync(recapsToAdd);
            if (reportRecapsToAdd.Count > 0)
                await _db.ReportRecaps.AddRangeAsync(reportRecapsToAdd);

            if (recapsToUpdate.Count > 0)
                _db.PresenceRecaps.UpdateRange(recapsToUpdate);
            if (reportRecapsToUpdate.Count > 0)
                _db.ReportRecaps.UpdateRange(reportRecapsToUpdate);

            await _db.SaveChangesAsync();
            return Ok(new
            {
                PresenceRecaps = recapsToAdd.Concat(recapsToUpdate).ToList(),
                ReportRecaps = reportRecapsToAdd.Concat(reportRecapsToUpdate).ToList(),
            });
        }

        [HttpPost("validation")]
        public async Task<IActionResult> ValidationRecap([FromBody] GetRecapRequest request)
        {
            if (request.StudentId <= 0)
                return BadRequest("StudentId is required.");

            var presenceExists = await _db.PresenceRecaps.AnyAsync(r => r.StudentId == request.StudentId);
            var reportExists = await _db.ReportRecaps.AnyAsync(r => r.StudentId == request.StudentId);

            if (presenceExists && reportExists)
                return Ok(new { result = "Yes" });
            else
                return Ok(new { result = "No" });
        }

        [HttpPost("presence")]
        public async Task<IActionResult> PostPresenceRecap([FromBody] GetRecapRequest request)
        {
            var student = await _db.Students.FirstOrDefaultAsync(s => s.id == request.StudentId);
            if (student == null)
                return NotFound("Student not found.");

            var recap = await _db.PresenceRecaps
                .Where(r => r.StudentId == request.StudentId)
                .OrderByDescending(r => r.update_at)
                .FirstOrDefaultAsync();
            if (recap == null)
                return NotFound("Presence recap not found.");

            var (studentName, kelas, mentorName, walasName) = await GetStudentRelatedInfoAsync(student);

            return Ok(new
            {
                studentName,
                recap.nis,
                kelas,
                mentorName,
                walasName,
                recap.pkl_days,
                recap.presence_total,
                recap.absen_total,
                recap.send_total,
                recap.not_send_total,
                recap.hadir,
                recap.sakit,
                recap.izin,
                recap.wfh,
                recap.libur
            });
        }

        [HttpPost("report")]
        public async Task<IActionResult> PostReportRecap([FromBody] GetRecapRequest request)
        {
            var student = await _db.Students.FirstOrDefaultAsync(s => s.id == request.StudentId);
            if (student == null)
                return NotFound("Student not found.");

            var recap = await _db.ReportRecaps
                .Where(r => r.StudentId == request.StudentId)
                .OrderByDescending(r => r.update_at)
                .FirstOrDefaultAsync();
            if (recap == null)
                return NotFound("Presence recap not found.");

            var (studentName, kelas, mentorName, walasName) = await GetStudentRelatedInfoAsync(student);

            return Ok(new
            {
                studentName,
                recap.nis,
                kelas,
                mentorName,
                walasName,
                recap.total_weeks,
                recap.report_total
            });
        }

        [HttpPost("report/photos")]
        public async Task<IActionResult> GetReportPhotos([FromBody] GetRecapRequest request)
        {
            if (request.StudentId <= 0)
                return BadRequest("StudentId is required.");

            var reportRecap = await _db.ReportRecaps
                .Where(r => r.StudentId == request.StudentId)
                .OrderByDescending(r => r.update_at)
                .FirstOrDefaultAsync();

            if (reportRecap == null)
                return Ok(new List<object>());

            var startDate = new DateOnly(2025, 8, 15);
            var endDate = reportRecap.update_at;

            var reportPhotoData = await _db.Reports
                .Where(r =>
                    r.Studentid == request.StudentId &&
                    r.ReportPhotoid != null &&
                    r.date >= startDate &&
                    r.date <= endDate
                )
                .Select(r => new { r.ReportPhotoid, r.date })
                .ToListAsync();

            if (reportPhotoData == null || reportPhotoData.Count == 0)
                return Ok(new List<object>());

            var photoIds = reportPhotoData.Select(x => x.ReportPhotoid).Distinct().ToList();
            var reportFiles = await _db.ReportFiles
                .Where(f => photoIds.Contains(f.id))
                .ToListAsync();

            var result = reportPhotoData.Select(item =>
            {
                var file = reportFiles.FirstOrDefault(f => f.id == item.ReportPhotoid);
                return new
                {
                    photoId = item.ReportPhotoid,
                    extension = file?.extension ?? "-",
                    url = $"/api/recap/report/photo/{item.ReportPhotoid}",
                    item.date
                };
            }).ToList();

            return Ok(result);
        }

        [Authorize]
        [HttpGet("report/photo/{id}")]
        public async Task<IActionResult> GetPhoto(Guid id)
        {
            var photo = await _db.ReportFiles.FindAsync(id);
            if (photo == null)
                return NotFound();

            var ext = photo.extension.Trim().ToLower();
            var contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };

            return File(photo.files, contentType);
        }

        [Authorize]
        [HttpPost("print")]
        public async Task<IActionResult> PrintRecap(
            [FromBody] GetRecapRequest request,
            [FromServices] IConfiguration config,
            [FromServices] IIdEncryptionService idEncrypt)
        {
            var student = await _db.Students
                .Include(s => s.Classroom)
                .Include(s => s.Mentor).ThenInclude(m => m.User)
                .Include(s => s.Company)
                .FirstOrDefaultAsync(s => s.id == request.StudentId);
            if (student == null)
                return NotFound("Student not found.");

            var presenceRecap = await _db.PresenceRecaps
                .Where(r => r.StudentId == request.StudentId)
                .OrderByDescending(r => r.update_at)
                .FirstOrDefaultAsync();

            var reportRecap = await _db.ReportRecaps
                .Where(r => r.StudentId == request.StudentId)
                .OrderByDescending(r => r.update_at)
                .FirstOrDefaultAsync();

            if (presenceRecap == null || reportRecap == null)
                return NotFound("Recap not found.");
            var (studentName, kelas, mentorName, walasName) = await GetStudentRelatedInfoAsync(student);

            var startDate = new DateOnly(2025, 8, 15);
            var endPresence = presenceRecap.update_at;
            var endReport = reportRecap.update_at;
            var endDate = endPresence > endReport ? endPresence : endReport;

            var reportPhotoData = await _db.Reports
                .Where(r =>
                    r.Studentid == request.StudentId &&
                    r.ReportPhotoid != null &&
                    r.date >= startDate &&
                    r.date <= endReport
                )
                .OrderBy(r => r.date)
                .Select(r => new { r.ReportPhotoid, r.date })
                .ToListAsync();

            var photoIds = reportPhotoData.Select(x => x.ReportPhotoid).Distinct().ToList();
            var reportFiles = await _db.ReportFiles
                .Where(f => photoIds.Contains(f.id))
                .ToListAsync();

            // Ambil file bytes untuk setiap photo
            var photoList = new List<(byte[]? Image, DateOnly Date, int Index)>();
            int photoIndex = 1;
            foreach (var item in reportPhotoData)
            {
                var file = reportFiles.FirstOrDefault(f => f.id == item.ReportPhotoid);
                if (file != null && file.files != null)
                {
                    photoList.Add((file.files, item.date, photoIndex));
                    photoIndex++;
                }
            }

            string ToIndoDate(DateOnly date)
            {
                var culture = new System.Globalization.CultureInfo("id-ID");
                return date.ToString("dd MMMM yyyy", culture);
            }

            var encryptedId = idEncrypt.EncryptId(request.StudentId);
            var qrUrl = $"https://presensi.smksabdev.my.id/lapran-pkl/approval/{encryptedId}";
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Helpers", "logotkj.png");
            byte[] qrBytes = PrintHelper.GenerateQrWithLogoAndFrame(qrUrl, logoPath);

            // PDF generator
            var pdfBytes = Document.Create(container =>
            {
                // Page 1: Rekap Presensi PKL (portrait)
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(50);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    page.PageColor(Colors.White);

                    page.Content().Column(col =>
                    {
                        col.Item().PaddingBottom(6).Text("Rekap Presensi PKL")
                            .FontSize(16).Bold().AlignCenter();
                        col.Item().PaddingBottom(12).Text($"{ToIndoDate(startDate)} - {ToIndoDate(endPresence)}")
                            .FontSize(11).AlignCenter();

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(150);
                                c.RelativeColumn();
                            });

                            void Row(string label, string value)
                            {
                                table.Cell().Element(CellStyle).PaddingLeft(3).Text(label).Bold();
                                table.Cell().Element(CellStyle).PaddingLeft(3).Text(value);
                            }

                            Row("Nama", studentName);
                            Row("NIS", presenceRecap.nis ?? "-");
                            Row("Kelas", kelas);
                            Row("Wali Kelas", walasName);
                            Row("Mentor", mentorName);
                            Row("Jumlah Hari PKL", $"{presenceRecap.pkl_days} hari");
                            Row("Jumlah Presensi", $"{presenceRecap.presence_total} kali");
                            Row("Jumlah Tidak Presensi", $"{presenceRecap.absen_total} kali");
                            Row("Jumlah Report Harian Lengkap", $"{presenceRecap.send_total} kali");
                            Row("Jumlah Report Harian Tidak Lengkap/Terlambat", $"{presenceRecap.not_send_total} kali");
                            Row("Hadir", $"{presenceRecap.hadir} hari");
                            Row("Sakit", $"{presenceRecap.sakit} hari");
                            Row("Izin", $"{presenceRecap.izin} hari");
                            Row("WFH", $"{presenceRecap.wfh} hari");
                            Row("Libur", $"{presenceRecap.libur} hari");
                        });

                        col.Item().PaddingTop(100).AlignCenter().Column(qrCol =>
                        {
                            qrCol.Item().AlignCenter().PaddingBottom(6).Text("Approval of PKL Report")
                                .FontSize(10).Italic();

                            qrCol.Item().AlignCenter().Width(120).Height(120).Image(qrBytes, ImageScaling.FitArea);
                        });
                    });

                    IContainer CellStyle(IContainer container) =>
                        container.Border(1).BorderColor(Colors.Grey.Medium).PaddingVertical(4).PaddingHorizontal(4).AlignMiddle();
                });

                // Page 2
                var firstPhotos = photoList.Take(4).ToList();
                var remainingPhotos = photoList.Skip(4).ToList();

                var photoChunks = remainingPhotos.Chunk(6).ToList();
                int totalSheets = Math.Max(1, photoChunks.Count);

                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    page.PageColor(Colors.White);

                    page.Content().Row(row =>
                    {
                        // Kiri: Rekap Bimbingan Laporan
                        row.ConstantItem(420).PaddingRight(15).Column(col =>
                        {
                            col.Item().PaddingBottom(6).Text("Rekap Bimbingan Laporan")
                                .FontSize(16).Bold().AlignCenter();
                            col.Item().PaddingBottom(12).Text($"{ToIndoDate(startDate)} - {ToIndoDate(endReport)}")
                                .FontSize(11).AlignCenter();

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.ConstantColumn(150);
                                    c.RelativeColumn();
                                });

                                void Row(string label, string value)
                                {
                                    table.Cell().Element(CellStyle).Text(label).Bold();
                                    table.Cell().Element(CellStyle).Text(value);
                                }

                                Row("Nama", studentName);
                                Row("NIS", reportRecap.nis ?? "-");
                                Row("Kelas", kelas);
                                Row("Wali Kelas", walasName);
                                Row("Mentor", mentorName);
                                Row("Jumlah Minggu", $"{reportRecap.total_weeks} minggu");
                                Row("Jumlah Bimbingan Laporan", $"{reportRecap.report_total} kali");
                            });

                            // 4 photo pertama (seperti sebelumnya)
                            if (firstPhotos.Count > 0)
                            {
                                col.Item().PaddingTop(20).Element(e =>
                                    e.Grid(grid =>
                                    {
                                        grid.Columns(2);
                                        int colIndex = 0;
                                        foreach (var (img, date, idx) in firstPhotos)
                                        {
                                            grid.Item()
                                                .PaddingRight(colIndex == 0 ? 12 : 0)
                                                .PaddingLeft(colIndex == 1 ? 12 : 0)
                                                .Element(photoCell =>
                                                {
                                                    photoCell.Column(cellCol =>
                                                    {
                                                        cellCol.Item().Border(1).BorderColor(Colors.Grey.Lighten2)
                                                            .Height(120).AlignCenter().AlignMiddle()
                                                            .Element(inner =>
                                                            {
                                                                if (img != null)
                                                                    inner.Padding(8).Image(img, ImageScaling.FitArea);
                                                                else inner.AlignCenter().AlignMiddle().Text($"Photo {idx}");
                                                            });
                                                        cellCol.Item().PaddingTop(4).PaddingBottom(10).AlignCenter().Text(ToIndoDate(date)).FontSize(10);
                                                    });
                                                });
                                            colIndex = (colIndex + 1) % 2;
                                        }
                                    })
                                );
                            }
                        });

                        // Kanan: 6 photo pertama dari remainingPhotos
                        row.RelativeItem().PaddingLeft(15).Element(e =>
                        {
                            var photos = photoChunks.Count > 0 ? photoChunks[0] : Array.Empty<(byte[]? Image, DateOnly Date, int Index)>();
                            e.Column(col =>
                            {
                                if (photos.Any())
                                {
                                    col.Item().Element(grid =>
                                        grid.Grid(g =>
                                        {
                                            g.Columns(2);
                                            int colIndex = 0;
                                            foreach (var (img, date, idx) in photos)
                                            {
                                                g.Item()
                                                    .PaddingRight(colIndex == 0 ? 12 : 0)
                                                    .PaddingLeft(colIndex == 1 ? 12 : 0)
                                                    .Element(photoCell =>
                                                    {
                                                        photoCell.Column(cellCol =>
                                                        {
                                                            cellCol.Item().Border(1).BorderColor(Colors.Grey.Lighten2)
                                                                .Height(152).AlignCenter().AlignMiddle()
                                                                .Element(inner =>
                                                                {
                                                                    if (img != null)
                                                                        inner.Padding(8).Image(img, ImageScaling.FitArea);
                                                                    else inner.AlignCenter().AlignMiddle().Text($"Photo {idx}");
                                                                });
                                                            cellCol.Item().PaddingTop(4).PaddingBottom(10).AlignCenter().Text(ToIndoDate(date)).FontSize(10);
                                                        });
                                                    });
                                                colIndex = (colIndex + 1) % 2;
                                            }
                                        })
                                    );
                                }
                            });
                        });
                    });

                    IContainer CellStyle(IContainer container) =>
                        container.Border(1).BorderColor(Colors.Grey.Medium).PaddingVertical(4).PaddingHorizontal(4).AlignMiddle();
                });

                // Sheet berikutnya: 6 photo per bagian kiri, 6 photo per bagian kanan
                for (int i = 1; i < photoChunks.Count; i += 2)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(30);
                        page.DefaultTextStyle(x => x.FontSize(12));
                        page.PageColor(Colors.White);

                        page.Content().Row(row =>
                        {
                            // Kiri
                            row.ConstantItem(420).PaddingRight(15).Element(e =>
                            {
                                var photos = photoChunks[i];
                                e.Column(col =>
                                {
                                    if (photos.Any())
                                    {
                                        col.Item().Element(grid =>
                                            grid.Grid(g =>
                                            {
                                                g.Columns(2);
                                                int colIndex = 0;
                                                foreach (var (img, date, idx) in photos)
                                                {
                                                    g.Item()
                                                        .PaddingRight(colIndex == 0 ? 12 : 0)
                                                        .PaddingLeft(colIndex == 1 ? 12 : 0)
                                                        .Element(photoCell =>
                                                        {
                                                            photoCell.Column(cellCol =>
                                                            {
                                                                cellCol.Item().Border(1).BorderColor(Colors.Grey.Lighten2)
                                                                    .Height(152).AlignCenter().AlignMiddle()
                                                                    .Element(inner =>
                                                                    {
                                                                        if (img != null)
                                                                            inner.Padding(8).Image(img, ImageScaling.FitArea);
                                                                        else inner.AlignCenter().AlignMiddle().Text($"Photo {idx}");
                                                                    });
                                                                cellCol.Item().PaddingTop(4).PaddingBottom(10).AlignCenter().Text(ToIndoDate(date)).FontSize(10);
                                                            });
                                                        });
                                                    colIndex = (colIndex + 1) % 2;
                                                }
                                            })
                                        );
                                    }
                                });
                            });

                            // Kanan
                            row.RelativeItem().PaddingLeft(15).Element(e =>
                            {
                                var photos = (i + 1 < photoChunks.Count) ? photoChunks[i + 1] : Array.Empty<(byte[]? Image, DateOnly Date, int Index)>();
                                e.Column(col =>
                                {
                                    if (photos.Any())
                                    {
                                        col.Item().Element(grid =>
                                            grid.Grid(g =>
                                            {
                                                g.Columns(2);
                                                int colIndex = 0;
                                                foreach (var (img, date, idx) in photos)
                                                {
                                                    g.Item()
                                                        .PaddingRight(colIndex == 0 ? 12 : 0)
                                                        .PaddingLeft(colIndex == 1 ? 12 : 0)
                                                        .Element(photoCell =>
                                                        {
                                                            photoCell.Column(cellCol =>
                                                            {
                                                                cellCol.Item().Border(1).BorderColor(Colors.Grey.Lighten2)
                                                                    .Height(152).AlignCenter().AlignMiddle()
                                                                    .Element(inner =>
                                                                    {
                                                                        if (img != null)
                                                                            inner.Padding(8).Image(img, ImageScaling.FitArea);
                                                                        else inner.AlignCenter().AlignMiddle().Text($"Photo {idx}");
                                                                    });
                                                                cellCol.Item().PaddingTop(4).PaddingBottom(10).AlignCenter().Text(ToIndoDate(date)).FontSize(10);
                                                            });
                                                        });
                                                    colIndex = (colIndex + 1) % 2;
                                                }
                                            })
                                        );
                                    }
                                });
                            });
                        });

                        IContainer CellStyle(IContainer container) =>
                            container.Border(1).BorderColor(Colors.Grey.Medium).PaddingVertical(4).PaddingHorizontal(4).AlignMiddle();
                    });
                }
            }).GeneratePdf();

            var fileName = $"Rekap Presensi dan Bimbingan Laporan - {student.nis}.pdf";
            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpGet("presence")]
        public async Task<IActionResult> GetPresenceRecap(
            string id, [FromServices] IIdEncryptionService enc)
        {
            int studentId;
            try
            {
                studentId = enc.DecryptId(id);
            }
            catch
            {
                return BadRequest("Invalid ID format.");
            }

            var student = await _db.Students.FirstOrDefaultAsync(s => s.id == studentId);
            if (student == null)
                return NotFound("Student not found.");

            var recap = await _db.PresenceRecaps
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.update_at)
                .FirstOrDefaultAsync();
            if (recap == null)
                return NotFound("Presence recap not found.");

            var (studentName, kelas, mentorName, walasName) = await GetStudentRelatedInfoAsync(student);
            return Ok(new
            {
                studentName,
                recap.nis,
                kelas,
                mentorName,
                walasName,
                recap.pkl_days,
                recap.presence_total,
                recap.absen_total,
                recap.send_total,
                recap.not_send_total,
                recap.hadir,
                recap.sakit,
                recap.izin,
                recap.wfh,
                recap.libur
            });
        }

        [HttpGet("report")]
        public async Task<IActionResult> GetReportRecap(
            string id, [FromServices] IIdEncryptionService enc)
        {
            int studentId;
            try
            {
                studentId = enc.DecryptId(id);
            }
            catch
            {
                return BadRequest("Invalid ID format.");
            }

            var student = await _db.Students.FirstOrDefaultAsync(s => s.id == studentId);
            if (student == null)
                return NotFound("Student not found.");

            var recap = await _db.ReportRecaps
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.update_at)
                .FirstOrDefaultAsync();
            if (recap == null)
                return NotFound("Presence recap not found.");

            var (studentName, kelas, mentorName, walasName) = await GetStudentRelatedInfoAsync(student);
            return Ok(new
            {
                studentName,
                recap.nis,
                kelas,
                mentorName,
                walasName,
                recap.total_weeks,
                recap.report_total
            });
        }

        [HttpPost("print-presence-recap")]
        public async Task<IActionResult> PrintPresenceRecap([FromBody] int classId)
        {
            var startDate = new DateOnly(2025, 8, 15);
            var endDate = new DateOnly(2025, 12, 19);

            var classroom = await _db.Classrooms.FirstOrDefaultAsync(c => c.id == classId);
            if (classroom == null)
                return NotFound("Classroom not found.");
            var className = classroom.name ?? "-";

            var students = await _db.Students
                .Where(s => s.Classroomid == classId)
                .Select(s => new { s.id, s.Userid })
                .ToListAsync();

            var userIds = students.Select(s => s.Userid).ToList();
            var users = await _db.Users
                .Where(u => userIds.Contains(u.id))
                .ToDictionaryAsync(u => u.id, u => u.fullname ?? "-");

            // Buat list tanggal (Senin-Jumat)
            var dateList = new List<DateOnly>();
            for (var dt = startDate; dt <= endDate; dt = dt.AddDays(1))
            {
                if (dt.DayOfWeek != DayOfWeek.Saturday && dt.DayOfWeek != DayOfWeek.Sunday)
                    dateList.Add(dt);
            }
            int totalHari = dateList.Count;

            var studentIds = students.Select(s => s.id).ToList();
            var presences = await _db.Presences
                .Where(p => studentIds.Contains(p.Studentid) && p.date >= startDate && p.date <= endDate)
                .ToListAsync();

            var presenceDetailIds = presences.Select(p => p.PresenceDetailid).Distinct().ToList();
            var presenceDetails = await _db.PresenceDetails
                .Where(d => presenceDetailIds.Contains(d.id))
                .ToDictionaryAsync(d => d.id, d => d);

            // Siapkan struktur data untuk rekap
            var studentRows = students.Select((s, idx) => new StudentRekapRow
            {
                No = idx + 1,
                StudentId = s.id,
                Name = users.ContainsKey(s.Userid) ? users[s.Userid] : "-"
            }).ToList();

            // Isi data per siswa per tanggal
            foreach (var row in studentRows)
            {
                int totalP = 0, totalR = 0, totalT = 0;
                var dataPerTanggal = new List<(string P, string RorT)>();

                foreach (var dt in dateList)
                {
                    var presence = presences.FirstOrDefault(p => p.Studentid == row.StudentId && p.date == dt);
                    if (presence != null && presenceDetails.TryGetValue(presence.PresenceDetailid, out var detail))
                    {
                        // Kolom P
                        string pVal = "P";
                        totalP++;

                        // Kolom R/T
                        string rVal = "-";
                        if (detail.iscomplate == true)
                        {
                            if (!detail.update_at.HasValue || detail.update_at.Value == presence.date)
                            {
                                rVal = "R";
                                totalR++;
                            }
                            else
                            {
                                rVal = "T";
                                totalT++;
                            }
                        }
                        else rVal = "-";
                        dataPerTanggal.Add((pVal, rVal));
                    }
                    else dataPerTanggal.Add(("-", "-"));
                }
                row.Data.AddRange(dataPerTanggal);
                row.TotalP = totalP;
                row.TotalR = totalR;
                row.TotalT = totalT;
            }

            // Generate Excel
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Rekap Presensi");

            int colOffset = 2; // No, Nama
            int dateColStart = colOffset + 1;
            int totalColStart = dateColStart + dateList.Count * 2;

            ws.Row(1).Height = 30;
            ws.Row(2).Height = 18; // header
            ws.Row(3).Height = 18;

            // Judul
            ws.Cells[1, 1, 1, totalColStart + 3].Merge = true;
            ws.Cells[1, 1].Value = $"Rekap Presensi PKL - {className}";
            ws.Cells[1, 1].Style.Font.Size = 14;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1, 3, 1].Merge = true; // No
            ws.Cells[2, 2, 3, 2].Merge = true; // Nama

            ws.Cells[2, 1].Value = "No";
            ws.Column(1).Width = 5;
            ws.Cells[2, 2].Value = "Nama";
            ws.Column(2).Width = 33;

            // Header "Nama"
            ws.Cells[2, 2, 3, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[2, 2, 3, 2].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            ws.Cells[2, 1, 3, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            for (int c = 3; c < totalColStart; c++)
            {
                ws.Column(c).Width = 4;
            }

            ws.Cells[2, 3, 2, dateColStart + dateList.Count * 2 - 1].Merge = true;
            ws.Cells[2, 3].Value = "Tanggal";
            ws.Cells[2, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[2, totalColStart].Value = "Total";
            ws.Cells[2, totalColStart, 2, totalColStart + 2].Merge = true;
            ws.Cells[2, totalColStart].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // Header baris 3 (tanggal dan subkolom)
            ws.Cells[3, 1].Value = "";
            ws.Cells[3, 2].Value = "";
            int col = 3;
            foreach (var dt in dateList)
            {
                ws.Cells[3, col].Value = dt.ToString("dd-MMM");
                ws.Cells[3, col, 3, col + 1].Merge = true;
                ws.Cells[3, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                col += 2;
            }
            ws.Cells[3, totalColStart].Value = "P";
            ws.Cells[3, totalColStart + 1].Value = "R";
            ws.Cells[3, totalColStart + 2].Value = "T";

            ws.Column(totalColStart).Width = 6;
            ws.Column(totalColStart + 1).Width = 6;
            ws.Column(totalColStart + 2).Width = 6;

            col = 3;
            ws.Cells[4, totalColStart].Value = "";
            ws.Cells[4, totalColStart + 1].Value = "";
            ws.Cells[4, totalColStart + 2].Value = "";

            // Data siswa
            int rowIdx = 4;
            foreach (var row in studentRows)
            {
                ws.Cells[rowIdx, 1].Value = row.No;
                ws.Cells[rowIdx, 2].Value = row.Name;
                col = 3;
                foreach (var (pVal, rVal) in row.Data)
                {
                    ws.Cells[rowIdx, col].Value = pVal;
                    ws.Cells[rowIdx, col + 1].Value = rVal;
                    col += 2;
                }
                ws.Cells[rowIdx, totalColStart].Value =
                    row.TotalP == 0 ? "-" : row.TotalP;

                ws.Cells[rowIdx, totalColStart + 1].Value =
                    row.TotalR == 0 ? "-" : row.TotalR;

                ws.Cells[rowIdx, totalColStart + 2].Value =
                    row.TotalT == 0 ? "-" : row.TotalT;
                rowIdx++;
            }

            // Footer total hari
            int totalHariRow = rowIdx;
            ws.Cells[totalHariRow, 1, totalHariRow, totalColStart - 1].Merge = true;
            ws.Cells[totalHariRow, 1].Value = "Total Hari : ";
            ws.Cells[totalHariRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells[totalHariRow, 1].Style.Font.Bold = true;

            for (int r = 4; r < totalHariRow; r++)
            {
                ws.Row(r).Height = 16.5;
            }

            // merge hasil total P R T
            ws.Cells[totalHariRow, totalColStart, totalHariRow, totalColStart + 2].Merge = true;
            ws.Cells[totalHariRow, totalColStart].Value = totalHari;
            ws.Cells[totalHariRow, totalColStart].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // row pemisah
            int separatorRow = totalHariRow + 1;

            // Hapus border di baris pemisah
            var sepCells = ws.Cells[separatorRow, 1, separatorRow, totalColStart + 2];
            sepCells.Style.Border.Top.Style = ExcelBorderStyle.None;
            sepCells.Style.Border.Bottom.Style = ExcelBorderStyle.None;
            sepCells.Style.Border.Left.Style = ExcelBorderStyle.None;
            sepCells.Style.Border.Right.Style = ExcelBorderStyle.None;

            ws.Cells[separatorRow + 2, 1].Value = "Ket :";
            ws.Cells[separatorRow + 2, 1].Style.Font.Bold = true;

            ws.Cells[separatorRow + 3, 1].Value = "1. P = Presensi";
            ws.Cells[separatorRow + 4, 1].Value = "2. R = Report";
            ws.Cells[separatorRow + 5, 1].Value = "3. T = Report Telat";

            // Style
            ws.Cells[2, 1, 3, totalColStart + 2].Style.Font.Bold = true;
            ws.Cells[2, 1, rowIdx - 1, totalColStart + 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // Nama
            ws.Cells[4, 2, rowIdx - 1, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
            ws.Cells[4, 2, rowIdx - 1, 2].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            // Vertical center untuk semua cell yang terpakai
            var usedRange = ws.Cells[1, 1, separatorRow + 4, totalColStart + 2];
            usedRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            var tableCells = ws.Cells[2, 1, rowIdx, totalColStart + 2];
            tableCells.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            tableCells.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            tableCells.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            tableCells.Style.Border.Right.Style = ExcelBorderStyle.Thin;

            var fileName = $"Rekap_Presensi_PKL_{className}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            var fileBytes = package.GetAsByteArray();
            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}