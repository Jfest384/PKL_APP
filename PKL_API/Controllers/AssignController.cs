using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PKL_API.Helpers;
using PKL_API.Models.DTO;

namespace PKL_API.Controllers
{
    [Route("assign")]
    [ApiController]
    public class AssignController : ControllerBase
    {
        private readonly PklContext _db;
        public AssignController(PklContext db)
        {
            _db = db;
        }

        [Authorize]
        [HttpPut("batch")]
        public async Task<IActionResult> EditStudentsBatch([FromBody] List<EditStudentBatchDTO> dtos)
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

            if (dtos == null || dtos.Count == 0)
                return BadRequest("No student data provided.");

            var studentIds = dtos.Select(d => d.studentId).ToList();
            var students = await _db.Students
                .Include(s => s.StudentValidation)
                .Where(s => studentIds.Contains(s.id)).ToListAsync();

            var notFoundIds = studentIds.Except(students.Select(s => s.id)).ToList();
            if (notFoundIds.Count > 0)
                return NotFound($"Student(s) not found: {string.Join(", ", notFoundIds)}");

            foreach (var dto in dtos)
            {
                var student = students.FirstOrDefault(s => s.id == dto.studentId);
                if (student == null) continue;

                // Ubah hanya jika idClass dikirim
                if (dto.idClass.HasValue)
                {
                    var classroom = await _db.Classrooms.FindAsync(dto.idClass.Value);
                    if (classroom == null)
                        return BadRequest($"Classroom with id {dto.idClass} does not exist.");
                    student.Classroomid = dto.idClass.Value;
                }

                // Ubah hanya jika isPKL dikirim
                if (dto.isPKL.HasValue)
                {
                    student.StudentValidation.isPKL = dto.isPKL.Value;
                    if (!dto.isPKL.Value)
                    {
                        student.Mentorid = null;
                        student.Companyid = null;
                        student.CompanyLocationid = null;
                    }
                }
            }

            _db.Students.UpdateRange(students);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Student data updated successfully." });
        }

        [Authorize]
        [HttpPut("company-location/{studentId}")]
        public async Task<IActionResult> EditStudentData(int studentId, int companyLocationId)
        {
            var user = await AuthHelper.GetCurrentUser(HttpContext, _db);
            if (user == null)
                return StatusCode(403, "User not found.");

            // Get role id from user via UserRoles table
            var userRole = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.Userid == user.id);
            if (userRole == null)
                return StatusCode(403, "User role not found.");

            var roleId = userRole.RoleId;
            if (roleId != 1 && roleId != 4 && roleId != 5)
                return StatusCode(403, "You do not have permission to edit this data.");

            var student = await _db.Students
                .Include(s => s.StudentValidation)
                .FirstOrDefaultAsync(s => s.id == studentId);
            if (student == null)
                return NotFound("Student not found.");

            if (student.StudentValidation?.isPKL == false)
                return BadRequest("Student is not currently assigned to PKL.");

            var companyLocation = await _db.CompanyLocations.FindAsync(companyLocationId);
            if (companyLocation == null)
                return NotFound("Company Location not found.");

