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
    [Route("api/students")]
    [ApiController]
    public class StudentController : ControllerBase
    {
        private const string V = "{studentId}";
        private readonly PklContext _db;
        public StudentController(PklContext db)
        {
            _db = db;
        }

        [Authorize]
        [HttpPost("add")]
        public async Task<IActionResult> AddStudent([FromBody] StudentDTO dto)
        {
            // Validasi input
            if (dto == null || string.IsNullOrWhiteSpace(dto.nis) || string.IsNullOrWhiteSpace(dto.name) || dto.Classid <= 0)
                return BadRequest("Invalid request body.");

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

            // 1. Tambahkan data ke tabel Users
            var newUser = new User
            {
                username = dto.nis,
                password = "9b8769a4a742959a2d0298c36fb70623f2dfacda8436237df08d8dfd5b37374c",
                fullname = dto.name,
                gender = true,
                UserRoles = new List<UserRole>()
            };

            _db.Users.Add(newUser);
            await _db.SaveChangesAsync();

            // 2. Tambahkan UserRole baru dengan id_role 2 (Student)
            var studentRole = await _db.Roles.FirstOrDefaultAsync(r => r.id == 2);
            if (studentRole == null)
                return BadRequest("Role Student (id=2) tidak ditemukan.");

            var newUserRole = new UserRole
            {
                Userid = newUser.id,
                RoleId = 2
            };
            _db.UserRoles.Add(newUserRole);
            await _db.SaveChangesAsync();

            // 3. Ambil id terakhir dari tabel Users
            var newUserId = newUser.id;

            // 4. Tambahkan data ke tabel Students
            var newStudent = new Student
            {
                Userid = newUserId,
                nis = dto.nis,
                Classroomid = dto.Classid,
                Departmentid = 1,
                isPKL = false
            };

            _db.Students.Add(newStudent);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Student added successfully.",
                studentId = newStudent.id
            });
        }

        [HttpGet]
        public IActionResult GetStudents([FromQuery] int? id_class, [FromQuery] string? name, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.Students.AsQueryable();

            // Apply filters BEFORE pagination so search is global
            if (id_class.HasValue)
            {
                query = query.Where(q => q.Classroomid == id_class.Value);
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                var lowered = name.ToLower();
                query = query.Where(q =>
                    q.User.fullname.ToLower().Contains(lowered) ||
                    q.nis.ToLower().Contains(lowered)
                );
            }

            var totalItems = query.Count();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var studentsList = query
                .OrderBy(q => q.id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    q.id,
                    q.Userid,
                    q.nis,
                    q.User.fullname,
                    q.User.gender,
                    q.Classroomid,
                    class_name = q.Classroom != null ? q.Classroom.name : null,
                    q.isPKL,
                    q.isLock
                })
                .ToList();

            var result = new
            {
                page,
                pageSize,
                totalItems,
                totalPages,
                students = studentsList
            };

            return Ok(result);
        }

        [HttpGet(V)]
        public IActionResult GetStudentById(int studentId)
        {
            var student = _db.Students
                .Where(q => q.id == studentId)
                .Select(q => new
                {
                    q.id,
                    q.Userid,
                    q.User.fullname,
                    q.User.gender,
                    q.nis,
                    q.nisn,
                    class_name = q.Classroom != null ? q.Classroom.name : null,
                    mentor_name = q.Mentor != null ? q.Mentor.User.fullname : null,
                    company_name = q.Company != null ? q.Company.name : null,
                    isPKL = q.isPKL == true ? "Yes" : "No",
                    isLock = q.isLock == true ? "Yes" : "No"
                })
                .FirstOrDefault();
            if (student == null)
            {
                return NotFound("Student not found");
            }
            return Ok(student);
        }

        [Authorize]
        [HttpGet($"{V}/photo")]
        public async Task<IActionResult> GetStudentPhoto(int studentId)
        {
            // Step 1: Get the student by id
            var student = await _db.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.id == studentId);

            if (student == null)
                return NotFound("Student not found.");

            // Step 2: Get the user by Userid
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.id == student.Userid);

            if (user == null)
                return NotFound("User not found.");

            // Step 3: Get the photo by Photoid
            if (user.Photoid == null)
                return Ok(null);

            var photo = await _db.Photos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.id == user.Photoid.Value);

            if (photo == null || photo.photo == null)
                return NotFound("Photo not found.");

            var contentType = photo.extension.Contains("png") ? "image/png" : "image/jpeg";
            return File(photo.photo, $"{contentType}");
        }

        [Authorize]
        [HttpGet("pkl")]
        public async Task<IActionResult> GetStudentsPKL(
            [FromQuery] int? id_class,
            [FromQuery] string? name,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? noMentor = null,
            [FromQuery] int? userIdFilter = null
        )
        {
            // Step 1: Get user ID from token
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null)
                return Unauthorized("User ID not found in token.");

            if (!int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("Invalid user ID in token.");

            // Step 2: Get user and role
            var user = await _db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.id == userId);
            if (user == null)
                return Unauthorized("User not found.");

            var userRole = user.UserRoles.FirstOrDefault();
            if (userRole == null)
                return Unauthorized("User role not found.");

            int roleId = userRole.RoleId;

            // Step 3: Base query for PKL students
            var query = _db.Students
                .Where(q => q.isPKL == true)
                .AsQueryable();

            // Step 4: Filter for student role
            if (roleId == 2)
            {
                var student = await _db.Students.FirstOrDefaultAsync(s => s.Userid == userId);
                if (student == null)
                    return NotFound("Student data not found.");

                query = query.Where(q => q.Classroomid == student.Classroomid);
            }
            else if (id_class.HasValue)
            {
                query = query.Where(q => q.Classroomid == id_class.Value);
            }

            // Step 4.1: Filter students with no mentor if requested
            if (noMentor == true)
            {
                query = query.Where(q => q.Mentorid == null);
            }

            // Step 4.2: Filter by userId if provided
            if (userIdFilter.HasValue)
            {
                query = query.Where(q => q.Userid == userIdFilter.Value);
            }

            // Step 5: Filter by name or nis if provided
            if (!string.IsNullOrWhiteSpace(name))
            {
                var lowered = name.ToLower();
                query = query.Where(q =>
                    q.User.fullname.ToLower().Contains(lowered) ||
                    q.nis.ToLower().Contains(lowered)
                );
            }

            // Step 6: Pagination
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var studentList = await query
                .OrderBy(q => q.id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    q.id,
                    q.Userid,
                    q.nis,
                    q.User.fullname,
                    q.User.email,
                    q.Classroomid,
                    class_name = q.Classroom != null ? q.Classroom.name : "-",
                    mentor_name = q.Mentor != null ? q.Mentor.User.fullname : "-",
                    company_name = q.Company != null ? q.Company.name : "-",
                    isLock = q.isLock == true ? "Yes" : "No"
                })
                .ToListAsync();

            var result = new
            {
                page,
                pageSize,
                totalItems,
                totalPages,
                students = studentList
            };

            return Ok(result);
        }

        [Authorize]
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteStudent([FromBody] List<int> studentIds)
        {
            if (studentIds == null || studentIds.Count == 0)
                return BadRequest("studentIds tidak boleh kosong.");

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

            // Ambil semua student yang akan dihapus
            var studentsToDelete = await _db.Students
                .Where(s => studentIds.Contains(s.id))
                .ToListAsync();

            if (studentsToDelete.Count == 0)
                return NotFound("Tidak ada data siswa yang ditemukan.");

            // Ambil semua userId dari student yang akan dihapus
            var userIdsToDelete = studentsToDelete.Select(s => s.Userid).Distinct().ToList();

            // 1. Hapus foto user (Photos)
            var photoIdsToDelete = await _db.Users
                .Where(u => userIdsToDelete.Contains(u.id) && u.Photoid != null)
                .Select(u => u.Photoid.Value)
                .ToListAsync();

            var photosToDelete = await _db.Photos
                .Where(p => photoIdsToDelete.Contains(p.id))
                .ToListAsync();
            _db.Photos.RemoveRange(photosToDelete);

            // 2. Hapus data Presences
            var presencesToDelete = await _db.Presences
                .Where(p => studentIds.Contains(p.Studentid))
                .ToListAsync();
            _db.Presences.RemoveRange(presencesToDelete);

            // 3. Hapus PresenceFeedbacks
            var presenceFeedbackIds = presencesToDelete
                .Where(p => p.PresenceFeedbackid.HasValue)
                .Select(p => p.PresenceFeedbackid.Value)
                .Distinct()
                .ToList();
            var presenceFeedbacksToDelete = await _db.PresenceFeedbacks
                .Where(fb => presenceFeedbackIds.Contains(fb.id))
                .ToListAsync();
            _db.PresenceFeedbacks.RemoveRange(presenceFeedbacksToDelete);

            // 4. Hapus PresenceDetails dan PresencePhotos
            var presenceDetailIds = presencesToDelete
                .Select(p => p.PresenceDetailid)
                .Distinct()
                .ToList();
            var presenceDetailsToDelete = await _db.PresenceDetails
                .Where(pd => presenceDetailIds.Contains(pd.id))
                .ToListAsync();

            // Ambil semua photoId dari PresenceDetails
            var presencePhotoIds = new List<Guid>();
            foreach (var detail in presenceDetailsToDelete)
            {
                if (detail.FullBodyPhotoid.HasValue) presencePhotoIds.Add(detail.FullBodyPhotoid.Value);
                if (detail.MedicalCertificatePhotoid.HasValue) presencePhotoIds.Add(detail.MedicalCertificatePhotoid.Value);
                if (detail.TreatmentPhotoid.HasValue) presencePhotoIds.Add(detail.TreatmentPhotoid.Value);
                if (detail.SickToCompanyPhotoid.HasValue) presencePhotoIds.Add(detail.SickToCompanyPhotoid.Value);
                if (detail.SickToMentorPhotoid.HasValue) presencePhotoIds.Add(detail.SickToMentorPhotoid.Value);
                if (detail.SickToWalasPhotoid.HasValue) presencePhotoIds.Add(detail.SickToWalasPhotoid.Value);
                if (detail.PermitToCompanyPhotoid.HasValue) presencePhotoIds.Add(detail.PermitToCompanyPhotoid.Value);
                if (detail.PermitToMentorPhotoid.HasValue) presencePhotoIds.Add(detail.PermitToMentorPhotoid.Value);
                if (detail.PermitToWalasPhotoid.HasValue) presencePhotoIds.Add(detail.PermitToWalasPhotoid.Value);
                if (detail.ActivityPhotoid.HasValue) presencePhotoIds.Add(detail.ActivityPhotoid.Value);
                if (detail.HolidayFromCompanyPhotoid.HasValue) presencePhotoIds.Add(detail.HolidayFromCompanyPhotoid.Value);
                if (detail.WFHFromCompanyPhotoid.HasValue) presencePhotoIds.Add(detail.WFHFromCompanyPhotoid.Value);
            }
            var presencePhotosToDelete = await _db.PresencePhotos
                .Where(pp => presencePhotoIds.Contains(pp.id))
                .ToListAsync();
            _db.PresencePhotos.RemoveRange(presencePhotosToDelete);
            _db.PresenceDetails.RemoveRange(presenceDetailsToDelete);

            // 5. Hapus LockLocations
            var lockLocationsToDelete = await _db.LockLocations
                .Where(l => studentIds.Contains(l.Studentid))
                .ToListAsync();
            _db.LockLocations.RemoveRange(lockLocationsToDelete);

            // 6. Hapus Reports
            var reportsToDelete = await _db.Reports
                .Where(r => studentIds.Contains(r.Studentid))
                .ToListAsync();
            _db.Reports.RemoveRange(reportsToDelete);

            // 7. Hapus ReportFeedbacks
            var reportFeedbackIds = reportsToDelete
                .Where(r => r.ReportFeedbackid.HasValue)
                .Select(r => r.ReportFeedbackid.Value)
                .Distinct()
                .ToList();
            var reportFeedbacksToDelete = await _db.ReportFeedbacks
                .Where(fb => reportFeedbackIds.Contains(fb.id))
                .ToListAsync();
            _db.ReportFeedbacks.RemoveRange(reportFeedbacksToDelete);

            // 8. Hapus ReportFiles (fileId dan photoId)
            var reportFileIds = reportsToDelete
                .Where(r => r.ReportFileid.HasValue)
                .Select(r => r.ReportFileid.Value)
                .Distinct()
                .ToList();
            var reportPhotoIds = reportsToDelete
                .Where(r => r.ReportPhotoid.HasValue)
                .Select(r => r.ReportPhotoid.Value)
                .Distinct()
                .ToList();

            var reportFilesToDelete = await _db.ReportFiles
                .Where(f => reportFileIds.Contains(f.id) || reportPhotoIds.Contains(f.id))
                .ToListAsync();
            _db.ReportFiles.RemoveRange(reportFilesToDelete);

            var weeklyGuidancesToDelete = await _db.WeeklyGuidances
                .Where(l => studentIds.Contains(l.Studentid))
                .ToListAsync();
            _db.WeeklyGuidances.RemoveRange(weeklyGuidancesToDelete);

            // Hapus data di tabel UserRoles
            var userRolesToDelete = await _db.UserRoles
                .Where(ur => userIdsToDelete.Contains(ur.Userid))
                .ToListAsync();
            _db.UserRoles.RemoveRange(userRolesToDelete);

            // Hapus data di tabel Users
            var usersToDelete = await _db.Users
                .Where(u => userIdsToDelete.Contains(u.id))
                .ToListAsync();
            _db.Users.RemoveRange(usersToDelete);

            // Hapus data di tabel Students
            _db.Students.RemoveRange(studentsToDelete);

            await _db.SaveChangesAsync();
            return Ok(new { message = "Data siswa berhasil dihapus." });
        }

        [HttpGet($"detail/{V}/print")]
        public async Task<IActionResult> PrintStudentsDetail(int studentId)
        {
            var student = await _db.Students
                .Include(s => s.Classroom)
                .Include(s => s.Company)
                .Include(s => s.Mentor)
                .FirstOrDefaultAsync(s => s.id == studentId);

            if (student == null)
            {
                return NotFound("Student not found");
            }

            // Generate PDF using QuestPDF
            var pdfBytes = GenerateStudentDetailPdf(student);

            return File(pdfBytes, "application/pdf", "Student Detail.pdf");
        }

        private static byte[] GenerateStudentDetailPdf(Student student)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text("Student Detail")
                        .FontSize(24)
                        .Bold()
                        .AlignCenter();

                    page.Content().PaddingTop(25).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(150); // Key column
                            columns.RelativeColumn();    // Value column
                        });

                        void AddRow(string key, string? value)
                        {
                            table.Cell().Element(CellStyle).Text(key).Bold();
                            table.Cell().Element(CellStyle).Text(value ?? "-");
                        }

                        // Add all fields to display
                        AddRow("ID Student", student.id.ToString());
                        AddRow("Name", student.User.fullname);
                        AddRow("NIS", student.nis);
                        AddRow("NISN", student.nisn);
                        AddRow("Class", student.Classroom?.name);
                        AddRow("Company", student.Company?.name);
                        AddRow("Mentor", student.Mentor?.User.fullname);

                        // Style for each cell
                        IContainer CellStyle(IContainer container) =>
                            container
                                .Border(1)
                                .BorderColor(Colors.Black)
                                .PaddingVertical(6)
                                .PaddingHorizontal(6);
                    });
                });
            }).GeneratePdf();
        }

        [HttpGet("{classId}/print")]
        public async Task<IActionResult> PrintStudentsByClass(int classId)
        {
            var students = await _db.Students
                .Include(s => s.Classroom)
                .Where(s => s.Classroomid == classId)
                .ToListAsync();

            if (students == null || students.Count == 0)
            {
                return NotFound("No students found for this class.");
            }

            var pdfBytes = GenerateAllStudentPdf(students);

            return File(pdfBytes, "application/pdf", "Students by Class.pdf");
        }

        private static byte[] GenerateAllStudentPdf(List<Student> students)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text("Student List")
                        .FontSize(24)
                        .Bold()
                        .AlignCenter();

                    page.Content().PaddingTop(25).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(100);   // ID Student
                            columns.RelativeColumn();      // Name
                            columns.RelativeColumn();      // Class
                        });

                        // Header row
                        table.Cell().Element(CellStyle).Text("ID Student").Bold();
                        table.Cell().Element(CellStyle).Text("Name").Bold();
                        table.Cell().Element(CellStyle).Text("Class").Bold();

                        foreach (var student in students)
                        {
                            table.Cell().Element(CellStyle).Text(student.id.ToString());
                            table.Cell().Element(CellStyle).Text(student.User.fullname ?? "-");
                            table.Cell().Element(CellStyle).Text(student.Classroom?.name ?? "-");
                        }

                        IContainer CellStyle(IContainer container) =>
                            container
                                .Border(1)
                                .BorderColor(Colors.Black)
                                .PaddingVertical(6)
                                .PaddingHorizontal(6);
                    });
                });
            }).GeneratePdf();
        }
    }
}
