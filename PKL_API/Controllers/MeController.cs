using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PKL_API.Models;
using PKL_API.Models.DTO;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace PKL_API.Controllers
{
    [Route("api/me")]
    [ApiController]
    public class MeController : ControllerBase
    {
        private readonly PklContext _db;
        public MeController(PklContext db)
        {
            _db = db;
        }

        private string PasswordHasher(string pass)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashByte = sha256.ComputeHash(Encoding.UTF8.GetBytes(pass));
                return BitConverter.ToString(hashByte).ToLower().Replace("-", "");
            }
        }

        [Authorize]
        [HttpGet]
        public IActionResult GetMe()
        {
            var selectedUserId = Convert.ToInt32(User.Claims.FirstOrDefault(q => q.Type == "id")?.Value);
            var selectedUser = _db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.Students)
                .Include(u => u.Teachers)
                .FirstOrDefault(q => q.id == selectedUserId);

            if (selectedUser == null)
            {
                return NotFound("User not found");
            }

            // Ambil role pertama (jika ada)
            var userRole = selectedUser.UserRoles.FirstOrDefault();
            var roleName = userRole?.Role.name ?? "";
            var roleId = userRole?.Role.id ?? 0;

            object? extraData = null;

            if (roleId == 2)
            {
                // Tampilkan semua data student, ganti id_class, id_department, id_mentor, id_company menjadi nama
                var students = _db.Students
                    .Where(s => s.User.id == selectedUserId)
                    .Select(s => new
                    {
                        s.id,
                        s.nis,
                        s.nisn,
                        s.User.fullname,
                        classroom = s.Classroom.name,
                        mentor = s.Mentor.User.fullname,
                        company = s.Company.name,
                        s.isPKL
                    })
                    .ToList();
                extraData = students;
            }
            else if (roleId != 2 && roleId != 1)
            {
                // Tampilkan data teacher
                var teacher = _db.Teachers
                    .Where(t => t.User.id == selectedUserId)
                    .Select(t => new
                    {
                        t.id,
                        t.User.fullname,
                        t.nip
                    })
                    .FirstOrDefault();
                extraData = teacher;
            }

            return Ok(new
            {
                id = selectedUser.id,
                username = selectedUser.username,
                fullname = selectedUser.fullname,
                role = roleName,
                email = string.IsNullOrEmpty(selectedUser.email) ? "-" : selectedUser.email,
                profile = selectedUser.Photoid,
                gender = selectedUser.gender,
                data = extraData
            });
        }

        [Authorize]
        [HttpPut]
        public async Task<IActionResult> UpdateMe(EditProfileDTO editProfileDTO)
        {
            var selectedUserId = Convert.ToInt32(User.Claims.FirstOrDefault(q => q.Type == "id")?.Value);

            // Update fullname di tabel User
            var selectedUser = await _db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(q => q.id == selectedUserId);

            if (selectedUser == null)
            {
                return NotFound("User not found");
            }

            selectedUser.fullname = editProfileDTO.fullname;
            selectedUser.email = editProfileDTO.email;
            selectedUser.gender = editProfileDTO.gender;

            // Update Student jika ada
            var student = await _db.Students.FirstOrDefaultAsync(s => s.User.id == selectedUserId);
            if (student != null)
            {
                if (!string.IsNullOrEmpty(editProfileDTO.nis))
                    student.nis = editProfileDTO.nis;
                if (editProfileDTO.Classroomid > 0)
                    student.Classroomid = editProfileDTO.Classroomid;
                if (editProfileDTO.Companyid > 0)
                    student.Companyid = editProfileDTO.Companyid;
            }

            // Update Teacher jika ada
            var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.User.id == selectedUserId);
            if (teacher != null && !string.IsNullOrEmpty(editProfileDTO.nip))
            {
                teacher.nip = editProfileDTO.nip;
            }

            await _db.SaveChangesAsync();

            var roleName = selectedUser.UserRoles.FirstOrDefault()?.Role.name ?? "";

            return Ok(new
            {
                id = selectedUser.id,
                username = selectedUser.username,
                fullname = selectedUser.fullname,
                role = roleName,
                profile = selectedUser.Photoid,
                email = string.IsNullOrEmpty(selectedUser.email) ? "-" : selectedUser.email,
                gender = selectedUser.gender
            });
        }

        [Authorize]
        [HttpGet("photo")]
        public async Task<IActionResult> GetPhoto()
        {
            var selectedUserId = Convert.ToInt32(User.Claims.FirstOrDefault(q => q.Type == "id")?.Value);
            var selectedUser = await _db.Users.FirstOrDefaultAsync(u => u.id == selectedUserId);

            if (selectedUser == null || selectedUser.Photoid == null)
                return NotFound("Photo not found");

            var photo = await _db.Photos.FirstOrDefaultAsync(p => p.id == selectedUser.Photoid);

            if (photo == null || photo.photo == null)
                return NotFound("Photo not found");

            var contentType = photo.extension.Contains("png") ? "image/png" : "image/jpeg";

            return File(photo.photo, $"{contentType}");
        }


        [Authorize]
        [HttpPut("photo")]
        public async Task<IActionResult> PutPhoto([Required] IFormFile image)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (image == null || image.Length <= 0)
                return BadRequest("No file found");

            var allowedContentTypes = new List<string> { "image/jpeg", "image/png" };
            if (!allowedContentTypes.Contains(image.ContentType.ToLower()))
                return BadRequest("Only allowed .png and .jpg file");

            var userId = int.Parse(User.FindFirst("id")!.Value);
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound("User not found");

            // Set PhotoId ke null dulu agar tidak konflik saat hapus
            Guid? oldPhotoId = user.Photoid;
            user.Photoid = null;
            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            // Hapus foto lama setelah user tidak lagi referensikan
            if (oldPhotoId.HasValue)
            {
                var oldPhoto = await _db.Photos.FindAsync(oldPhotoId.Value);
                if (oldPhoto != null)
                {
                    _db.Photos.Remove(oldPhoto);
                    await _db.SaveChangesAsync();
                }
            }

            // Simpan foto baru
            byte[] photoBytes;
            using (var memStream = new MemoryStream())
            {
                await image.CopyToAsync(memStream);
                photoBytes = memStream.ToArray();
            }
            var fileExtension = Path.GetExtension(image.FileName);

            var newPhoto = new Photo
            {
                photo = photoBytes,
                extension = fileExtension,
                Users = new List<User>()
            };

            _db.Photos.Add(newPhoto);
            await _db.SaveChangesAsync();

            // Update user dengan foto baru
            user.Photoid = newPhoto.id;
            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            return Created("", newPhoto.id);
        }
    }
}
