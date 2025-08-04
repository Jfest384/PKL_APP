using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class PresenceDetail
    {
        [Key]
        public int id { get; set; }

        [ForeignKey(nameof(FullBodyPhoto))]
        [Column("full_body")]
        public Guid? FullBodyPhotoid { get; set; }
        public PresencePhoto? FullBodyPhoto { get; set; }

        [Column(TypeName = "decimal(10,7)")]
        public decimal? lat { get; set; }
        [Column("long", TypeName = "decimal(10,7)")]
        public decimal? longitude { get; set; }

        [MaxLength]
        public string? daily_report { get; set; }

        [ForeignKey(nameof(MedicalCertificatePhoto))]
        [Column("medical_certificate")]
        public Guid? MedicalCertificatePhotoid { get; set; }
        public PresencePhoto? MedicalCertificatePhoto { get; set; }


        [ForeignKey(nameof(TreatmentPhoto))]
        [Column("treatment")]
        public Guid? TreatmentPhotoid { get; set; }
        public PresencePhoto? TreatmentPhoto { get; set; }


        [ForeignKey(nameof(SickToCompanyPhoto))]
        [Column("sick_to_company")]
        public Guid? SickToCompanyPhotoid { get; set; }
        public PresencePhoto? SickToCompanyPhoto { get; set; }


        [ForeignKey(nameof(SickToMentorPhoto))]
        [Column("sick_to_mentor")]
        public Guid? SickToMentorPhotoid { get; set; }
        public PresencePhoto? SickToMentorPhoto { get; set; }


        [ForeignKey(nameof(SickToWalasPhoto))]
        [Column("sick_to_walas")]
        public Guid? SickToWalasPhotoid { get; set; }
        public PresencePhoto? SickToWalasPhoto { get; set; }


        [ForeignKey(nameof(PermitToCompanyPhoto))]
        [Column("permit_to_company")]
        public Guid? PermitToCompanyPhotoid { get; set; }
        public PresencePhoto? PermitToCompanyPhoto { get; set; }


        [ForeignKey(nameof(PermitToMentorPhoto))]
        [Column("permit_to_mentor")]
        public Guid? PermitToMentorPhotoid { get; set; }
        public PresencePhoto? PermitToMentorPhoto { get; set; }


        [ForeignKey(nameof(PermitToWalasPhoto))]
        [Column("permit_to_walas")]
        public Guid? PermitToWalasPhotoid { get; set; }
        public PresencePhoto? PermitToWalasPhoto { get; set; }


        [ForeignKey(nameof(ActivityPhoto))]
        [Column("activity")]
        public Guid? ActivityPhotoid { get; set; }
        public PresencePhoto? ActivityPhoto { get; set; }


        [ForeignKey(nameof(HolidayFromCompanyPhoto))]
        [Column("holiday_from_company")]
        public Guid? HolidayFromCompanyPhotoid { get; set; }
        public PresencePhoto? HolidayFromCompanyPhoto { get; set; }



    }
}
