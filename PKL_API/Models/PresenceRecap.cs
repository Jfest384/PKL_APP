using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class PresenceRecap
    {
        public int id { get; set; }

        [Column("id_student")]
        public int StudentId { get; set; }

        public string nis { get; set; }
        public int pkl_days { get; set; }
        public int presence_total { get; set; }
        public int absen_total { get; set; }
        public int send_total { get; set; }
        public int not_send_total { get; set; }
        public int hadir { get; set; }
        public int sakit { get; set; }
        public int izin { get; set; }
        public int libur { get; set; }
        public int wfh { get; set; }
        public DateOnly update_at { get; set; }
    }
}
