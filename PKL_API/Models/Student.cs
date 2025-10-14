using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class Student
    {
        [Key]
        public int id { get; set; }

        [Column("id_user")]
        public int Userid { get; set; }

        [StringLength(50)]
        public required string nis { get; set; }

        [StringLength(50)]
        public string? nisn { get; set; }

        [Column("id_class")]
        public int? Classroomid { get; set; }

        [ForeignKey(nameof(Classroomid))]
        public Classroom? Classroom { get; set; }

        [Column("id_department")]
        public int Departmentid { get; set; }

        [ForeignKey(nameof(Departmentid))]
        public Department? Department { get; set; }

        [Column("id_mentor")]
        public int? Mentorid { get; set; }

        [ForeignKey(nameof(Mentorid))]
        public Mentor? Mentor { get; set; }

        [Column("id_company")]
        public int? Companyid { get; set; }

        [ForeignKey(nameof(Companyid))]
        public Company? Company { get; set; }

        public bool? isPKL { get; set; }

        [ForeignKey(nameof(Userid))]
        public User? User { get; set; }

        public bool isLock { get; set; }
        public DateTime? update_at { get; set; }


        public ICollection<Presence>? Presences { get; set; }
        public ICollection<Report>? Reports { get; set; }
    }
}
