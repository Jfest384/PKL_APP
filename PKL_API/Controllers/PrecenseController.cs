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
    [Route("api/presence")]
    [ApiController]
    public class PrecenseController : ControllerBase
    {
        private readonly PklContext _db;
        private readonly UserAccessHelper _userAccessHelper;
        private readonly ILogger<PrecenseController> _logger;

        private static string ToIndonesianLongDate(DateOnly date)
        {
            var culture = new System.Globalization.CultureInfo("id-ID");
            // "dddd, dd MMMM yyyy" → Sabtu, 12 Juli 2025
            return date.ToString("dddd, dd MMMM yyyy", culture);
        }

        public PrecenseController(PklContext db, UserAccessHelper userAccessHelper, ILogger<PrecenseController> logger)
        {
            _db = db;
            _userAccessHelper = userAccessHelper;
            _logger = logger;
        }

        [Authorize]
        [HttpPost]
        [RequestSizeLimit(5_000_000)]
        public async Task<IActionResult> SubmitPrecense([FromForm] PrecenseDTO dto)
        {
            var userIdClaim = User.FindFirst("id")?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized("Invalid user token");

            var student = await _db.Students
                .Include(s => s.User)
                .Include(s => s.Mentor)
                .FirstOrDefaultAsync(s => s.Userid == userId);
            if (student == null)
                return NotFound("Student not found");

            var detail = new PresenceDetail();

            async Task<Guid?> SavePhotoAsync(IFormFile? file)
            {
                if (file == null || file.Length == 0)
                    return null;

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var photo = new PresencePhoto
                {
                    id = Guid.NewGuid(),
                    photo = ms.ToArray(),
                    extension = Path.GetExtension(file.FileName)
                };
                _db.PresencePhotos.Add(photo);
                return photo.id;
            }

            detail.FullBodyPhotoid = await SavePhotoAsync(dto.FullBodyPhoto);
            detail.MedicalCertificatePhotoid = await SavePhotoAsync(dto.MedicalCertificate);
            detail.TreatmentPhotoid = await SavePhotoAsync(dto.Treatment);
            detail.SickToCompanyPhotoid = await SavePhotoAsync(dto.SickToCompany);
            detail.SickToMentorPhotoid = await SavePhotoAsync(dto.SickToMentor);
            detail.SickToWalasPhotoid = await SavePhotoAsync(dto.SickToWalas);
            detail.PermitToCompanyPhotoid = await SavePhotoAsync(dto.PermitToCompany);
            detail.PermitToMentorPhotoid = await SavePhotoAsync(dto.PermitToMentor);
            detail.PermitToWalasPhotoid = await SavePhotoAsync(dto.PermitToWalas);
            detail.ActivityPhotoid = await SavePhotoAsync(dto.Activity);
            detail.HolidayFromCompanyPhotoid = await SavePhotoAsync(dto.HolidayFromCompany);

            if (dto.Lat.HasValue)
                detail.lat = Math.Round(dto.Lat.Value, 7, MidpointRounding.AwayFromZero);
            if (dto.Long.HasValue)
                detail.longitude = Math.Round(dto.Long.Value, 7, MidpointRounding.AwayFromZero);

            _db.PresenceDetails.Add(detail);
            await _db.SaveChangesAsync();

            var presenceType = await _db.Set<PresenceType>().FindAsync(dto.PresenceTypeid);
            if (presenceType == null)
                return NotFound("Presence type not found");

            var presence = new Presence
            {
                Studentid = student.id,
                date = DateOnly.FromDateTime(DateTime.Now),
                time = TimeOnly.FromDateTime(DateTime.Now),
                PresenceTypeid = dto.PresenceTypeid,
                feedback = null,
                PresenceDetailid = detail.id,
                Mentorid = student.Mentorid ?? throw new Exception("Student does not have a mentor assigned."),
                Classroomid = student.Classroomid ?? throw new Exception("Student does not have a classroom assigned."),
            };

            _db.Presences.Add(presence);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Presence submitted successfully" });
        }

        [Authorize]
        [HttpPut("feedback/{presenceId}")]
        public async Task<IActionResult> GiveFeedback(int presenceId, FeedbackDTO feedbackDTO)
        {
            if (feedbackDTO == null || string.IsNullOrWhiteSpace(feedbackDTO.feedback))
                return BadRequest("Feedback is required.");
            int userId, roleId;
            try
            {
                (userId, roleId) = await _userAccessHelper.GetUserIdAndRoleAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            // Only allow role id 3 (mentor) to give feedback
            if (roleId == 1 || roleId == 2 || roleId == 6)
                return StatusCode(403, "You are not allowed to give feedback.");

            var presence = await _db.Presences
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.id == presenceId);
            if (presence == null)
                return NotFound("Report not found.");

            if (roleId == 3)
            {
                var mentor = await _db.Mentors.FirstOrDefaultAsync(m => m.Userid == userId);
                if (mentor == null)
                    return Unauthorized("Mentor data not found.");

                // Cek apakah student pada report dimentori oleh mentor ini
                if (presence.Student?.Mentorid != mentor.id)
                    return StatusCode(403, "You can only give feedback to your own students' reports.");
            }

            presence.feedback = feedbackDTO.feedback;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Feedback submitted successfully" });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetPresences(
            [FromQuery] DateOnly? date = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int? classId = null,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            int userId, roleId;
            try
            {
                (userId, roleId) = await _userAccessHelper.GetUserIdAndRoleAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            IQueryable<Presence> query = _db.Presences
                .Include(p => p.Student)
                    .ThenInclude(s => s.Classroom!)
                .Include(p => p.Student)
                    .ThenInclude(s => s.Mentor!)
                .Include(p => p.PresenceType)
                .Include(p => p.Detail);

            if (roleId == 2) // Student
            {
                var student = await _db.Students.FirstOrDefaultAsync(s => s.Userid == userId);
                if (student == null)
                    return BadRequest("Student data not found.");
                query = query.Where(p => p.Studentid == student.id);
                var totalCount = await query.CountAsync();
                var presences = await query
                    .OrderByDescending(p => p.date)
                    .ThenByDescending(p => p.time)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new
                    {
                        id_presence = p.id.ToString(),
                        date = ToIndonesianLongDate(p.date),
                        time = p.time.ToString("HH:mm:ss"),
                        nis = p.Student != null ? p.Student.nis ?? "-" : "-",
                        name = p.Student != null ? p.Student.User.fullname ?? "-" : "-",
                        classroom_name = p.Student != null && p.Student.Classroom != null ? p.Student.Classroom.name ?? "-" : "-",
                        presence_type = p.PresenceType != null ? p.PresenceType.name : "-",
                        feedback = p.feedback ?? "-",
                        lat = p.Detail != null ? (p.Detail.lat.ToString()) : "-",
                        longitude = p.Detail != null ? (p.Detail.longitude.ToString()) : "-"
                    })
                    .ToListAsync();
                return Ok(new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    data = presences
                });
            }
            // Replace the following code blocks in the GetPresences method:

            // For Mentor (roleId == 3)
            else if (roleId == 3) // Mentor
            {
                var mentor = await _db.Mentors.FirstOrDefaultAsync(m => m.Userid == userId);
                if (mentor == null)
                    return BadRequest("Mentor data not found.");

                var filterDate = date ?? DateOnly.FromDateTime(DateTime.Now);

                var studentsQuery = _db.Students
                    .Include(s => s.User)
                    .Include(s => s.Classroom)
                    .Where(s => s.Mentorid == mentor.id && s.isPKL);

                if (classId.HasValue)
                    studentsQuery = studentsQuery.Where(s => s.Classroomid == classId.Value);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    studentsQuery = studentsQuery.Where(s => s.User.fullname.ToLower().Contains(searchLower));
                }

                var students = await studentsQuery.ToListAsync();

                var presencesOnDate = await _db.Presences
                    .Include(p => p.Detail)
                    .Include(p => p.PresenceType)
                    .Where(p => p.Mentorid == mentor.id && p.date == filterDate)
                    .ToListAsync();

                var result = students.Select(s =>
                {
                    var presence = presencesOnDate.FirstOrDefault(p => p.Studentid == s.id);
                    return new
                    {
                        id_presence = presence?.id.ToString() ?? "-",
                        nis = s.nis ?? "-",
                        name = s.User.fullname ?? "-",
                        classroom_name = s.Classroom != null ? s.Classroom.name ?? "-" : "-",
                        date = ToIndonesianLongDate(filterDate),
                        time = presence != null ? presence.time.ToString("HH:mm:ss") : "-",
                        presence_type = presence?.PresenceType?.name ?? "-",
                        id_detail = presence?.PresenceDetailid.ToString() ?? "-",
                        feedback = presence?.feedback ?? "-",
                        isPresence = presence != null ? "✔️" : "❌",
                        lat = presence?.Detail != null ? presence.Detail.lat.ToString() : "-",
                        longitude = presence?.Detail?.longitude?.ToString() ?? "-"
                    };
                }).ToList();

                var totalCount = result.Count;
                var pagedResult = result.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Ok(new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    data = pagedResult
                });
            }

            // For Wali Kelas (roleId == 5)
            else if (roleId == 5) // Wali Kelas
            {
                var waliKelas = await _db.WaliKelas.FirstOrDefaultAsync(wk => wk.Userid == userId);
                if (waliKelas == null)
                    return BadRequest("Homeroom teacher data not found.");

                var filterDate = date ?? DateOnly.FromDateTime(DateTime.Now);

                var classroom = await _db.Classrooms.FirstOrDefaultAsync(c => c.WaliKelasid == waliKelas.id);
                if (classroom == null)
                    return BadRequest("Classroom for this homeroom teacher not found.");

                var studentsQuery = _db.Students
                    .Include(s => s.User)
                    .Include(s => s.Classroom)
                    .Where(s => s.Classroomid == classroom.id && s.isPKL);

                if (classId.HasValue)
                    studentsQuery = studentsQuery.Where(s => s.Classroomid == classId.Value);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    studentsQuery = studentsQuery.Where(s => s.User.fullname.ToLower().Contains(searchLower));
                }

                var students = await studentsQuery.ToListAsync();

                var studentIds = students.Select(s => s.id).ToList();

                var presencesOnDate = await _db.Presences
                    .Include(p => p.Detail)
                    .Include(p => p.PresenceType)
                    .Where(p => studentIds.Contains(p.Studentid) && p.date == filterDate)
                    .ToListAsync();

                var result = students.Select(s =>
                {
                    var presence = presencesOnDate.FirstOrDefault(p => p.Studentid == s.id);
                    return new
                    {
                        id_presence = presence?.id.ToString() ?? "-",
                        nis = s.nis ?? "-",
                        name = s.User.fullname ?? "-",
                        classroom_name = s.Classroom != null ? s.Classroom.name ?? "-" : "-",
                        date = ToIndonesianLongDate(filterDate),
                        time = presence != null ? presence.time.ToString("HH:mm:ss") : "-",
                        presence_type = presence?.PresenceType?.name ?? "-",
                        feedback = presence?.feedback ?? "-",
                        isPresence = presence != null ? "✔️" : "❌",
                        lat = presence?.Detail != null ? presence.Detail.lat.ToString() : "-",
                        longitude = presence?.Detail != null ? presence.Detail.longitude.ToString() : "-"
                    };
                }).ToList();

                var totalCount = result.Count;
                var pagedResult = result.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Ok(new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    data = pagedResult,
                });
            }

            // For Admin/Operator (roleId == 1 || roleId == 4)
            else if (roleId == 1 || roleId == 4)
            {
                var studentsQuery = _db.Students
                    .Include(s => s.User)
                    .Include(s => s.Classroom)
                    .Where(s => s.isPKL);

                if (classId.HasValue)
                {
                    studentsQuery = studentsQuery.Where(s => s.Classroomid == classId.Value);
                }
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    studentsQuery = studentsQuery.Where(s => s.User.fullname.ToLower().Contains(searchLower));
                }

                var students = await studentsQuery.ToListAsync();
                var studentIds = students.Select(s => s.id).ToList();

                var filterDate = date ?? DateOnly.FromDateTime(DateTime.Now);

                var presencesOnDate = await _db.Presences
                    .Include(p => p.Detail)
                    .Include(p => p.PresenceType)
                    .Where(p => studentIds.Contains(p.Studentid) && p.date == filterDate)
                    .ToListAsync();

                var result = students.Select(s =>
                {
                    var presence = presencesOnDate.FirstOrDefault(p => p.Studentid == s.id);
                    return new
                    {
                        id_presence = presence?.id.ToString() ?? "-",
                        nis = s.nis ?? "-",
                        name = s.User.fullname ?? "-",
                        classId = s.Classroomid,
                        classroom_name = s.Classroom != null ? s.Classroom.name ?? "-" : "-",
                        date = ToIndonesianLongDate(filterDate),
                        time = presence != null ? presence.time.ToString("HH:mm:ss") : "-",
                        presence_type = presence?.PresenceType?.name ?? "-",
                        feedback = presence?.feedback ?? "-",
                        isPresence = presence != null ? "✔️" : "❌",
                        lat = presence?.Detail != null ? presence.Detail.lat.ToString() : "-",
                        longitude = presence?.Detail != null ? presence.Detail.longitude.ToString() : "-"
                    };
                }).ToList();

                var totalCount = result.Count;
                var pagedResult = result.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Ok(new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    data = pagedResult
                });
            }

            // Fallback: return BadRequest if role is not handled
            return BadRequest("Role not supported.");
        }

        [Authorize]
        [HttpGet("{presenceId}/photos")]
        public async Task<IActionResult> GetPhotosForPresence(int presenceId)
        {
            var presence = await _db.Presences
                .Include(p => p.Detail)
                    .ThenInclude(d => d.FullBodyPhoto)
                .Include(p => p.Detail)
                    .ThenInclude(d => d.MedicalCertificatePhoto)
                .Include(p => p.Detail)
                    .ThenInclude(d => d.ActivityPhoto)
                .Include(p => p.Detail)
                    .ThenInclude(d => d.TreatmentPhoto)
                .Include(p => p.Detail)
                    .ThenInclude(d => d.SickToCompanyPhoto)
                .Include(p => p.Detail)
                    .ThenInclude(d => d.SickToMentorPhoto)
                .Include(p => p.Detail)
                    .ThenInclude(d => d.SickToWalasPhoto)
                .Include(p => p.Detail)
                    .ThenInclude(d => d.PermitToCompanyPhoto)
                .Include(p => p.Detail)
                    .ThenInclude(d => d.PermitToWalasPhoto)
                .Include(p => p.Detail)
                    .ThenInclude(d => d.PermitToMentorPhoto)
                .Include(p => p.Detail)
                    .ThenInclude(d => d.HolidayFromCompanyPhoto)
                .FirstOrDefaultAsync(p => p.id == presenceId);

            if (presence == null || presence.Detail == null)
                return NotFound("Presence or detail not found.");

            var detail = presence.Detail;

            // Mapping properti navigation ke key
            var photoMap = new Dictionary<string, PresencePhoto?>
            {
                { "photoBody", detail.FullBodyPhoto },
                { "medicalCertificate", detail.MedicalCertificatePhoto },
                { "activity", detail.ActivityPhoto },
                { "treatment", detail.TreatmentPhoto },
                { "sickToCompany", detail.SickToCompanyPhoto },
                { "sickToMentor", detail.SickToMentorPhoto },
                { "sickToWalas", detail.SickToWalasPhoto },
                { "permitToCompany", detail.PermitToCompanyPhoto },
                { "permitToMentor", detail.PermitToMentorPhoto },
                { "permitToWalas", detail.PermitToWalasPhoto },
                { "holidayFromCompany", detail.HolidayFromCompanyPhoto }
            };

            var result = photoMap
                .Where(entry => entry.Value != null)
                .Select(entry => new
                {
                    id = entry.Value!.id,
                    type = entry.Key,
                    extension = entry.Value!.extension.Trim(),
                    url = $"/api/presence/photos/{entry.Value!.id}"
                })
                .ToList();

            return Ok(result);
        }

        [Authorize]
        [HttpGet("photos/{id}")]
        public async Task<IActionResult> GetPhoto(Guid id)
        {
            var photo = await _db.PresencePhotos.FindAsync(id);
            if (photo == null)
                return NotFound();

            var ext = photo.extension.Trim().ToLower();
            var contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };

            return File(photo.photo, contentType);
        }

        [Authorize]
        [HttpGet("byStudent/{studentId}/print")]
        public async Task<IActionResult> PrintPresenceByStudent(int studentId,
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

            // Validasi student dan ambil data lengkap
            var student = await _db.Students
                .Include(s => s.User)
                .Include(s => s.Classroom)
                .Include(s => s.Company)
                .Include(s => s.Mentor)
                //.ThenInclude(m => m.User)
                .FirstOrDefaultAsync(s => s.id == studentId);

            if (student == null)
                return NotFound("Student not found.");

            if (roleId == 3)
            {
                var mentor = await _db.Mentors.FirstOrDefaultAsync(m => m.Userid == userId);
                if (mentor == null)
                    return StatusCode(403, "Mentor data not found.");
                if (student.Mentorid != mentor.id)
                    return StatusCode(403, "You can only print presences for your own mentees.");
            }

            // Jika wali kelas, hanya boleh print siswa perwaliannya
            if (roleId == 5)
            {
                var waliKelas = await _db.WaliKelas
                    .FirstOrDefaultAsync(wk => wk.Userid == userId);
                if (waliKelas == null)
                    return StatusCode(403, "You are not assigned as a homeroom teacher for any class.");
                var classroom = await _db.Classrooms.FirstOrDefaultAsync(c => c.WaliKelasid == waliKelas.Userid);
                if (classroom == null)
                    return StatusCode(403, "No classroom assigned to your homeroom teacher role.");
                if (student.Classroomid != classroom.id)
                    return StatusCode(403, "You can only print presences for students in your homeroom class.");
            }

            var query = _db.Presences
                .Include(r => r.Classroom)
                .Include(r => r.Student)
                    .ThenInclude(s => s.Company)
                .Include(r => r.PresenceType)
                .Where(r => r.Studentid == studentId);

            if (startDate.HasValue)
                query = query.Where(r => r.date >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(r => r.date <= endDate.Value);

            var presences = await query
                .OrderBy(r => r.date)
                .ThenBy(r => r.time)
                .ToListAsync();

            var pdfBytes = GenerateStudentPresencePdf(student, presences, startDate, endDate);
            var fileName = $"StudentPresence_{student.nis}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        private static byte[] GenerateStudentPresencePdf(
            Student student,
            List<Presence> presences,
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
                        .Text("Student Presence")
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
                                    columns.RelativeColumn();   // Nilai
                                });

                                table.Cell().Element(CellStyle).Text("NIS");
                                table.Cell().Element(CellStyle).Text($":  {student.nis ?? "-"}");

                                table.Cell().Element(CellStyle).Text("Name");
                                table.Cell().Element(CellStyle).Text($":  {student.User.fullname ?? "-"}");
                            });

                            // Sisi kanan: Class & Mentor
                            row.RelativeColumn().PaddingLeft(85).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(45); // Label
                                    columns.RelativeColumn();   // Nilai
                                });

                                table.Cell().Element(CellStyle).Text("Class");
                                table.Cell().Element(CellStyle).Text($":  {className}");

                                table.Cell().Element(CellStyle).Text("Mentor");
                                table.Cell().Element(CellStyle).Text($":  {mentorName}");
                            });

                            IContainer CellStyle(IContainer container) =>
                                container.PaddingVertical(2);
                        });

                        // Table laporan
                        col.Item().PaddingTop(20).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(85); // Date
                                columns.RelativeColumn(2);  // Company
                                columns.RelativeColumn(1);  // Type
                                columns.RelativeColumn(2);  // Feedback
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Date").Bold();
                                header.Cell().Element(CellStyle).Text("Company").Bold();
                                header.Cell().Element(CellStyle).Text("Type").Bold();
                                header.Cell().Element(CellStyle).Text("Feedback").Bold();
                            });

                            foreach (var r in presences)
                            {
                                table.Cell().Element(CellStyle).Text(r.date.ToString("yyyy-MM-dd"));
                                table.Cell().Element(CellStyle).Text(r.Student?.Company?.name ?? "-");
                                table.Cell().Element(CellStyle).Text(r.PresenceType?.name ?? "-");
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
        public async Task<IActionResult> PrintReportByClass(int? classId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate
        )
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

            if (roleId == 5)
            {
                var waliKelas = await _db.WaliKelas
                    .Include(wk => wk.User)
                    .FirstOrDefaultAsync(wk => wk.User.id == userId);

                if (waliKelas == null)
                    return NotFound("You are not assigned as a homeroom teacher for any class.");

                classroom = await _db.Classrooms
                    .FirstOrDefaultAsync(c => c.WaliKelasid == waliKelas.id);
                if (classroom == null)
                    return NotFound("Classroom not found for your homeroom assignment.");
                classId = classroom.id;
            }
            else
            {
                if (!classId.HasValue)
                    return BadRequest("ClassId is required.");
                classroom = await _db.Classrooms
                    .Include(c => c.Students)
                    .FirstOrDefaultAsync(c => c.id == classId.Value);

                if (classroom == null)
                    return NotFound("Classroom not found.");
            }

            // Ambil semua student PKL di kelas ini
            var students = await _db.Students
                .Include(s => s.User)
                .Where(s => s.Classroomid == classroom.id && s.isPKL)
                .OrderBy(s => s.nis)
                .ToListAsync();

            // Tentukan rentang tanggal
            var minDate = startDate ?? await _db.Presences
                .Where(p => p.Classroomid == classroom.id)
                .OrderBy(p => p.date)
                .Select(p => (DateOnly?)p.date)
                .FirstOrDefaultAsync() ?? DateOnly.FromDateTime(DateTime.Now);
            var maxDate = endDate ?? await _db.Presences
                .Where(p => p.Classroomid == classroom.id)
                .OrderByDescending(p => p.date)
                .Select(p => (DateOnly?)p.date)
                .FirstOrDefaultAsync() ?? DateOnly.FromDateTime(DateTime.Now);

            // Buat list tanggal (tanpa hari minggu)
            var dates = new List<DateOnly>();
            for (var d = minDate; d <= maxDate; d = d.AddDays(1))
            {
                var dt = d.ToDateTime(TimeOnly.MinValue);
                if (dt.DayOfWeek != DayOfWeek.Sunday)
                    dates.Add(d);
            }

            // Batasi 10 tanggal per halaman
            var dateChunks = dates
                .Select((date, idx) => new { date, idx })
                .GroupBy(x => x.idx / 15)
                .Select(g => g.Select(x => x.date).ToList())
                .ToList();

            // Ambil presensi untuk semua student dan tanggal dalam rentang
            var studentIds = students.Select(s => s.id).ToList();
            var allPresences = await _db.Presences
                .Include(p => p.PresenceType)
                .Where(p => studentIds.Contains(p.Studentid) && p.date >= minDate && p.date <= maxDate)
                .ToListAsync();

            foreach (var p in allPresences)
            {
                if (p.PresenceType == null)
                {
                    // Log id presence yang bermasalah
                    Console.WriteLine($"Presence id {p.id} type null, PresenceTypeid: {p.PresenceTypeid}");
                }
                _logger.LogInformation("PresenceId: {id}, PresenceTypeId: {pid}, Name: [{name}]",
                    p.id, p.PresenceTypeid, p.PresenceType?.name ?? "<NULL>");

            }

            var presenceDict = allPresences
                .GroupBy(p => (p.Studentid, p.date))
                .ToDictionary(g => g.Key, g => g.First());

            var pdfBytes = GenerateClassPresenceMatrixPdf(classroom, students, dateChunks, presenceDict);
            var fileName = $"ClassPresenceMatrix_{classroom.name}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        private static byte[] GenerateClassPresenceMatrixPdf(
            Classroom classroom,
            List<Student> students,
            List<List<DateOnly>> dateChunks,
            Dictionary<(int studentId, DateOnly date), Presence> presenceDict
        )
        {
            return Document.Create(container =>
            {
                foreach (var chunk in dateChunks)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(30);
                        page.DefaultTextStyle(x => x.FontSize(12));
                        page.PageColor(Colors.White);

                        page.Header()
                            .Text($"Class PKL Presence Matrix - {classroom.name}")
                            .FontSize(18)
                            .Bold()
                            .AlignCenter();

                        page.Content().PaddingTop(10).Column(col =>
                        {
                            // Header tanggal
                            col.Item().PaddingBottom(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(60); // NIS
                                    columns.RelativeColumn(2); // Name
                                    foreach (var _ in chunk)
                                        columns.RelativeColumn(1);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("NIS").Bold();
                                    header.Cell().Element(CellStyle).Text("Name").Bold();
                                    foreach (var d in chunk)
                                        header.Cell().Element(CellStyle).Text($"{d:MM-dd}");
                                });

                                // Data rows
                                foreach (var s in students)
                                {
                                    table.Cell().Element(CellStyle).Text(s.nis);
                                    table.Cell().Element(CellStyle).Text(s.User.fullname);
                                    foreach (var d in chunk)
                                    {
                                        if (presenceDict.TryGetValue((s.id, d), out var p))
                                        {
                                            table.Cell().Element(CellStyle).Text(p.PresenceType?.name ?? "-");
                                        }
                                        else
                                        {
                                            table.Cell().Element(CellStyle).Text("-");
                                        }
                                    }
                                }

                                IContainer CellStyle(IContainer container) =>
                                    container
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Grey.Lighten2)
                                        .PaddingVertical(4)
                                        .PaddingHorizontal(4);
                            });
                        });
                    });
                }
            }).GeneratePdf();
        }
    }
}