            student.CompanyLocationid = companyLocationId;
            student.Companyid = companyLocation.Companyid;
            _db.Students.Update(student);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Student company location updated successfully." });
        }

        [Authorize]
        [HttpPut("mentor/{mentorId}")]
        public async Task<IActionResult> AssignMentorToStudents(int mentorId, [FromBody] List<int> studentIds)
        {
            var user = await AuthHelper.GetCurrentUser(HttpContext, _db);
            if (user == null)
                return StatusCode(403, "User not found.");

            // Get role id from user via UserRoles table
            var userRole = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.Userid == user.id);
            if (userRole == null)
                return StatusCode(403, "User role not found.");

            var roleId = userRole.RoleId;
            if (roleId != 1 && roleId != 4)
                return StatusCode(403, "You do not have permission to edit this data.");

            var mentor = await _db.Mentors.FindAsync(mentorId);
            if (mentor == null)
                return NotFound("Mentor not found.");

            var students = await _db.Students
                .Include(s => s.StudentValidation)
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
                if (student.StudentValidation?.isPKL == false)
                    return BadRequest($"Student with id {student.id} is not currently assigned to PKL.");
                student.Mentorid = mentorId;
            }

            _db.Students.UpdateRange(students);

            // 1. Update mentorId in Presences
            var presencesToUpdate = await _db.Presences
                .Where(p => studentIds.Contains(p.Studentid))
                .ToListAsync();
            foreach (var presence in presencesToUpdate)
            {
                presence.Mentorid = mentorId;
            }
            _db.Presences.UpdateRange(presencesToUpdate);

            // 2. Update mentorId in Reports
            var reportsToUpdate = await _db.Reports
                .Where(r => studentIds.Contains(r.Studentid))
                .ToListAsync();
            foreach (var report in reportsToUpdate)
            {
                report.Mentorid = mentorId;
            }
            _db.Reports.UpdateRange(reportsToUpdate);

            // 3. Update mentorId in WeeklyGuidances
            var guidancesToUpdate = await _db.WeeklyGuidances
                .Where(wg => studentIds.Contains(wg.Studentid))
                .ToListAsync();
            foreach (var guidance in guidancesToUpdate)
            {
                guidance.Mentorid = mentorId;
            }
            _db.WeeklyGuidances.UpdateRange(guidancesToUpdate);

            await _db.SaveChangesAsync();
            return Ok(new { message = "Mentor assigned to selected students successfully." });
        }

        [Authorize]
        [HttpGet("status-lock-location")]
        public async Task<ActionResult<bool>> GetStatusLockLocation()
        {
            var statusEntity = await _db.StatusLockLocations.FirstOrDefaultAsync();
            if (statusEntity == null)
                return NotFound("StatusLockLocation not found.");

            return Ok(statusEntity.status);
        }

        [Authorize]
        [HttpPut("status-lock-location")]
        public async Task<IActionResult> EditStatusLockLocation([FromBody] int status)
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

            if (status != 0 && status != 1)
                return BadRequest("Status value must be 0 or 1.");

            var statusEntity = await _db.StatusLockLocations.FirstOrDefaultAsync();
            if (statusEntity == null)
                return NotFound("StatusLockLocation not found.");

            statusEntity.status = status == 1;

            // Set updateAt to WIB (UTC+7)
            var wibTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var wibNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, wibTimeZone);
            statusEntity.updateAt = wibNow;

            // Update semua StudentValidations: set isLock dan update_daily
            var allValidations = await _db.StudentValidations.ToListAsync();
            foreach (var v in allValidations)
            {
                v.isLock = statusEntity.status;
                v.update_daily = wibNow;
            }
            _db.StudentValidations.UpdateRange(allValidations);

            _db.StatusLockLocations.Update(statusEntity);
            await _db.SaveChangesAsync();

            var message = statusEntity.status
                ? "Fitur Lock Location telah diaktifkan."
                : "Fitur Lock Location telah dinonaktifkan.";

            return Ok(new { message, status = statusEntity.status ? 1 : 0, updateAt = statusEntity.updateAt });
        }

        [Authorize]
        [HttpPut("student-lock")]
        public async Task<IActionResult> EditStudentLock([FromBody] EditStudentLockDTO dto)
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

            if (dto.status != 0 && dto.status != 1)
                return BadRequest("Status value must be 0 or 1.");

            var student = await _db.Students
                .Include(s => s.StudentValidation)
                .FirstOrDefaultAsync(s => s.id == dto.studentId);
            if (student == null)
                return NotFound("Student not found.");

            student.StudentValidation.isLock = dto.status == 1;
            student.StudentValidation.update_daily = DateTime.Now;

            _db.Students.Update(student);
            await _db.SaveChangesAsync();

            var message = student.StudentValidation.isLock == true
                ? "Lock Location berhasil diaktifkan."
                : "Lock Location berhasil dinonaktifkan.";

            return Ok(new { message, isLock = student.StudentValidation.isLock ? 1 : 0, update_at = student.StudentValidation.update_daily });
        }
    }
}
