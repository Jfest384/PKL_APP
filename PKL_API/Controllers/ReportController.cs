using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PKL_API.Helpers;
using PKL_API.Models;
using PKL_API.Models.DTO;

namespace PKL_API.Controllers
{
    [Route("reports")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly PklContext _db;
        private readonly UserAccessHelper _userAccessHelper;

        public static string ToIndonesianLongDate(DateOnly date)
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
                .Include(s => s.StudentValidation)
                .FirstOrDefaultAsync(s => s.Userid == userId);

            if (student == null || !(student.StudentValidation.isPKL))
                return StatusCode(403, "Only active PKL students can submit reports.");

            if (string.IsNullOrWhiteSpace(dto.description))
                return BadRequest("Description is required.");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };

            bool IsValidFile(IFormFile? file)
            {
                if (file == null) return true;
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                return allowedExtensions.Contains(ext);
            }

            if (!IsValidFile(dto.GuidancePhoto) || !IsValidFile(dto.ReportFile))
                return BadRequest("Only PNG, JPG, JPEG, or PDF files are allowed.");

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

            await _db.SaveChangesAsync();

            var report = new Report
            {
                Studentid = student.id,
                date = DateOnly.FromDateTime(now),
                time = TimeOnly.FromDateTime(now),
                Mentorid = student.Mentorid,
                Classroomid = student.Classroomid ?? throw new Exception("No classroom assigned."),
                description = dto.description,
                ReportPhotoid = photoEntity?.id,
                ReportFileid = fileEntity?.id
            };

            _db.Reports.Add(report);

            DateTime GetStartOfWeek(DateTime date)
            {
                int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
                return date.AddDays(-diff).Date;
            }
            var weekStartDate = GetStartOfWeek(now);

            _db.WeeklyGuidances.Add(new WeeklyGuidance
            {
                Studentid = student.id,
                Mentorid = student.Mentorid,
                WeekStartDate = weekStartDate,
                CreatedAt = now
            });

            student.StudentValidation.isReport = true;
            student.StudentValidation.update_daily = DateTime.Now;
            _db.StudentValidations.Update(student.StudentValidation);
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

            int userId, roleId;
            try
            {
                (userId, roleId) = await _userAccessHelper.GetUserIdAndRoleAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            if (roleId == 2 || roleId == 6)
                return StatusCode(403, "You are not allowed to give feedback.");

            var report = await _db.Reports
                .Include(r => r.Student)
                .Include(r => r.ReportFeedback)
                .FirstOrDefaultAsync(r => r.id == reportId);

            if (report == null)
                return NotFound("Report not found.");

            ReportFeedback feedback = report.ReportFeedback ?? new ReportFeedback();

            if (roleId == 1 || roleId == 4)
                feedback.kajur = feedbackDTO.feedback;
            else if (roleId == 3) feedback.mentor = feedbackDTO.feedback;
            else if (roleId == 5) feedback.walas = feedbackDTO.feedback;
            else if (roleId == 3 && roleId == 5)
            {
                feedback.mentor = feedbackDTO.feedback;
                feedback.walas = feedbackDTO.feedback;
            }

            if (report.ReportFeedback == null)
            {
                _db.ReportFeedbacks.Add(feedback);
                await _db.SaveChangesAsync();
                report.ReportFeedbackid = feedback.id;
            }
            else _db.ReportFeedbacks.Update(feedback);

            await _db.SaveChangesAsync();
            return Ok(new { message = "Feedback submitted successfully" });
        }

