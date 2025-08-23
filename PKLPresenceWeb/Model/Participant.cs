using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKLPresenceWeb.Model
{
    public class StudentListResponse
    {
        public int page { get; set; }
        public int pageSize { get; set; }
        public int totalItems { get; set; }
        public int totalPages { get; set; }
        public List<StudentItem> students { get; set; } = new();
    }

    public class StudentItem
    {
        public int id { get; set; }
        public int userid { get; set; }
        public string nis { get; set; }
        public string fullname { get; set; }
        public bool gender { get; set; }
        public int classroomid { get; set; }
        public string class_name { get; set; }
        public bool isPKL { get; set; }
    }

    public class MentorListResponse
    {
        public int page { get; set; }
        public int pageSize { get; set; }
        public int totalItems { get; set; }
        public int totalPages { get; set; }
        public List<MentorItem> data { get; set; } = new();
    }

    public class MentorItem
    {
        public int id { get; set; }
        public int userid { get; set; }
        public int teacherid { get; set; }
        public string fullname { get; set; }
        public List<string> classes { get; set; } = new();
    }

    public class StudentPKLListResponse
    {
        public int page { get; set; }
        public int pageSize { get; set; }
        public int totalItems { get; set; }
        public int totalPages { get; set; }
        public List<StudentPKLItem> students { get; set; } = new();
    }

    public class StudentPKLItem
    {
        public int id { get; set; }
        public int userid { get; set; }
        public string nis { get; set; }
        public string fullname { get; set; }
        public string email { get; set; }
        public int classroomid { get; set; }
        public string class_name { get; set; }
        public string mentor_name { get; set; }
        public string company_name { get; set; }
    }

    public class ClassListResponse
    {
        public int page { get; set; }
        public int pageSize { get; set; }
        public int totalItems { get; set; }
        public int totalPages { get; set; }
        public List<ClassItem> classrooms { get; set; } = new();
    }

    public class ClassItem
    {
        public int id { get; set; }
        public string name { get; set; }
        public int students { get; set; }
        public int id_walas { get; set; }
        public string walas { get; set; }
        public int year { get; set; }
        public string description { get; set; }
    }

    public class NewClass
    {
        [StringLength(50)]
        public required string name { get; set; }
        public required int total_students { get; set; }
        [Column("id_walas")]
        public required int WaliKelasid { get; set; }
        public required int year { get; set; }
        [StringLength(200)]
        public required string description { get; set; }
    }

    public class UserResponse
    {
        public int id { get; set; }
        public string username { get; set; }
        public string fullname { get; set; }
        public string role { get; set; }
        public string email { get; set; }
        private bool gender { get; set; }
    }

    public class WalasItem
    {
        public int id { get; set; }
        public string fullname { get; set; }
        public int userid { get; set; }
        public int teacherid { get; set; }
    }

    public class CompanyItem
    {
        public int id { get; set; }
        public string name { get; set; }
        public string address { get; set; }
        public string phone { get; set; }
    }

    public class TeacherItem
    {
        public int id { get; set; }
        public int userid { get; set; }
        public string fullname { get; set; }
        public string nip { get; set; }
        public string roles { get; set; }
    }

    public class MentorDTO
    {
        public int id_user { get; set; }
        public int id_teacher { get; set; }
    }

    public class EditStudentBatchDTO
    {
        public int studentId { get; set; }
        public bool? isPKL { get; set; }
        public int? idClass { get; set; }
    }
}
