using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class Report
    {
        [Key]
        public int id { get; set; }

        [Column("id_student")]
        public int Studentid { get; set; }

        [Column("id_mentor")]
        public int? Mentorid { get; set; }

        [Column("id_class")]
        public int? Classroomid { get; set; }

        public DateOnly date { get; set; }
        public TimeOnly time { get; set; }

        [DataType(DataType.MultilineText)]
        public required string description { get; set; }

        [Column("id_feedback")]
        public int? ReportFeedbackid { get; set; }

        [Column("id_file")]
        public Guid? ReportFileid { get; set; }

        [Column("id_photo")]
        public Guid? ReportPhotoid { get; set; }

        public Student Student { get; set; }
        public Mentor Mentor { get; set; }
        public Classroom Classroom { get; set; }
        public ReportFile? ReportFile { get; set; }
        public ReportFile? ReportPhoto { get; set; }
        public ReportFeedback? ReportFeedback { get; set; }
    }
}