        [Authorize]
        [HttpDelete("delete/{reportId}")]
        public async Task<IActionResult> DeleteReport(int reportId)
        {
            var report = await _db.Reports
                .FirstOrDefaultAsync(r => r.id == reportId);

            if (report == null)
                return NotFound("Report not found.");

            if (report.ReportFeedbackid.HasValue)
            {
                var feedback = await _db.ReportFeedbacks
                    .FirstOrDefaultAsync(f => f.id == report.ReportFeedbackid.Value);
                if (feedback != null)
                    _db.ReportFeedbacks.Remove(feedback);
            }

            // Hapus ReportFile (file utama) jika ada
            if (report.ReportFileid.HasValue)
            {
                var file = await _db.ReportFiles
                    .FirstOrDefaultAsync(f => f.id == report.ReportFileid.Value);
                if (file != null)
                    _db.ReportFiles.Remove(file);
            }

            // Hapus ReportPhoto jika ada
            if (report.ReportPhotoid.HasValue)
            {
                var photo = await _db.ReportFiles
                    .FirstOrDefaultAsync(f => f.id == report.ReportPhotoid.Value);
                if (photo != null)
                    _db.ReportFiles.Remove(photo);
            }

            _db.Reports.Remove(report);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Report and related data deleted successfully." });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetReports(
            [FromQuery] DateOnly? date = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int? classId = null,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var userIdClaim = User.FindFirst("id")?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized("Invalid user token");

            // Ambil semua role user
            var roleIds = await _db.UserRoles
                .Where(ur => ur.User.id == userId)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            DateTime GetStartOfWeek(DateOnly d)
            {
                var dt = d.ToDateTime(TimeOnly.MinValue);
                int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
                return dt.AddDays(-diff).Date;
            }

            if (roleIds.Contains(2))
            {
                var student = await _db.Students.FirstOrDefaultAsync(s => s.Userid == userId);
                if (student == null)
                    return BadRequest("Student data not found.");

                var query = _db.Reports
                    .Include(r => r.Student).ThenInclude(s => s.Classroom)
                    .Include(r => r.Student).ThenInclude(s => s.Company)
                    .Include(r => r.Student).ThenInclude(s => s.User)
                    .Include(r => r.Mentor).ThenInclude(m => m.User)
                    .Include(r => r.ReportFile)
                    .Include(r => r.ReportPhoto)
                    .Include(p => p.ReportFeedback)
                    .Where(r => r.Studentid == student.id);

                var totalCount = await query.CountAsync();
                var reports = await query
                    .OrderByDescending(r => r.date)
                    .ThenByDescending(r => r.time)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new
                    {
                        id = r.id.ToString(),
                        nis = r.Student.nis ?? "-",
                        classroom_name = r.Classroom.name ?? "-",
                        company_name = r.Student.Company.name ?? "-",
                        date = ToIndonesianLongDate(r.date),
                        time = r.time.ToString("HH:mm:ss"),
                        name = r.Student.User.fullname ?? "-",
                        description = r.description,
                        feedback = r.ReportFeedback != null
                        ? new
                        {
                            kajur = !string.IsNullOrWhiteSpace(r.ReportFeedback.kajur) ? r.ReportFeedback.kajur : "-",
                            mentor = !string.IsNullOrWhiteSpace(r.ReportFeedback.mentor) ? r.ReportFeedback.mentor : "-",
                            walas = !string.IsNullOrWhiteSpace(r.ReportFeedback.walas) ? r.ReportFeedback.walas : "-"
                        }
                        : new
                        {
                            kajur = "-",
                            mentor = "-",
                            walas = "-"
                        },
                        reportFileId = r.ReportFileid,
                        reportPhotoId = r.ReportPhotoid,
                    })
                    .ToListAsync();

                return Ok(new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    data = reports
                });
            }

            else if (roleIds.Contains(3) && roleIds.Contains(5))
            {
                var filterDate = date ?? DateOnly.FromDateTime(DateTime.Now);
                var mentor = await _db.Mentors.FirstOrDefaultAsync(m => m.Userid == userId);
                var waliKelas = await _db.WaliKelas.FirstOrDefaultAsync(wk => wk.Userid == userId);
                var classroom = waliKelas != null
                    ? await _db.Classrooms.FirstOrDefaultAsync(c => c.WaliKelasid == waliKelas.id) : null;

                var mentorStudentsQuery = _db.Students
                    .Include(s => s.User)
                    .Include(s => s.Classroom)
                    .Include(s => s.Company)
                    .Where(s => s.Mentorid == mentor.id && (s.StudentValidation.isPKL));

                var waliKelasStudentsQuery = classroom != null
                    ? _db.Students
                        .Include(s => s.User)
                        .Include(s => s.Classroom)
                        .Include(s => s.Company)
                        .Where(s => s.Classroomid == classroom.id && (s.StudentValidation.isPKL))
                    : Enumerable.Empty<Student>().AsQueryable();

                var studentsQuery = mentorStudentsQuery.Union(waliKelasStudentsQuery);

                if (classId.HasValue)
                    studentsQuery = studentsQuery.Where(s => s.Classroomid == classId.Value);

                var students = await studentsQuery.ToListAsync();
                var studentIds = students.Select(s => s.id).ToList();

                // Ambil report
                var reportsQuery = _db.Reports
                    .Include(r => r.ReportFile)
                    .Include(r => r.ReportPhoto)
                    .Include(p => p.ReportFeedback)
                    .Where(r => studentIds.Contains(r.Studentid) && r.date == filterDate);

                var reportsOnDate = await reportsQuery.ToListAsync();

                var weekStart = GetStartOfWeek(filterDate);
                var weekEnd = weekStart.AddDays(6);
                var weeklyGuidances = await _db.WeeklyGuidances
                    .Where(wg => studentIds.Contains(wg.Studentid) && wg.WeekStartDate == weekStart)
                    .ToListAsync();

                var result = students.Select(s =>
                {
                    var report = reportsOnDate.FirstOrDefault(r => r.Studentid == s.id);
                    var hasGuidance = weeklyGuidances.Any(wg => wg.Studentid == s.id);
                    return new
                    {
                        id = report?.id.ToString() ?? "-",
                        studentId = report?.Studentid.ToString() ?? "-",
                        nis = s.nis ?? "-",
                        name = s?.User?.fullname ?? "-",
                        classroom_name = s?.Classroom?.name ?? "-",
                        company_name = s?.Company?.name ?? "-",
                        date = ToIndonesianLongDate(filterDate),
                        time = report != null ? report.time.ToString("HH:mm:ss") : "-",
                        description = report?.description ?? "-",
                        feedback = report != null && report?.ReportFeedback != null
                            ? new
                            {
                                kajur = !string.IsNullOrWhiteSpace(report.ReportFeedback.kajur) ? report.ReportFeedback.kajur : "-",
                                mentor = !string.IsNullOrWhiteSpace(report.ReportFeedback.mentor) ? report.ReportFeedback.mentor : "-",
                                walas = !string.IsNullOrWhiteSpace(report.ReportFeedback.walas) ? report.ReportFeedback.walas : "-"
                            }
                            : new
                            {
                                kajur = "-",
                                mentor = "-",
                                walas = "-"
                            },
                        reportFileId = report?.ReportFileid != null ? report.ReportFileid.ToString() : "-",
                        reportPhotoId = report?.ReportPhotoid != null ? report.ReportPhotoid.ToString() : "-",
                        isGuidance = hasGuidance ? "✔️" : "❌"
                    };
                });

                // Search di hasil join (nama, nis, deskripsi)
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    result = result.Where(r =>
                        (r.name.ToLower().Contains(searchLower)) ||
                        (r.nis.ToLower().Contains(searchLower)) ||
                        (r.classroom_name.ToLower().Contains(searchLower)) ||
                        (r.company_name.ToLower().Contains(searchLower))
                    );
                }

                var resultList = result.ToList();
                var totalCount = resultList.Count;
                List<object> pagedResult;
                if (pageSize < 0 || pageSize >= totalCount)
                {
                    pagedResult = resultList.Cast<object>().ToList();
                    pageSize = totalCount;
                    page = 1;
                }
                else pagedResult = resultList.Skip((page - 1) * pageSize).Take(pageSize).Cast<object>().ToList();

                return Ok(new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    data = pagedResult
                });
            }

            // Mentor saja: tampilkan semua student yang diampunya (data perhari)
            else if (roleIds.Contains(3))
            {
                var mentor = await _db.Mentors.FirstOrDefaultAsync(m => m.Userid == userId);
                if (mentor == null)
                    return BadRequest("Mentor data not found.");

                var filterDate = date ?? DateOnly.FromDateTime(DateTime.Now);

                // Ambil semua siswa PKL bimbingan mentor ini
                var studentsQuery = _db.Students
                    .Include(s => s.User)
                    .Include(s => s.Classroom)
                    .Include(s => s.Company)
                    .Where(s => s.Mentorid == mentor.id && (s.StudentValidation.isPKL));

                if (classId.HasValue)
                    studentsQuery = studentsQuery.Where(s => s.Classroomid == classId.Value);

                var students = await studentsQuery.ToListAsync();
                var studentIds = students.Select(s => s.id).ToList();

                // Ambil report hari itu
                var reportsQuery = _db.Reports
                    .Include(r => r.ReportFile)
                    .Include(r => r.ReportPhoto)
                    .Include(p => p.ReportFeedback)
                    .Where(r => r.Mentorid == mentor.id && r.date == filterDate);

                var reportsOnDate = await reportsQuery.ToListAsync();

                var weekStart = GetStartOfWeek(filterDate);
                var weekEnd = weekStart.AddDays(6);
                var weeklyGuidances = await _db.WeeklyGuidances
                    .Where(wg => studentIds.Contains(wg.Studentid) && wg.WeekStartDate == weekStart)
                    .ToListAsync();

                var result = students.Select(s =>
                {
                    var report = reportsOnDate.FirstOrDefault(r => r.Studentid == s.id);
                    var hasGuidance = weeklyGuidances.Any(wg => wg.Studentid == s.id);
                    return new
                    {
                        id = report?.id.ToString() ?? "-",
                        studentId = report?.Studentid.ToString() ?? "-",
                        nis = s.nis ?? "-",
                        name = s?.User?.fullname ?? "-",
                        classroom_name = s?.Classroom?.name ?? "-",
                        company_name = s?.Company?.name ?? "-",
                        date = ToIndonesianLongDate(filterDate),
                        time = report != null ? report.time.ToString("HH:mm:ss") : "-",
                        description = report?.description ?? "-",
                        feedback = report != null && report?.ReportFeedback != null
                            ? new
                            {
                                kajur = !string.IsNullOrWhiteSpace(report.ReportFeedback.kajur) ? report.ReportFeedback.kajur : "-",
                                mentor = !string.IsNullOrWhiteSpace(report.ReportFeedback.mentor) ? report.ReportFeedback.mentor : "-",
                                walas = !string.IsNullOrWhiteSpace(report.ReportFeedback.walas) ? report.ReportFeedback.walas : "-"
                            }
                            : new
                            {
                                kajur = "-",
                                mentor = "-",
                                walas = "-"
                            },
                        reportFileId = report?.ReportFileid != null ? report.ReportFileid.ToString() : "-",
                        reportPhotoId = report?.ReportPhotoid != null ? report.ReportPhotoid.ToString() : "-",
                        isGuidance = hasGuidance ? "✔️" : "❌"
                    };
                });

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    result = result.Where(r =>
                        (r.name.ToLower().Contains(searchLower)) ||
                        (r.nis.ToLower().Contains(searchLower)) ||
                        (r.classroom_name.ToLower().Contains(searchLower)) ||
                        (r.company_name.ToLower().Contains(searchLower))
                    );
                }

                var resultList = result.ToList();
                var totalCount = resultList.Count;
                List<object> pagedResult;
                if (pageSize < 0 || pageSize >= totalCount)
                {
                    pagedResult = resultList.Cast<object>().ToList();
                    pageSize = totalCount;
                    page = 1;
                }
                else pagedResult = resultList.Skip((page - 1) * pageSize).Take(pageSize).Cast<object>().ToList();

                return Ok(new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    data = pagedResult
                });
            }

            // Wali Kelas saja: tampilkan semua student yang jadi waliannya (data perhari)
            else if (roleIds.Contains(5))
            {
                var waliKelas = await _db.WaliKelas.FirstOrDefaultAsync(wk => wk.Userid == userId);
                if (waliKelas == null)
                    return BadRequest("Homeroom teacher data not found.");

                var classroom = await _db.Classrooms.FirstOrDefaultAsync(c => c.WaliKelasid == waliKelas.id);
                if (classroom == null)
                    return BadRequest("Classroom for this homeroom teacher not found.");

                var filterDate = date ?? DateOnly.FromDateTime(DateTime.Now);

                var studentsQuery = _db.Students
                    .Include(s => s.User)
                    .Include(s => s.Classroom)
                    .Include(s => s.Company)
                    .Where(s => s.Classroomid == classroom.id && (s.StudentValidation.isPKL));

                if (classId.HasValue)
                    studentsQuery = studentsQuery.Where(s => s.Classroomid == classId.Value);

                var students = await studentsQuery.ToListAsync();
                var studentIds = students.Select(s => s.id).ToList();

                var reportsQuery = _db.Reports
                    .Include(r => r.ReportFile)
                    .Include(r => r.ReportPhoto)
                    .Include(p => p.ReportFeedback)
                    .Where(r => studentIds.Contains(r.Studentid) && r.date == filterDate);

                var reportsOnDate = await reportsQuery.ToListAsync();

                var weekStart = GetStartOfWeek(filterDate);
                var weekEnd = weekStart.AddDays(6);
                var weeklyGuidances = await _db.WeeklyGuidances
                    .Where(wg => studentIds.Contains(wg.Studentid) && wg.WeekStartDate == weekStart)
                    .ToListAsync();

                var result = students.Select(s =>
                {
                    var report = reportsOnDate.FirstOrDefault(r => r.Studentid == s.id);
                    var hasGuidance = weeklyGuidances.Any(wg => wg.Studentid == s.id);
                    return new
                    {
                        id = report?.id.ToString() ?? "-",
                        studentId = report?.Studentid.ToString() ?? "-",
                        nis = s.nis ?? "-",
                        name = s?.User?.fullname ?? "-",
                        classroom_name = s?.Classroom?.name ?? "-",
                        company_name = s?.Company?.name ?? "-",
                        date = ToIndonesianLongDate(filterDate),
                        time = report != null ? report.time.ToString("HH:mm:ss") : "-",
                        description = report?.description ?? "-",
                        feedback = report != null && report?.ReportFeedback != null
                            ? new
                            {
                                kajur = !string.IsNullOrWhiteSpace(report.ReportFeedback.kajur) ? report.ReportFeedback.kajur : "-",
                                mentor = !string.IsNullOrWhiteSpace(report.ReportFeedback.mentor) ? report.ReportFeedback.mentor : "-",
                                walas = !string.IsNullOrWhiteSpace(report.ReportFeedback.walas) ? report.ReportFeedback.walas : "-"
                            }
                            : new
                            {
                                kajur = "-",
                                mentor = "-",
                                walas = "-"
                            },
                        reportFileId = report?.ReportFileid != null ? report.ReportFileid.ToString() : "-",
                        reportPhotoId = report?.ReportPhotoid != null ? report.ReportPhotoid.ToString() : "-",
                        isGuidance = hasGuidance ? "✔️" : "❌"
                    };
                });

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    result = result.Where(r =>
                        (r.name.ToLower().Contains(searchLower)) ||
                        (r.nis.ToLower().Contains(searchLower)) ||
                        (r.classroom_name.ToLower().Contains(searchLower)) ||
                        (r.company_name.ToLower().Contains(searchLower))
                    );
                }

                var resultList = result.ToList();
                var totalCount = resultList.Count;
                List<object> pagedResult;
                if (pageSize < 0 || pageSize >= totalCount)
                {
                    pagedResult = resultList.Cast<object>().ToList();
                    pageSize = totalCount;
                    page = 1;
                }
                else pagedResult = resultList.Skip((page - 1) * pageSize).Take(pageSize).Cast<object>().ToList();

                return Ok(new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    data = pagedResult
                });
            }

            // Admin/Operator: tampilkan semua student (data perhari)
            else
            {
                var filterDate = date ?? DateOnly.FromDateTime(DateTime.Now);

                var studentsQuery = _db.Students
                    .Include(s => s.User)
                    .Include(s => s.Classroom)
                    .Include(s => s.Company)
                    .Where(s => s.StudentValidation.isPKL);

                if (classId.HasValue)
                    studentsQuery = studentsQuery.Where(s => s.Classroomid == classId.Value);

                var students = await studentsQuery.ToListAsync();
                var studentIds = students.Select(s => s.id).ToList();

                var reportsQuery = _db.Reports
                    .Include(r => r.ReportFile)
                    .Include(r => r.ReportPhoto)
                    .Include(p => p.ReportFeedback)
                    .Where(r => studentIds.Contains(r.Studentid) && r.date == filterDate);

                var reportsOnDate = await reportsQuery.ToListAsync();

                var weekStart = GetStartOfWeek(filterDate);
                var weekEnd = weekStart.AddDays(6);
                var weeklyGuidances = await _db.WeeklyGuidances
                    .Where(wg => studentIds.Contains(wg.Studentid) && wg.WeekStartDate == weekStart)
                    .ToListAsync();

                var result = students.Select(s =>
                {
                    var report = reportsOnDate.FirstOrDefault(r => r.Studentid == s.id);
                    var hasGuidance = weeklyGuidances.Any(wg => wg.Studentid == s.id);
                    return new
                    {
                        id = report?.id.ToString() ?? "-",
                        studentId = report?.Studentid.ToString() ?? "-",
                        nis = s.nis ?? "-",
                        name = s?.User?.fullname ?? "-",
                        classroom_name = s?.Classroom?.name ?? "-",
                        company_name = s?.Company?.name ?? "-",
                        date = ToIndonesianLongDate(filterDate),
                        time = report != null ? report.time.ToString("HH:mm:ss") : "-",
                        description = report?.description ?? "-",
                        feedback = report != null && report?.ReportFeedback != null
                            ? new
                            {
                                kajur = !string.IsNullOrWhiteSpace(report.ReportFeedback.kajur) ? report.ReportFeedback.kajur : "-",
                                mentor = !string.IsNullOrWhiteSpace(report.ReportFeedback.mentor) ? report.ReportFeedback.mentor : "-",
                                walas = !string.IsNullOrWhiteSpace(report.ReportFeedback.walas) ? report.ReportFeedback.walas : "-"
                            }
                            : new
                            {
                                kajur = "-",
                                mentor = "-",
                                walas = "-"
                            },
                        reportFileId = report?.ReportFileid != null ? report.ReportFileid.ToString() : "-",
                        reportPhotoId = report?.ReportPhotoid != null ? report.ReportPhotoid.ToString() : "-",
                        isGuidance = hasGuidance ? "✔️" : "❌"
                    };
                });

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    result = result.Where(r =>
                        (r.name.ToLower().Contains(searchLower)) ||
                        (r.nis.ToLower().Contains(searchLower)) ||
                        (r.classroom_name.ToLower().Contains(searchLower)) ||
                        (r.company_name.ToLower().Contains(searchLower))
                    );
                }

                var resultList = result.ToList();
                var totalCount = resultList.Count;
                List<object> pagedResult;
                if (pageSize < 0 || pageSize >= totalCount)
                {
                    pagedResult = resultList.Cast<object>().ToList();
                    pageSize = totalCount;
                    page = 1;
                }
                else pagedResult = resultList.Skip((page - 1) * pageSize).Take(pageSize).Cast<object>().ToList();

                return Ok(new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    data = pagedResult
                });
            }
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
        public async Task<IActionResult> PrintReportByStudent(
            int studentId,
            [FromQuery] DateOnly date)
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
                .Include(s => s.Mentor).ThenInclude(m => m.User)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.id == studentId);

            if (student == null)
                return NotFound("Student not found.");

            // Ambil report pada tanggal tersebut
            var report = await _db.Reports
                .Include(r => r.ReportPhoto)
                .Include(r => r.ReportFeedback)
                .FirstOrDefaultAsync(r => r.Studentid == studentId && r.date == date);

            var pdfBytes = PrintHelper.GenerateStudentReportPdf(student, report, date);
            var fileName = $"Bimbingan Laporan_{student.nis}_{date:yyyyMMdd}.pdf";

            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return File(pdfBytes, "application/pdf", fileName);
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

            if (!classId.HasValue)
                return BadRequest("ClassId is required.");

            var classroom = await _db.Classrooms
                .Include(c => c.Students)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(c => c.id == classId.Value);

            if (classroom == null)
                return NotFound("Classroom not found.");

            DateTime GetMonday(DateOnly date)
            {
                var dt = date.ToDateTime(TimeOnly.MinValue);
                int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
                return dt.AddDays(-diff).Date;
            }

            if (!startDate.HasValue || !endDate.HasValue)
                return BadRequest("startDate dan endDate wajib diisi.");

            var mondayStart = GetMonday(startDate.Value);
            var mondayEnd = GetMonday(endDate.Value);

            // Ambil semua report dalam rentang minggu yang terlibat
            var allReports = await _db.Reports
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.Student).ThenInclude(s => s.Company)
                .Include(r => r.Student).ThenInclude(s => s.Mentor)
                .Include(r => r.ReportFeedback)
                .Where(r => r.Classroomid == classroom.id && r.date >= DateOnly.FromDateTime(mondayStart) && r.date <= DateOnly.FromDateTime(mondayEnd.AddDays(6)))
                .ToListAsync();

            // Kumpulkan data per minggu
            var weeklyData = new List<(DateOnly weekStart, DateOnly weekEnd, List<(Student student, List<Report> reports)>)>();
            for (var week = mondayStart; week <= mondayEnd; week = week.AddDays(7))
            {
                var weekStart = DateOnly.FromDateTime(week);
                var weekEnd = DateOnly.FromDateTime(week.AddDays(6));

                // Ambil report di minggu ini
                var reportsInWeek = allReports
                    .Where(r => r.date >= weekStart && r.date <= weekEnd)
                    .ToList();

                // Untuk setiap siswa, ambil semua report di minggu ini (bisa kosong)
                var studentRows = classroom.Students
                    .OrderBy(s => s?.User?.fullname)
                    .Select(s => (
                        student: s,
                        reports: reportsInWeek
                            .Where(r => r.Studentid == s.id)
                            .OrderBy(r => r.date)
                            .ThenBy(r => r.time)
                            .ToList()
                    )).ToList();

                weeklyData.Add((weekStart, weekEnd, studentRows));
            }

            var pdfBytes = PrintHelper.GenerateClassReportPdf(classroom, weeklyData);
            var fileName = $"ClassReport_{classroom.name}_{DateTime.Now:yyyyMMdd}.pdf";

            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return File(pdfBytes, "application/pdf", fileName);
        }

        [Authorize]
        [HttpGet("mentor/{mentorId?}/print")]
        public async Task<IActionResult> PrintReportByMentor(
            int? mentorId,
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

            if (!mentorId.HasValue)
                return BadRequest("MentorId is required");

            var mentor = await _db.Mentors
                .Include(m => m.User)
                .Include(m => m.Students)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(m => m.id == mentorId.Value);

            if (mentor == null)
                return NotFound("Mentor not found.");

            DateTime GetMonday(DateOnly date)
            {
                var dt = date.ToDateTime(TimeOnly.MinValue);
                int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
                return dt.AddDays(-diff).Date;
            }

            if (!startDate.HasValue || !endDate.HasValue)
                return BadRequest("startDate dan endDate wajib diisi.");

            var mondayStart = GetMonday(startDate.Value);
            var mondayEnd = GetMonday(endDate.Value);

            // Ambil semua report dalam rentang minggu yang terlibat
            var allReports = await _db.Reports
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.ReportFeedback)
                .Where(r => r.Mentorid == mentor.id && r.date >= DateOnly.FromDateTime(mondayStart) && r.date <= DateOnly.FromDateTime(mondayEnd.AddDays(6)))
                .ToListAsync();

            var weeklyData = new List<(DateOnly weekStart, DateOnly weekEnd, List<(Student student, List<Report> reports)>)>();
            for (var week = mondayStart; week <= mondayEnd; week = week.AddDays(7))
            {
                var weekStart = DateOnly.FromDateTime(week);
                var weekEnd = DateOnly.FromDateTime(week.AddDays(6));

                // Ambil report di minggu ini
                var reportsInWeek = allReports
                    .Where(r => r.date >= weekStart && r.date <= weekEnd)
                    .ToList();

                // Untuk setiap siswa, ambil semua report di minggu ini (bisa kosong)
                var studentRows = mentor.Students
                    .OrderBy(s => s?.User?.fullname)
                    .Select(s => (
                        student: s,
                        reports: reportsInWeek
                            .Where(r => r.Studentid == s.id)
                            .OrderBy(r => r.date)
                            .ThenBy(r => r.time)
                            .ToList()
                    )).ToList();

                weeklyData.Add((weekStart, weekEnd, studentRows));
            }

            var pdfBytes = PrintHelper.GenerateMentorReportPdf(mentor, weeklyData);
            var fileName = $"MentorReport_{mentor.User?.fullname ?? mentor.id.ToString()}_{DateTime.Now:yyyyMMdd}.pdf";

            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return File(pdfBytes, "application/pdf", fileName);
        }

        [Authorize]
        [HttpGet("combined/{userId}/print")]
        public async Task<IActionResult> PrintReportByMentorAndWaliKelas(
            int userId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate)
        {
            var userRoles = await _db.UserRoles.Where(ur => ur.Userid == userId).Select(ur => ur.RoleId).ToListAsync();
            if (!(userRoles.Contains(3) && userRoles.Contains(5)))
                return StatusCode(403, "Hanya user dengan role Mentor & Wali Kelas yang dapat mengakses.");

            if (!startDate.HasValue || !endDate.HasValue)
                return BadRequest("startDate dan endDate wajib diisi.");

            // Ambil mentor dan wali kelas
            var mentor = await _db.Mentors.Include(m => m.User).FirstOrDefaultAsync(m => m.Userid == userId);
            var waliKelas = await _db.WaliKelas.Include(wk => wk.User).FirstOrDefaultAsync(wk => wk.Userid == userId);

            // Ambil kelas yang diampu wali kelas
            var classroom = waliKelas != null
                ? await _db.Classrooms.Include(c => c.Students).ThenInclude(s => s.User)
                    .FirstOrDefaultAsync(c => c.WaliKelasid == waliKelas.id)
                : null;

            // Ambil semua siswa yang dimentori
            var mentorStudents = mentor != null
                ? await _db.Students.Include(s => s.User)
                    .Where(s => s.Mentorid == mentor.id)
                    .ToListAsync()
                : new List<Student>();

            // Gabungkan semua siswa (kelas wali + mentor), hilangkan duplikat
            var allStudents = new List<Student>();
            if (classroom != null)
                allStudents.AddRange(classroom.Students);
            allStudents.AddRange(mentorStudents);
            allStudents = allStudents
                .GroupBy(s => s.id)
                .Select(g => g.First())
                .OrderBy(s => s?.User?.fullname)
                .ToList();

            // Ambil semua report siswa dalam rentang minggu
            DateTime GetMonday(DateOnly date)
            {
                var dt = date.ToDateTime(TimeOnly.MinValue);
                int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
                return dt.AddDays(-diff).Date;
            }
            var mondayStart = GetMonday(startDate.Value);
            var mondayEnd = GetMonday(endDate.Value);

            var allStudentIds = allStudents.Select(s => s.id).ToList();
            var allReports = await _db.Reports
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.ReportFeedback)
                .Where(r => allStudentIds.Contains(r.Studentid) && r.date >= DateOnly.FromDateTime(mondayStart) && r.date <= DateOnly.FromDateTime(mondayEnd.AddDays(6)))
                .ToListAsync();

            // Kumpulkan data per minggu
            var weeklyData = new List<(DateOnly weekStart, DateOnly weekEnd, List<(Student student, List<Report> reports)>)>();
            for (var week = mondayStart; week <= mondayEnd; week = week.AddDays(7))
            {
                var weekStart = DateOnly.FromDateTime(week);
                var weekEnd = DateOnly.FromDateTime(week.AddDays(6));

                var reportsInWeek = allReports
                    .Where(r => r.date >= weekStart && r.date <= weekEnd)
                    .ToList();

                var studentRows = allStudents
                    .OrderBy(s => s?.User?.fullname)
                    .Select(s => (
                        student: s,
                        reports: reportsInWeek
                            .Where(r => r.Studentid == s.id)
                            .OrderBy(r => r.date)
                            .ThenBy(r => r.time)
                            .ToList()
                    )).ToList();

                weeklyData.Add((weekStart, weekEnd, studentRows));
            }

            // Nama mentor untuk file
            var mentorName = mentor?.User?.fullname ?? "MentorWaliKelas";
            var fileName = $"RekapBimbinganLaporan_{mentorName}_{DateTime.Now:yyyyMMdd}.pdf";

            var pdfBytes = PrintHelper.GenerateMentorWaliKelasReportPdf(mentorName, weeklyData);
            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return File(pdfBytes, "application/pdf", fileName);
        }

        [Authorize]
        [HttpGet("history-reports")]
        public async Task<IActionResult> GetHistoryReports(
            [FromQuery] int studentId,
            [FromQuery] int page = 1)
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

            // Hanya roleId 2 & 6 yang tidak boleh akses
            if (roleId == 2 || roleId == 6)
                return StatusCode(403, "You are not allowed to access this resource.");

            if (studentId <= 0)
                return BadRequest("studentId is required.");

            // Ambil data student
            var student = await _db.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.id == studentId);

            if (student == null)
                return NotFound("Student not found.");

            // Ambil semua report milik student
            var query = _db.Reports
                .Include(r => r.ReportFile)
                .Include(r => r.ReportPhoto)
                .Where(r => r.Studentid == studentId);

            // Pagination
            const int pageSize = 4;
            if (page < 1) page = 1;

            var totalCount = await query.CountAsync();
            var reports = await query
                .OrderByDescending(r => r.date)
                .ThenByDescending(r => r.time)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    id = r.id.ToString(),
                    nis = student.nis,
                    name = student.User.fullname,
                    date = ToIndonesianLongDate(r.date),
                    time = r.time.ToString("HH:mm:ss"),
                    description = r.description,
                    reportFileId = r.ReportFileid,
                    reportPhotoId = r.ReportPhotoid,
                    feedback = r.ReportFeedback != null
                        ? new
                        {
                            kajur = !string.IsNullOrWhiteSpace(r.ReportFeedback.kajur) ? r.ReportFeedback.kajur : "-",
                            mentor = !string.IsNullOrWhiteSpace(r.ReportFeedback.mentor) ? r.ReportFeedback.mentor : "-",
                            walas = !string.IsNullOrWhiteSpace(r.ReportFeedback.walas) ? r.ReportFeedback.walas : "-"
                        }
                        : new
                        {
                            kajur = "-",
                            mentor = "-",
                            walas = "-"
                        }
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                data = reports
            });
        }
    }
}