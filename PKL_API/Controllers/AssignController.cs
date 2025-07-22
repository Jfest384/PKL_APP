using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PKL_API.Helpers;
using PKL_API.Models.DTO;

namespace PKL_API.Controllers
{
    [Route("api/assign")]
    [ApiController]
    public class AssignController : ControllerBase
    {
        private readonly PklContext _db;
        public AssignController(PklContext db)
        {
            _db = db;
        }

        [Authorize]
        [HttpPut("{studentId}")]
        public async Task<IActionResult> EditStudentData(int studentId, [FromBody] EditStudentDTO dto)
        {
            var user = await AuthHelper.GetCurrentUser(HttpContext, _db);
            if (user == null)
                return Unauthorized("User not found.");

            var userRole = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.Userid == user.id);
            if (userRole == null)
                return StatusCode(403, "User role not found.");

            var roleId = userRole.RoleId;
            if (roleId != 1 && roleId != 4)
                return StatusCode(403, "You do not have permission to edit this data.");

            var student = await _db.Students.FindAsync(studentId);
            if (student == null)
                return NotFound("Student not found.");

            // Ubah hanya jika dto.idClass dikirim
            if (dto.idClass.HasValue)
            {
                var classroom = await _db.Classrooms.FindAsync(dto.idClass.Value);
                if (classroom == null)
                    return BadRequest($"Classroom with id {dto.idClass} does not exist.");

                student.Classroomid = dto.idClass.Value;
            }

            // Ubah hanya jika dto.isPKL dikirim
            if (dto.isPKL.HasValue)
            {
                student.isPKL = dto.isPKL.Value;

                // Jika isPKL = false, kosongkan mentor & company
                if (!dto.isPKL.Value)
                {
                    student.Mentorid = null;
                    student.Companyid = null;
                }
            }

            _db.Students.Update(student);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Student data updated successfully." });
        }

        [Authorize]
        [HttpPut("company/{studentId}")]
        public async Task<IActionResult> EditStudentData(int studentId, int companyId)
        {
            var user = await AuthHelper.GetCurrentUser(HttpContext, _db);
            if (user == null)
            {
                return StatusCode(403, "User not found.");
            }

            // Get role id from user via UserRoles table
            var userRole = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.Userid == user.id);
            if (userRole == null)
            {
                return StatusCode(403, "User role not found.");
            }
            var roleId = userRole.RoleId;
            if (roleId != 1 && roleId != 4 && roleId != 5)
            {
                return StatusCode(403, "You do not have permission to edit this data.");
            }

            var student = await _db.Students.FindAsync(studentId);
            if (student == null)
            {
                return NotFound("Student not found.");
            }

            if (student.isPKL == false)
            {
                return BadRequest("Student is not currently assigned to PKL.");
            }

            var company = await _db.Companies.FindAsync(companyId);
            if (company == null)
            {
                return NotFound("Mentor not found.");
            }

            student.Companyid = companyId;
            _db.Students.Update(student);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Student mentor updated successfully." });
        }

        [Authorize]
        [HttpPut("mentor/{mentorId}")]
        public async Task<IActionResult> AssignMentorToStudents(int mentorId, [FromBody] List<int> studentIds)
        {
            var user = await AuthHelper.GetCurrentUser(HttpContext, _db);
            if (user == null)
            {
                return StatusCode(403, "User not found.");
            }

            // Get role id from user via UserRoles table
            var userRole = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.Userid == user.id);
            if (userRole == null)
            {
                return StatusCode(403, "User role not found.");
            }
            var roleId = userRole.RoleId;
            if (roleId != 1 && roleId != 4)
            {
                return StatusCode(403, "You do not have permission to edit this data.");
            }

            var mentor = await _db.Mentors.FindAsync(mentorId);
            if (mentor == null)
            {
                return NotFound("Mentor not found.");
            }

            var students = await _db.Students
                .Where(s => studentIds.Contains(s.id))
                .ToListAsync();

            if (students.Count != studentIds.Count)
            {
                var notFoundIds = studentIds.Except(students.Select(s => s.id)).ToList();
                return NotFound($"Student(s) not found: {string.Join(", ", notFoundIds)}");
            }

            // Check PKL status and assign mentor
            foreach (var student in students)
            {
                if (student.isPKL == false)
                {
                    return BadRequest($"Student with id {student.id} is not currently assigned to PKL.");
                }
                student.Mentorid = mentorId;
            }

            _db.Students.UpdateRange(students);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Mentor assigned to selected students successfully." });
        }

        //[Authorize]
        //[HttpPut("company/{companyId}")]
        //public async Task<IActionResult> AssignCompanyToStudents(int companyId, [FromBody] List<int> studentIds)
        //{
        //    var user = await AuthHelper.GetCurrentUser(HttpContext, _db);
        //    if (user == null)
        //    {
        //        return StatusCode(403, "User not found.");
        //    }

        //    // Get role id from user via UserRoles table
        //    var userRole = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.Userid == user.id);
        //    if (userRole == null)
        //    {
        //        return StatusCode(403, "User role not found.");
        //    }
        //    var roleId = userRole.RoleId;
        //    if (roleId != 1 && roleId != 4)
        //    {
        //        return StatusCode(403, "You do not have permission to edit this data.");
        //    }

        //    var company = await _db.Companies.FindAsync(companyId);
        //    if (company == null)
        //    {
        //        return NotFound("Company not found.");
        //    }

        //    var students = await _db.Students
        //        .Where(s => studentIds.Contains(s.id))
        //        .ToListAsync();

        //    if (students.Count != studentIds.Count)
        //    {
        //        var notFoundIds = studentIds.Except(students.Select(s => s.id)).ToList();
        //        return NotFound($"Student(s) not found: {string.Join(", ", notFoundIds)}");
        //    }

        //    // Check PKL status and assign company
        //    foreach (var student in students)
        //    {
        //        if (!student.isPKL)
        //        {
        //            return BadRequest($"Student with id {student.id} is not currently assigned to PKL.");
        //        }
        //        student.Companyid = companyId;
        //    }

        //    _db.Students.UpdateRange(students);
        //    await _db.SaveChangesAsync();

        //    return Ok(new { message = "Company assigned to selected students successfully." });
        //}
    }
}
