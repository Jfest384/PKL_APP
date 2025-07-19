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
        public required string nisn { get; set; }

        [Column("id_class")]
        public int? Classroomid { get; set; }

        [ForeignKey(nameof(Classroomid))]
        public required Classroom Classroom { get; set; }

        [Column("id_department")]
        public int Departmentid { get; set; }

        //[ForeignKey(nameof(Departmentid))]
        //public required Department Department { get; set; }

        [Column("id_mentor")]
        public int? Mentorid { get; set; }

        [ForeignKey(nameof(Mentorid))]
        public required Mentor Mentor { get; set; }

        [Column("id_company")]
        public int? Companyid { get; set; }

        [ForeignKey(nameof(Companyid))]
        public required Company Company { get; set; }

        public bool isPKL { get; set; }

        [ForeignKey(nameof(Userid))]
        public required User User { get; set; }


        public required ICollection<Presence> Presences { get; set; }
        public required ICollection<Report> Reports { get; set; }
    }
}
