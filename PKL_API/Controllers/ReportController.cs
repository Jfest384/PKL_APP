using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PKL_API.Models;
using PKL_API.Models.DTO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PKL_API.Controllers
{
    [Route("api/repots")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly PklContext _db;
        private readonly UserAccessHelper _userAccessHelper;

        private static string ToIndonesianLongDate(DateOnly date)
        {
            var culture = new System.Globalization.CultureInfo("id-ID");
            return date.ToString("dddd, dd MMMM yyyy", culture);
        }

        public ReportController(PklContext db, UserAccessHelper userAccessHelper)
        {
            _db = db;
            _userAccessHelper = userAccessHelper;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> SubmitReport([FromForm] ReportDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("Invalid user ID in token.");

            var student = await _db.Students
                .Include(s => s.User)
                .Include(s => s.Classroom)
                .Include(s => s.Company)
                .FirstOrDefaultAsync(s => s.Userid == userId);

            if (student == null || !(student.isPKL ?? false))
                return StatusCode(403, "Only active PKL students can submit reports.");

            if (string.IsNullOrWhiteSpace(dto.description))
                return BadRequest("Description is required.");

            var allowedExtensions = new[] { ".pdf" };

            bool IsValidFile(IFormFile? file)
            {
                if (file == null) return true;
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                return allowedExtensions.Contains(ext);
            }

            if (!IsValidFile(dto.GuidancePhoto) || !IsValidFile(dto.ReportFile))
                return BadRequest("Only PNG, JPG, JPEG, DOCX, or PDF files are allowed.");

            async Task<ReportFile?> SaveFileAsync(IFormFile? file)
            {
                if (file == null || file.Length == 0)
                    return null;

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);

                return new ReportFile
                {
                    id = Guid.NewGuid(),
                    files = ms.ToArray(),
                    extension = Path.GetExtension(file.FileName)
                };
            }

            var now = DateTime.Now;

            var photoEntity = await SaveFileAsync(dto.GuidancePhoto);
            var fileEntity = await SaveFileAsync(dto.ReportFile);

            if (photoEntity != null)
                _db.ReportFiles.Add(photoEntity);
            if (fileEntity != null)
                _db.ReportFiles.Add(fileEntity);

            await _db.SaveChangesAsync(); // simpan file lebih dulu untuk dapat ID-nya

            var report = new Report
            {
                Studentid = student.id,
                date = DateOnly.FromDateTime(now),
                time = TimeOnly.FromDateTime(now),
                Mentorid = student.Mentorid ?? throw new Exception("No mentor assigned."),
                Classroomid = student.Classroomid ?? throw new Exception("No classroom assigned."),
                description = dto.description,
                ReportPhotoid = photoEntity?.id,
                ReportFileid = fileEntity?.id
            };

            _db.Reports.Add(report);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                id_student = student.id,
                nis = student.nis,
                name = student.User?.fullname,
                classroom = student.Classroom?.name,
                company = student.Company?.name,
                description = dto.description
            });
        }


        [Authorize]
        [HttpPut("feedback/{reportId}")]
        public async Task<IActionResult> GiveFeedback(int reportId, FeedbackDTO feedbackDTO)
        {
            if (feedbackDTO == null || string.IsNullOrWhiteSpace(feedbackDTO.feedback))
                return BadRequest("Feedback is required.");

            // Get user id from claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null)
                return Unauthorized("User ID not found in token.");

            if (!int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("Invalid user ID in token.");

            // Get user from database to check role
            var user = await _db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.id == userId);
            if (user == null)
                return Unauthorized("User not found.");

            // Ambil roleId dari UserRoles (asumsi satu role per user)
            var userRole = user.UserRoles.FirstOrDefault();
            if (userRole == null)
                return Unauthorized("User role not found.");
            int roleId = userRole.RoleId;

            // Only allow role id 3 (mentor) to give feedback
            if (roleId == 1 || roleId == 2 || roleId == 6)
                return StatusCode(403, "You are not allowed to give feedback.");

            // Get report by id (include Student for nis/name, and Student.Mentor for mentor check)
            var report = await _db.Reports
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.id == reportId);
            if (report == null)
                return NotFound("Report not found.");

            // If mentor, only allow giving feedback to their own students' reports
            if (roleId == 3)
            {
                var mentor = await _db.Mentors.FirstOrDefaultAsync(m => m.Userid == userId);
                if (mentor == null)
                    return Unauthorized("Mentor data not found.");

                // Cek apakah student pada report dimentori oleh mentor ini
                if (report.Student.Mentorid != mentor.id)
                    return StatusCode(403, "You can only give feedback to your own students' reports.");
            }

            report.feedback = feedbackDTO.feedback;
            await _db.SaveChangesAsync();

            // Get classroom and company info
            var classroom = await _db.Classrooms.FirstOrDefaultAsync(c => c.id == report.Student.Classroomid);
            var company = await _db.Companies.FirstOrDefaultAsync(c => c.id == report.Student.Companyid);

            return Ok(new
            {
                id_student = report.Studentid,
                nis = report.Student.nis,
                name = report.Student.User?.fullname,
                classroom_name = classroom?.name,
                company_name = company?.name,
                content = report.description,
                feedback = report.feedback
            });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetReports(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? name = null,
            [FromQuery] int? classId = null)
        {
            int userId, roleId;
            try
            {
                (userId, roleId) = await _userAccessHelper.GetUserIdAndRoleAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            if (pageSize < 1) pageSize = 10;

            IQueryable<Report> query = _db.Reports
                .Include(r => r.Student).ThenInclude(s => s.Classroom)
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.Student).ThenInclude(s => s.Company)
                .Include(r => r.Student).ThenInclude(s => s.Mentor).ThenInclude(m => m.User)
                .Include(r => r.ReportFile)
                .Include(r => r.ReportPhoto);

            // Role-based filtering
            if (roleId == 2)
            {
                var student = await _db.Students.FirstOrDefaultAsync(s => s.Userid == userId);
                if (student == null) return BadRequest("Student not found.");
                query = query.Where(r => r.Studentid == student.id);
            }
            else if (roleId == 3)
            {
                var mentor = await _db.Mentors.FirstOrDefaultAsync(m => m.Userid == userId);
                if (mentor == null) return BadRequest("Mentor not found.");
                query = query.Where(r => r.Student.Mentorid == mentor.id);
            }
            else if (roleId == 5)
            {
                var wali = await _db.WaliKelas.Include(w => w.Teacher)
                                              .FirstOrDefaultAsync(w => w.Userid == userId);
                var classroom = await _db.Classrooms.FirstOrDefaultAsync(c => c.WaliKelasid == wali.id);
                if (classroom == null) return BadRequest("Classroom not found.");
                query = query.Where(r => r.Student.Classroomid == classroom.id);
            }

            // Filter by name
            if (!string.IsNullOrWhiteSpace(name))
            {
                var loweredName = name.ToLower();
                query = query.Where(r => r.Student.User.fullname.ToLower().Contains(loweredName));
            }

            // Filter by class
            if (classId.HasValue)
            {
                query = query.Where(r => r.Classroomid == classId.Value);
            }

            var totalCount = await query.CountAsync();
            var rawReports = await query
                .OrderByDescending(r => r.date)
                .ThenByDescending(r => r.time)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pagedResult = rawReports.Select(r => new
            {
                id = r.id,
                date = ToIndonesianLongDate(r.date),
                time = r.time,
                studentId = r.Studentid,
                nis = r.Student.nis,
                name = r.Student.User.fullname,
                classId = r.Classroomid,
                classroom_name = r.Student.Classroom?.name,
                company_name = r.Student.Company?.name,
                mentor = r.Student.Mentor?.User?.fullname ?? "-",
                description = r.description,
                feedback = r.feedback ?? "-",
                reportFileId = r.ReportFileid,
                reportPhotoId = r.ReportPhotoid,
                hasAttachment = (r.ReportFileid != null || r.ReportPhotoid != null),
                isGuidance = _db.WeeklyGuidances.Any(w =>
                    w.Studentid == r.Studentid &&
                    w.WeekStartDate.Date == GetStartOfWeek(r.date).Date)
                        ? "✔️" : "❌"
            }).ToList();

            return Ok(new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                data = pagedResult
            });
        }

        private DateTime GetStartOfWeek(DateOnly date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).ToDateTime(TimeOnly.MinValue).Date;
        }

        [HttpGet("preview/{id}")]
        public async Task<IActionResult> PreviewReportFile(Guid id)
        {
            var fileEntity = await _db.ReportFiles.FindAsync(id);

            if (fileEntity == null)
                return NotFound("File tidak ditemukan");

            var extension = fileEntity.extension.Trim().ToLower();

            byte[] fileBytes = fileEntity.files;
            string contentType;

            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    contentType = "image/jpeg";
                    break;
                case ".png":
                    contentType = "image/png";
                    break;
                case ".pdf":
                    contentType = "application/pdf";
                    break;
                case ".doc":
                case ".docx":
                    fileBytes = ConvertDocxToPdf(fileBytes); // Konversi Word ke PDF
                    contentType = "application/pdf";
                    break;
                default:
                    return BadRequest("Format file tidak didukung untuk preview");
            }

            return File(fileBytes, contentType);
        }

        private byte[] ConvertDocxToPdf(byte[] docxBytes)
        {
            using var inputStream = new MemoryStream(docxBytes);
            using var wordDocument = new Syncfusion.DocIO.DLS.WordDocument(inputStream, Syncfusion.DocIO.FormatType.Docx);
            using var renderer = new Syncfusion.DocIORenderer.DocIORenderer();
            using var pdfDocument = renderer.ConvertToPDF(wordDocument);

            using var outputStream = new MemoryStream();
            pdfDocument.Save(outputStream);
            return outputStream.ToArray();
        }

        [Authorize]
        [HttpPost("weekly-guidance/{studentId}")]
        public async Task<IActionResult> ConfirmWeeklyGuidance(int studentId)
        {
            // Ambil userId dan roleId dari helper
            int userId, roleId;
            try
            {
                (userId, roleId) = await _userAccessHelper.GetUserIdAndRoleAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            // Hanya role mentor (id = 3) yang boleh akses
            if (roleId != 3)
                return StatusCode(403, "Only mentors are allowed to access this endpoint.");

            var mentor = await _db.Mentors.FirstOrDefaultAsync(m => m.Userid == userId);
            if (mentor == null) return BadRequest("Mentor not found.");

            var student = await _db.Students.FirstOrDefaultAsync(s => s.id == studentId);
            if (student == null) return NotFound("Student not found.");

            var weekStart = GetStartOfWeek(DateTime.Today);

            bool exists = await _db.WeeklyGuidances.AnyAsync(w => w.Studentid == studentId && w.WeekStartDate == weekStart);
            if (exists)
                return BadRequest("Bimbingan minggu ini sudah dicatat.");

            _db.WeeklyGuidances.Add(new WeeklyGuidance
            {
                Studentid = studentId,
                Mentorid = mentor.id,
                WeekStartDate = weekStart,
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();

            return Ok("Bimbingan minggu ini berhasil disimpan.");
        }

        private DateTime GetStartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }


        [Authorize]
        [HttpGet("student/{studentId}/print")]
        [Obsolete]
        public async Task<IActionResult> PrintReportByStudent(
            int studentId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate)
        {
            int userId, roleId;
            try
            {
                (userId, roleId) = await _userAccessHelper.GetUserIdAndRoleAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            // Cari student berdasarkan studentId
            var student = await _db.Students
                .Include(s => s.Classroom)
                .Include(s => s.Company)
                .Include(s => s.Mentor).ThenInclude(s => s.User)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.id == studentId);

            if (student == null)
                return NotFound("Student not found.");

            //// Jika mentor, hanya boleh print siswa bimbingannya
            //if (roleId == 3)
            //{
            //    var mentor = await _db.Mentors.FirstOrDefaultAsync(m => m.Userid == userId);
            //    if (mentor == null)
            //        return StatusCode(403, "Mentor data not found.");
            //    if (student.Mentorid != mentor.id)
            //        return StatusCode(403, "You can only print reports for your own mentees.");
            //}

            //// Jika wali kelas, hanya boleh print siswa perwaliannya
            //if (roleId == 5)
            //{
            //    var waliKelas = await _db.WaliKelas
            //        .FirstOrDefaultAsync(wk => wk.Userid == userId);
            //    if (waliKelas == null)
            //        return StatusCode(403, "You are not assigned as a homeroom teacher for any class.");

            //    var classroom = await _db.Classrooms.FirstOrDefaultAsync(c => c.WaliKelasid == waliKelas.id);
            //    if (classroom == null)
            //        return StatusCode(403, "You are not assigned as a homeroom teacher for any class.");
            //    if (student.Classroomid != classroom.id)
            //        return StatusCode(403, "You can only print reports for students in your homeroom class.");
            //}

            // Ambil semua report siswa tersebut
            var query = _db.Reports
                .Include(r => r.Classroom)
                .Include(r => r.Student)
                    .ThenInclude(s => s.Company)
                .Where(r => r.Studentid == student.id);

            if (startDate.HasValue)
                query = query.Where(r => r.date >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(r => r.date <= endDate.Value);

            var reports = await query
                .OrderBy(r => r.date)
                .ThenBy(r => r.time)
                .ToListAsync();

            var pdfBytes = GenerateStudentReportPdf(student, reports, startDate, endDate);
            var fileName = $"StudentReport_{student.nis}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        [Obsolete]
        private static byte[] GenerateStudentReportPdf(
            Student student,
            List<Report> reports,
            DateOnly? startDate,
            DateOnly? endDate
        )
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    page.PageColor(Colors.White);

                    page.Header()
                        .Text("Student Report")
                        .FontSize(20)
                        .Bold()
                        .AlignCenter();

                    page.Content().PaddingTop(15).Column(col =>
                    {
                        string rangeText = (startDate.HasValue && endDate.HasValue)
                            ? $"From: {startDate:yyyy-MM-dd}  To: {endDate:yyyy-MM-dd}"
                            : (startDate.HasValue ? $"From: {startDate:yyyy-MM-dd}" :
                                (endDate.HasValue ? $"Until: {endDate:yyyy-MM-dd}" : "All Dates"));

                        col.Item().Element(x => x.Text(rangeText).FontSize(13).Bold().AlignCenter());

                        var mentorName = student.Mentor?.User?.fullname ?? "-";
                        var className = student.Classroom?.name ?? "-";

                        col.Item().PaddingTop(20).AlignLeft().Row(row =>
                        {
                            // Sisi kiri: NIS & Name
                            row.RelativeColumn().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(45); // Label
                                    columns.ConstantColumn(15); // Separator
                                    columns.RelativeColumn();   // Value
                                });

                                table.Cell().Element(CellStyle).Text("NIS");
                                table.Cell().Element(CellStyle).Text(":");
                                table.Cell().Element(CellStyle).Text(student.nis ?? "-").WrapAnywhere();

                                table.Cell().Element(CellStyle).Text("Name");
                                table.Cell().Element(CellStyle).Text(":");
                                table.Cell().Element(CellStyle).Text(student.User?.fullname ?? "-").WrapAnywhere();
                            });

                            // Sisi kanan: Class & Mentor
                            row.RelativeColumn().PaddingLeft(85).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(45); // Label
                                    columns.ConstantColumn(15); // Separator
                                    columns.RelativeColumn();   // Value
                                });

                                table.Cell().Element(CellStyle).Text("Class");
                                table.Cell().Element(CellStyle).Text(":");
                                table.Cell().Element(CellStyle).Text(className).WrapAnywhere();

                                table.Cell().Element(CellStyle).Text("Mentor");
                                table.Cell().Element(CellStyle).Text(":");
                                table.Cell().Element(CellStyle).Text(mentorName).WrapAnywhere();
                            });

                            IContainer CellStyle(IContainer container) =>
                                container.PaddingVertical(2);
                        });

                        // Table laporan
                        col.Item().PaddingTop(20).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80); // Date
                                columns.RelativeColumn(1);  // Company
                                columns.RelativeColumn(2);  // Content
                                columns.RelativeColumn(2);  // Feedback
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Date").Bold();
                                header.Cell().Element(CellStyle).Text("Company").Bold();
                                header.Cell().Element(CellStyle).Text("Content").Bold();
                                header.Cell().Element(CellStyle).Text("Feedback").Bold();
                            });

                            foreach (var r in reports)
                            {
                                table.Cell().Element(CellStyle).Text(r.date.ToString("yyyy-MM-dd"));
                                table.Cell().Element(CellStyle).Text(r.Student?.Company?.name ?? "-");
                                table.Cell().Element(CellStyle).Text(r.description ?? "-");
                                table.Cell().Element(CellStyle).Text(r.feedback ?? "-");
                            }

                            IContainer CellStyle(IContainer container) =>
                                container
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .PaddingVertical(6)
                                    .PaddingHorizontal(6);
                        });
                    });
                });
            }).GeneratePdf();
        }


        [Authorize]
        [HttpGet("class/{classId?}/print")]
        public async Task<IActionResult> PrintReportByClass(
            int? classId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate)
        {
            int userId, roleId;
            try
            {
                (userId, roleId) = await _userAccessHelper.GetUserIdAndRoleAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            Classroom? classroom = null;

            //if (roleId == 5)
            //{
            //    // Wali Kelas: ambil kelas yang diwalikan user ini
            //    var waliKelas = await _db.WaliKelas
            //        .FirstOrDefaultAsync(wk => wk.User.id == userId);

            //    if (waliKelas == null)
            //        return NotFound("You are not assigned as a homeroom teacher for any class.");

            //    classroom = await _db.Classrooms
            //        .FirstOrDefaultAsync(c => c.WaliKelasid == waliKelas.id);

            //    if (classroom == null)
            //        return NotFound("Classroom not found for your homeroom assignment.");
            //    classId = classroom.id;
            //}
            //else
            //{
            //    // Role lain: gunakan classId dari parameter
            //    if (!classId.HasValue)
            //        return BadRequest("ClassId is required.");
            //    classroom = await _db.Classrooms
            //        .Include(c => c.Students)
            //            .ThenInclude(s => s.Company)
            //        .Include(c => c.Students)
            //            .ThenInclude(s => s.Mentor)
            //        .FirstOrDefaultAsync(c => c.id == classId.Value);

            //    if (classroom == null)
            //        return NotFound("Classroom not found.");
            //}

            // Role lain: gunakan classId dari parameter
            if (!classId.HasValue)
                return BadRequest("ClassId is required.");
            classroom = await _db.Classrooms
                .Include(c => c.Students)
                    .ThenInclude(s => s.Company)
                .Include(c => c.Students)
                    .ThenInclude(s => s.Mentor)
                .FirstOrDefaultAsync(c => c.id == classId.Value);

            if (classroom == null)
                return NotFound("Classroom not found.");

            // Ambil semua report siswa di kelas tersebut
            var query = _db.Reports
                .Include(r => r.Student)
                    .ThenInclude(s => s.Mentor)
                .Include(r => r.Mentor)
                    .ThenInclude(m => m.User)
                .Include(r => r.Classroom)
                .Include(r => r.Student)
                    .ThenInclude(s => s.Company)
                .Include(r => r.Student)
                    .ThenInclude(s => s.User)
                .Where(r => r.Classroomid == classroom.id);

            if (startDate.HasValue)
                query = query.Where(r => r.date >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(r => r.date <= endDate.Value);

            var reports = await query
                .OrderBy(r => r.date)
                .ThenBy(r => r.time)
                .ToListAsync();

            var pdfBytes = GenerateClassReportPdf(classroom, reports, startDate, endDate);
            var fileName = $"ClassReport_{classroom.name}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        private static byte[] GenerateClassReportPdf(
            Classroom classroom,
            List<Report> reports,
            DateOnly? startDate,
            DateOnly? endDate
        )
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    page.PageColor(Colors.White);

                    page.Header()
                        .Text($"Class Report - {classroom.name}")
                        .FontSize(20)
                        .Bold()
                        .AlignCenter();

                    page.Content().PaddingTop(15).Column(col =>
                    {
                        string rangeText = (startDate.HasValue && endDate.HasValue)
                            ? $"From: {startDate:yyyy-MM-dd}  To: {endDate:yyyy-MM-dd}"
                            : (startDate.HasValue ? $"From: {startDate:yyyy-MM-dd}" :
                                (endDate.HasValue ? $"Until: {endDate:yyyy-MM-dd}" : "All Dates"));

                        col.Item().Element(x => x.Text(rangeText).FontSize(13).Bold().AlignCenter());

                        col.Item().PaddingTop(15).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(60);
                                columns.RelativeColumn(2);  // Student
                                columns.ConstantColumn(80); // Date
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);  // Company
                                columns.RelativeColumn(2);  // Content
                                columns.RelativeColumn(2);  // Feedback
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("NIS").Bold();
                                header.Cell().Element(CellStyle).Text("Name").Bold();
                                header.Cell().Element(CellStyle).Text("Date").Bold();
                                header.Cell().Element(CellStyle).Text("Mentor").Bold();
                                header.Cell().Element(CellStyle).Text("Company").Bold();
                                header.Cell().Element(CellStyle).Text("Content").Bold();
                                header.Cell().Element(CellStyle).Text("Feedback").Bold();
                            });

                            foreach (var r in reports)
                            {
                                table.Cell().Element(CellStyle).Text(r.Student?.nis);
                                table.Cell().Element(CellStyle).Text(r.Student?.User?.fullname ?? "-");
                                table.Cell().Element(CellStyle).Text(r.date.ToString("yyyy-MM-dd"));
                                table.Cell().Element(CellStyle).Text(r.Mentor?.User?.fullname ?? "-");
                                table.Cell().Element(CellStyle).Text(r.Student?.Company?.name ?? "-");
                                table.Cell().Element(CellStyle).Text(r.description ?? "-");
                                table.Cell().Element(CellStyle).Text(r.feedback ?? "-");
                            }

                            IContainer CellStyle(IContainer container) =>
                                container
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .PaddingVertical(5)
                                    .PaddingHorizontal(5);
                        });
                    });
                });
            }).GeneratePdf();
        }

        [Authorize]
        [HttpGet("mentor/{mentorId}/print")]
        public async Task<IActionResult> PrintReportByMentor(
            int mentorId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate)
        {
            int userId, roleId;
            try
            {
                (userId, roleId) = await _userAccessHelper.GetUserIdAndRoleAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            // Validasi mentor dan ambil data lengkap
            var mentor = await _db.Mentors
                .Include(m => m.Students)
                    .ThenInclude(s => s.Classroom)
                .Include(m => m.Students)
                    .ThenInclude(s => s.Company)
                .Include(m => m.Students)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(m => m.id == mentorId);

            if (mentor == null)
                return NotFound("Mentor not found.");

            //// Jika role 3 (mentor), hanya boleh print siswa bimbingannya sendiri
            //if (roleId == 3)
            //{
            //    var currentMentor = await _db.Mentors.FirstOrDefaultAsync(m => m.Userid == userId);
            //    if (currentMentor == null)
            //        return StatusCode(403, "Mentor data not found.");
            //    if (mentor.id != currentMentor.id)
            //        return StatusCode(403, "You can only print reports for your own mentees.");
            //}

            var query = _db.Reports
                .Include(r => r.Student)
                    .ThenInclude(s => s.Classroom)
                .Include(r => r.Student)
                    .ThenInclude(s => s.Company)
                .Include(r => r.Student)
                    .ThenInclude(s => s.User)
                .Include(r => r.Mentor).ThenInclude(m => m.User)
                .Include(r => r.Classroom)
                .Where(r => r.Mentorid == mentorId);

            if (startDate.HasValue)
                query = query.Where(r => r.date >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(r => r.date <= endDate.Value);

            var reports = await query
                .OrderBy(r => r.date)
                .ThenBy(r => r.time)
                .ToListAsync();

            var pdfBytes = GenerateMentorReportPdf(mentor, reports, startDate, endDate);
            var fileName = $"MentorReport_{mentor.User?.fullname}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        private static byte[] GenerateMentorReportPdf(
            Mentor mentor,
            List<Report> reports,
            DateOnly? startDate,
            DateOnly? endDate
        )
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    page.PageColor(Colors.White);

                    page.Header()
                        .Text($"Mentor Report - {mentor.User?.fullname}")
                        .FontSize(20)
                        .Bold()
                        .AlignCenter();

                    page.Content().PaddingTop(15).Column(col =>
                    {
                        string rangeText = (startDate.HasValue && endDate.HasValue)
                            ? $"From: {startDate:yyyy-MM-dd}  To: {endDate:yyyy-MM-dd}"
                            : (startDate.HasValue ? $"From: {startDate:yyyy-MM-dd}" :
                                (endDate.HasValue ? $"Until: {endDate:yyyy-MM-dd}" : "All Dates"));

                        col.Item().Element(x => x.Text(rangeText).FontSize(13).Bold().AlignCenter());

                        col.Item().PaddingTop(15).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(60); // NIS
                                columns.RelativeColumn(2);  // Name
                                columns.RelativeColumn(1);  // Class
                                columns.ConstantColumn(80); // Date
                                columns.RelativeColumn(1);  // Company
                                columns.RelativeColumn(2);  // Content
                                columns.RelativeColumn(2);  // Feedback
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("NIS").Bold();
                                header.Cell().Element(CellStyle).Text("Name").Bold();
                                header.Cell().Element(CellStyle).Text("Class").Bold();
                                header.Cell().Element(CellStyle).Text("Date").Bold();
                                header.Cell().Element(CellStyle).Text("Company").Bold();
                                header.Cell().Element(CellStyle).Text("Content").Bold();
                                header.Cell().Element(CellStyle).Text("Feedback").Bold();
                            });

                            foreach (var r in reports)
                            {
                                table.Cell().Element(CellStyle).Text(r.Student?.nis);
                                table.Cell().Element(CellStyle).Text(r.Student?.User?.fullname ?? "-");
                                table.Cell().Element(CellStyle).Text(r.Student?.Classroom?.name ?? "-");
                                table.Cell().Element(CellStyle).Text(r.date.ToString("yyyy-MM-dd"));
                                table.Cell().Element(CellStyle).Text(r.Student?.Company?.name ?? "-");
                                table.Cell().Element(CellStyle).Text(r.description ?? "-");
                                table.Cell().Element(CellStyle).Text(r.feedback ?? "-");
                            }

                            IContainer CellStyle(IContainer container) =>
                                container
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .PaddingVertical(5)
                                    .PaddingHorizontal(5);
                        });
                    });
                });
            }).GeneratePdf();
        }
    }
}