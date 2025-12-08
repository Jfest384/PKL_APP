namespace PKLPresenceWeb.Model
{
    public class PresenceRecap
    {
        public string studentName { get; set; } = "";
        public string nis { get; set; } = "";
        public string kelas { get; set; } = "";
        public string mentorName { get; set; } = "";
        public string walasName { get; set; } = "";
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
    }

    public class ReportRecap
    {
        public string studentName { get; set; } = "";
        public string nis { get; set; } = "";
        public string kelas { get; set; } = "";
        public string mentorName { get; set; } = "";
        public string walasName { get; set; } = "";
        public int total_weeks { get; set; }
        public int report_total { get; set; }
    }

    public class RecapReportPhotos
    {
        public string photoId { get; set; }
        public string extension { get; set; }
        public string url { get; set; }
        public string date { get; set; }
    }
}