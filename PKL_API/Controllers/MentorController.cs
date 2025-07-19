using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PKL_API.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PKL_API.Controllers
{
    [Route("api/mentors")]
    [ApiController]
    public class MentorController : ControllerBase
    {
        private readonly PklContext _db;
        public MentorController(PklContext db)
        {
            _db = db;
        }

        //[Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMentors([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _db.Mentors
                .Select(mentor => new
                {
                    mentor.id,
                    mentor.Userid,
                    mentor.Teacherid,
                    mentor.User.fullname,
                    classes = _db.Students
                        .Where(s => s.Mentorid == mentor.id && s.Classroomid != null)
                        .Select(s => s.Classroom.name)
                        .Distinct()
                        .ToList()
                });

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            var mentorsList = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                totalItems,
                totalPages,
                data = mentorsList
            });
        }



        [Authorize]
        [HttpGet("students")]
        public IActionResult GetStudentsByCurrentMentor()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null)
                return Unauthorized("User ID not found in token.");

            int userId = int.Parse(userIdClaim.Value);

            // Cari mentor berdasarkan id user
            var mentor = _db.Mentors.FirstOrDefault(m => m.Userid == userId);
            if (mentor == null)
                return NotFound("Mentor not found for current user.");

            var students = _db.Students
                .Where(s => s.Mentorid == mentor.id)
                .Select(s => new
                {
                    s.id,
                    s.Userid,
                    s.nis,
                    s.nisn,
                    s.User.fullname,
                    class_name = s.Classroom != null ? s.Classroom.name : null,
                    mentor_name = s.Mentor != null ? s.Mentor.User.fullname : null,
                    company_name = s.Company != null ? s.Company.name : null
                })
                .ToList();

            return Ok(students);
        }

        [Authorize]
        [HttpGet("students/{studentId}/photo")]
        public async Task<IActionResult> GetStudentPhoto(int studentId)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null)
                return Unauthorized("User ID not found in token.");

            int userId = int.Parse(userIdClaim.Value);

            var mentor = await _db.Mentors.FirstOrDefaultAsync(m => m.Userid == userId);
            if (mentor == null)
                return NotFound("Mentor not found for current user.");

            // Find student by id and ensure the student belongs to this mentor
            var student = await _db.Students.FirstOrDefaultAsync(s => s.id == studentId && s.Mentorid == mentor.id);
            if (student == null)
                return NotFound("Student not found or does not belong to this mentor.");

            // Get user associated with the student
            var studentUser = await _db.Users.FirstOrDefaultAsync(u => u.id == student.Userid);
            if (studentUser == null)
                return NotFound("User for student not found.");

            if (studentUser.Photoid == null)
                return NotFound("Photo not found.");

            // Get photo from Photos table
            var photo = await _db.Photos.FirstOrDefaultAsync(p => p.id == studentUser.Photoid);
            if (photo == null || photo.photo == null)
                return NotFound(new { title = "Not Found", status = 404, detail = "Photo not found." });

            // Set content type based on extension
            string contentType = photo.extension != null && photo.extension.ToLower().Contains("png") ? "image/png" : "image/jpeg";

            return File(photo.photo, contentType);
        }

        [Authorize]
        [HttpGet("students/print")]
        public async Task<IActionResult> PrintStudentsPdf()
        {
            // Get user id from JWT claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null)
                return Unauthorized("User ID not found in token.");

            int userId = int.Parse(userIdClaim.Value);

            // Find mentor by user id
            var mentor = await _db.Mentors.FirstOrDefaultAsync(m => m.Userid == userId);
            if (mentor == null)
                return NotFound("Mentor not found for current user.");

            // Get students for this mentor
            var students = await _db.Students
                .Where(s => s.Mentorid == mentor.id)
                .Include(s => s.Classroom)
                .ToListAsync();

            // Generate PDF using QuestPDF
            var pdfBytes = GenerateStudentListPdf(students);

            return File(pdfBytes, "application/pdf", "Student List.pdf");
        }

        private static byte[] GenerateStudentListPdf(List<Student> students)
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
                        // Definisikan kolom
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(100); // ID Student
                            columns.RelativeColumn(3);   // Name
                            columns.RelativeColumn(2);   // Class Name
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("ID Student").Bold();
                            header.Cell().Element(CellStyle).Text("Name").Bold();
                            header.Cell().Element(CellStyle).Text("Class").Bold();
                        });

                        // Isi
                        foreach (var s in students)
                        {
                            table.Cell().Element(CellStyle).Text(s.id.ToString());
                            table.Cell().Element(CellStyle).Text(s.User.fullname);
                            table.Cell().Element(CellStyle).Text(s.Classroom?.name ?? "-");
                        }

                        // Gaya cell (bisa border atau padding)
                        IContainer CellStyle(IContainer container) =>
                            container
                                .Border(1)
                                .BorderColor(Colors.Black)
                                .PaddingVertical(6)
                                .PaddingHorizontal(6);
                    });
                });
            })
            .GeneratePdf();
        }


    }
}
