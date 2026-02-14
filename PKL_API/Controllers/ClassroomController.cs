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
    [Route("classes")]
    [ApiController]
    public class ClassroomController : ControllerBase
    {
        private readonly PklContext _db;
        public ClassroomController(PklContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult GetClassroom(
            [FromQuery] string? name,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.Classrooms
                .Include(q => q.Teachers)
                .ThenInclude(wk => wk.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                var lowered = name.ToLower();

                // Parse values outside the expression tree
                bool parsedTotal = int.TryParse(name, out var total);
                bool parsedYear = int.TryParse(name, out var yearVal);

                query = query.Where(q =>
                    q.name.ToLower().Contains(lowered)
                    || (parsedTotal && q.total_students == total)
                    || (q.Teachers != null && q.Teachers.User.fullname.ToLower().Contains(lowered))
                    || (parsedYear && q.year == yearVal)
                );
            }

            var totalItems = query.Count();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var classroomsList = query
                .OrderBy(q => q.id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    q.id,
                    q.name,
                    students = q.total_students,
                    id_walas = q.Teacherid,
                    walas = q.Teachers != null ? q.Teachers.User.fullname : null,
                    q.year,
                    q.description,
                    q.ChatContactid
                })
                .ToList();

            var result = new
            {
                page,
                pageSize,
                totalItems,
                totalPages,
                classrooms = classroomsList
            };

            return Ok(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Class(ClassroomDTO dto)
        {
            // Get user id from claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null)
                return Unauthorized("User ID not found in token.");

            int userId = int.Parse(userIdClaim.Value);

            // Get user role
            var userRole = await _db.Roles
                .Where(r => _db.UserRoles.Any(ur => ur.User.id == userId && ur.Role.id == r.id))
                .Select(r => r.id)
                .FirstOrDefaultAsync();

            if (userRole != 1 && userRole != 4)
                return StatusCode(403, "Only admin and kepala jurusan can create a class.");

            // Validate required fields
            if (string.IsNullOrWhiteSpace(dto.name))
                return BadRequest("Class name is required.");

            var waliKelas = await _db.Teachers.FindAsync(dto.WaliKelasid);
            if (waliKelas == null)
                return BadRequest("Wali Kelas not found.");

            var classroom = new Classroom
            {
                name = dto.name,
                Teacherid = waliKelas.id,
                year = dto.year,
                description = dto.description,
                ChatContactid = dto.contactId
            };

            _db.Classrooms.Add(classroom);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Classroom created successfully", classroom.id });
        }

        [Authorize]
        [HttpPut("{classId}")]
        public async Task<IActionResult> EditClassroom(int classId, [FromBody] ClassroomEditDTO dto)
        {
            // Validasi classId
            if (classId <= 0)
                return BadRequest("classId is required and must be greater than 0.");

            // Ambil user id dari claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null)
                return Unauthorized("User ID not found in token.");

            int userId = int.Parse(userIdClaim.Value);

            // Ambil role user
            var userRole = await _db.Roles
                .Where(r => _db.UserRoles.Any(ur => ur.User.id == userId && ur.Role.id == r.id))
                .Select(r => r.id)
                .FirstOrDefaultAsync();

            if (userRole != 1 && userRole != 4)
                return StatusCode(403, "Only admin and kepala jurusan can edit a class.");

            // Cari classroom
            var classroom = await _db.Classrooms.FindAsync(classId);
            if (classroom == null)
                return NotFound("Classroom not found.");

            // Validasi input
            if (string.IsNullOrWhiteSpace(dto.name))
                return BadRequest("Class name is required.");

            var waliKelas = await _db.Teachers.FindAsync(dto.WaliKelasid);
            if (waliKelas == null)
                return BadRequest("Wali Kelas not found.");

            // Update data classroom
            classroom.name = dto.name;
            classroom.Teacherid = dto.WaliKelasid;
            classroom.year = dto.year;
            classroom.description = dto.description;
            classroom.ChatContactid = dto.contactId;

            await _db.SaveChangesAsync();

            return Ok(new { message = "Classroom updated successfully." });
        }

        [Authorize]
        [HttpDelete("{classId}")]
        public async Task<IActionResult> DeleteClassroom(int classId)
        {
            // Validate classId
            if (classId <= 0)
                return BadRequest("classId is required and must be greater than 0.");

            // Get user id from claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null)
                return Unauthorized("User ID not found in token.");

            int userId = int.Parse(userIdClaim.Value);

            // Get user role
            var userRole = await _db.Roles
                .Where(r => _db.UserRoles.Any(ur => ur.User.id == userId && ur.Role.id == r.id))
                .Select(r => r.id)
                .FirstOrDefaultAsync();

            if (userRole != 1 && userRole != 4)
                return StatusCode(403, "Only admin and kepala jurusan can delete a class.");

            // Find classroom
            var classroom = await _db.Classrooms.FindAsync(classId);
            if (classroom == null)
                return NotFound("Classroom not found.");

            // Set id_class to null in Presence, Report, and Student
            var presences = _db.Presences.Where(p => p.Classroomid == classId);
            await presences.ForEachAsync(p => p.Classroomid = null);

            var reports = _db.Reports.Where(r => r.Classroomid == classId);
            await reports.ForEachAsync(r => r.Classroomid = null);

            var students = _db.Students.Where(s => s.Classroomid == classId);
            await students.ForEachAsync(s => s.Classroomid = null);

            // Remove classroom
            _db.Classrooms.Remove(classroom);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Classroom deleted successfully." });
        }

        [HttpGet("{classroomId}")]
        public IActionResult GetClassroomById(int classroomId)
        {
            var classroom = _db.Classrooms
                .Where(q => q.id == classroomId)
                .Select(q => new
                {
                    q.id,
                    q.name,
                    walas = q.Teachers != null ? q.Teachers.User.fullname : null,
                    q.total_students,
                    q.description,
                    q.ChatContactid,
                    students = q.Students.Select(s => new
                    {
                        s.id,
                        s.User.fullname,
                        s.nis,
                        s.nisn
                    }).ToList()
                })
                .FirstOrDefault();
            if (classroom == null)
            {
                return NotFound("Classroom not found");
            }
            return Ok(classroom);
        }

        [HttpGet("print")]
        public async Task<IActionResult> PrintAllClass()
        {
            var classrooms = await _db.Classrooms
                .Include(s => s.Teachers)
                .ToListAsync();

            if (classrooms == null || classrooms.Count == 0)
            {
                return NotFound("No teachers found");
            }

            // Generate PDF using QuestPDF
            var pdfBytes = GenerateAllClassPdf(classrooms);

            return File(pdfBytes, "application/pdf", "All Classes.pdf");
        }

        private static byte[] GenerateAllClassPdf(List<Classroom> classrooms)
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
                        .Text("Class List")
                        .FontSize(24)
                        .Bold()
                        .AlignCenter();

                    page.Content().PaddingTop(25).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();   // ID
                            columns.RelativeColumn();     // Name
                        });

                        // Header row
                        table.Cell().Element(CellStyle).Text("Wali Kelas").Bold();
                        table.Cell().Element(CellStyle).Text("Class Name").Bold();

                        //int no = 1;
                        foreach (var classroom in classrooms)
                        {
                            table.Cell().Element(CellStyle).Text(classroom.Teachers?.User.fullname ?? "-");
                            table.Cell().Element(CellStyle).Text(classroom.name ?? "-");
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
