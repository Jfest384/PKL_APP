using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class ReportRecap
    {
        public int id { get; set; }

        [Column("id_student")]
        public int StudentId { get; set; }
        public string nis { get; set; }
        public int total_weeks { get; set; }
        public int report_total { get; set; }
        public DateOnly update_at { get; set; }
    }
}
