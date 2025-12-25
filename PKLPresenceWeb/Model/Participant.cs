using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Text.Json.Serialization;

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
        public bool isLock { get; set; }
    }

    public class StudentDetail
    {
        public int id { get; set; }
        public int userid { get; set; }
        public string fullname { get; set; }
        public bool gender { get; set; }
        public string nis { get; set; }
        public string class_name { get; set; }
        public string? mentor_name { get; set; }
        public string? company_name { get; set; }
        public string isPKL { get; set; }
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
        public string nip { get; set; }
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
        public string isLock { get; set; }
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

    public class NewStudent
    {
        [StringLength(50)]
        public required string nis { get; set; }
        public required string name { get; set; }
        [Column("id_class")]
        public required int Classid { get; set; }
    }

    public class UserResponse
    {
        public int id { get; set; }
        public string username { get; set; }
        public string fullname { get; set; }
        public string role { get; set; }
        public string email { get; set; }
        public bool gender { get; set; }
        public UserItem data { get; set; }
    }

    public class UserItem
    {
        public int id { get; set; }
        public int mentorId { get; set; }
        public int classId { get; set; }
        public string fullname { get; set; }
        public string nip { get; set; }
        public string nis { get; set; }
        public string nisn { get; set; }
        public string classroom { get; set; }
        public string mentor { get; set; }
        public string company { get; set; }
        public bool isPKL { get; set; }

    }

    public class WalasItem
    {
        public int id { get; set; }
        public string fullname { get; set; }
        public int userid { get; set; }
        public int teacherid { get; set; }
    }

    public class CompanyListResponse
    {
        public int page { get; set; }
        public int pageSize { get; set; }
        public int totalItems { get; set; }
        public int totalPages { get; set; }
        public List<CompanyItem> companies { get; set; } = new();
    }

    public class CompanyItem
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class CompanyLocationListResponse
    {
        public int page { get; set; }
        public int pageSize { get; set; }
        public int totalItems { get; set; }
        public int totalPages { get; set; }
        public List<CompanyLocationItem> CompanyLocationItems { get; set; } = new();
    }

    public class CompanyLocationItem
    {
        public int id { get; set; }
        public string LocationName { get; set; }
    }

    public class CompanyDetailResponse
    {
        public CompanyInfo company { get; set; }
        public List<CompanyLocationInfo> locations { get; set; }
    }

    public class CompanyInfo
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class CompanyLocationInfo
    {
        public int id { get; set; }
        public int companyid { get; set; }
        public string locationName { get; set; }
        public string address { get; set; }
        public double lat { get; set; }
        public double longitude { get; set; }
        public int radius_meter { get; set; }
        public bool is_active { get; set; }
    }

    public class CompanyModel
    {
        public string Name { get; set; } = "";
        public string? Address { get; set; } = "";
        public string? Lat { get; set; }
        public string? Long { get; set; }
    }

    public class CompanyLocationModel
    {
        public int Companyid { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string Lat { get; set; }
        public string Long { get; set; }
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

    public class WahaSession
    {
        public string name { get; set; }
        public string status { get; set; }
        public int? config { get; set; }
        public MeInfo me { get; set; }
        public EngineInfo engine { get; set; }
    }

    public class MeInfo
    {
        public string id { get; set; }
        public string pushName { get; set; }
    }

    public class EngineInfo
    {
        public string engine { get; set; }
        public string WWebVersion { get; set; }
        public string state { get; set; }
    }

    public class DefaultChatItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("serviceId")]
        public int ServiceId { get; set; }

        [JsonPropertyName("service_name")]
        public string ServiceName { get; set; } = string.Empty;

        [JsonPropertyName("contactId")]
        public List<int> ContactId { get; set; } = new();

        [JsonPropertyName("id_chat")]
        public List<string> IdChat { get; set; } = new();

        [JsonPropertyName("chat_name")]
        public List<string> ChatName { get; set; } = new();
    }

    public class DefaultChatDetail
    {
        [JsonPropertyName("contactId")]
        public int ContactId { get; set; }

        [JsonPropertyName("chat_name")]
        public string ChatName { get; set; } = string.Empty;

        [JsonPropertyName("id_chat")]
        public string IdChat { get; set; } = string.Empty;

        [JsonPropertyName("serviceId")]
        public int ServiceId { get; set; }

        [JsonPropertyName("service_name")]
        public string ServiceName { get; set; } = string.Empty;

        [JsonPropertyName("template")]
        public ChatTemplate Template { get; set; } = new();
    }

    public class ChatTemplate
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("chatServices")]
        public object? ChatServices { get; set; }
    }

    public class ChatServiceItem
    {
        public int id { get; set; }
        public string service_name { get; set; }
    }

    public class ChatContactItem
    {
        public int id { get; set; }
        public string id_chat { get; set; }
        public string chat_name { get; set; }
    }

    public class NominatimResult
    {
        public string display_name { get; set; }
    }

    public class CompanyState
    {
        public int? CompanyId { get; set; }
    }

    public class SwalResult
    {
        public bool isConfirmed { get; set; }
        public bool isDismissed { get; set; }
    }

}
