using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class User
    {
        public static object? Claims { get; internal set; }

        [Key]
        public int id { get; set; }
        [StringLength(100)]
        public required string username { get; set; }
        [StringLength(100)]
        public required string password { get; set; }
        [StringLength(100)]
        public required string fullname { get; set; }
        [StringLength(100)]
        public string? email { get; set; }
        [Column("id_photo")]
        public Guid? Photoid { get; set; }
        public bool gender { get; set; }

        public required Photo Photo { get; set; }

        public required ICollection<Student> Students { get; set; }
        public required ICollection<Mentor> Mentors { get; set; }
        public required ICollection<WaliKelas> WaliKelas { get; set; }
        public required ICollection<UserRole> UserRoles { get; set; }
        public required ICollection<Teacher> Teachers { get; set; }
    }
}
