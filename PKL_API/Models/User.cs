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

        public Photo Photo { get; set; }

        public ICollection<Student> Students { get; set; }
        public ICollection<Mentor> Mentors { get; set; }
        public ICollection<WaliKelas> WaliKelas { get; set; }
        public ICollection<UserRole> UserRoles { get; set; }
        public ICollection<Teacher> Teachers { get; set; }
    }
}
