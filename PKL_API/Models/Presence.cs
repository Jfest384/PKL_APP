using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class Presence
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

        [Column("id_presence")]
        public int PresenceTypeid { get; set; }

        [Column("id_feedback")]
        public int? PresenceFeedbackid { get; set; }

        [Column("id_detail")]
        public int PresenceDetailid { get; set; }

        public Student? Student { get; set; }
        [ForeignKey(nameof(PresenceTypeid))]
        public PresenceType? PresenceType { get; set; }
        public PresenceDetail? Detail { get; set; }
        public Mentor? Mentor { get; set; }
        public Classroom? Classroom { get; set; }
        public PresenceFeedback? PresenceFeedback { get; set; }
    }
}
