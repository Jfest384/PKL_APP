using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PKL_API.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
namespace PKL_API.Controllers
{
    [Route("teachers")]
    [ApiController]
    public class TeacherController : ControllerBase
    {
        private readonly PklContext _db;
        public TeacherController(PklContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetTeachers(
            [FromQuery] string? name,
            [FromQuery] string? nip)
        {
            var query = _db.Teachers
                .Include(t => t.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                var lowered = name.ToLower();
                query = query.Where(t => t.User.fullname.ToLower().Contains(lowered));
            }

            if (!string.IsNullOrWhiteSpace(nip))
            {
                var loweredNip = nip.ToLower();
                query = query.Where(t => t.nip.ToLower().Contains(loweredNip));
            }

            var teachersList = await query
                .Select(t => new
                {
                    t.id,
                    t.Userid,
                    t.User.fullname,
                    t.nip,
                    roles = t.User.UserRoles.Select(ur => ur.Role.name).ToList()
                })
                .ToListAsync();

            var result = teachersList.Select(t => new
            {
                t.id,
                t.Userid,
                t.fullname,
                t.nip,
                roles = t.roles.Count switch
                {
                    0 => "-",
                    1 => t.roles[0],
                    _ => string.Join(" & ", t.roles)
                }
            });

            return Ok(result);
        }

        [HttpGet("{teacherId}/print")]
        public async Task<IActionResult> PrintTeachersDetail(int teacherId, string? rolesDisplay)
        {
            var teacher = await _db.Teachers
                .Include(t => t.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(t => t.id == teacherId);

            if (teacher == null)
                return NotFound("Teacher not found");

            // Compose role string from User.UserRoles
            var roleNames = teacher.User.UserRoles?
                .Select(ur => ur.Role?.name)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList() ?? [];

            string? v = roleNames.Count switch
            {
                0 => "-",
                1 => roleNames[0],
                _ => string.Join(" & ", roleNames)
            };
            var pdfBytes = GenerateTeacherDetailPdf(teacher, v);

            return File(pdfBytes, "application/pdf", "Teacher Detail.pdf");
        }

        private static byte[] GenerateTeacherDetailPdf(Teacher teacher, string? rolesDisplay)
        {
            // Since Teacher does not have TeacherRoles, get roles from User.UserRoles
            var roleNames = teacher.User?.UserRoles?
                .Select(ur => ur.Role?.name)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList() ?? [];

            string? v = roleNames.Count switch
            {
                0 => "-",
                1 => roleNames[0],
                _ => string.Join(" & ", roleNames)
            };
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text("Teacher Detail")
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
                        AddRow("ID Teacher", teacher.id.ToString());
                        AddRow("Name", teacher?.User.fullname);
                        AddRow("NIP", teacher?.nip);
                        AddRow("Role", rolesDisplay);

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

        [HttpGet("print")]
        public async Task<IActionResult> PrintAllTeachers()
        {
            var teachers = await _db.Teachers
                .ToListAsync();

            if (teachers == null || teachers.Count == 0)
            {
                return NotFound("No teachers found");
            }

            // Generate PDF using QuestPDF
            var pdfBytes = GenerateAllTeachersPdf(teachers);

            return File(pdfBytes, "application/pdf", "All Teachers.pdf");
        }

        private static byte[] GenerateAllTeachersPdf(List<Teacher> teachers)
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
                        .Text("Teacher List")
                        .FontSize(24)
                        .Bold()
                        .AlignCenter();

                    page.Content().PaddingTop(25).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(80);   // ID
                            columns.RelativeColumn();     // Name
                        });

                        // Header row
                        table.Cell().Element(CellStyle).Text("ID").Bold();
                        table.Cell().Element(CellStyle).Text("Name").Bold();
                        foreach (var teacher in teachers)
                        {
                            table.Cell().Element(CellStyle).Text(teacher.id.ToString());
                            table.Cell().Element(CellStyle).Text(teacher.User.fullname ?? "-");
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

