using Microsoft.EntityFrameworkCore;
using PKL_API.Models;

namespace PKL_API
{
    public class PklContext : DbContext
    {
        public PklContext(DbContextOptions opt) : base(opt) { }
        public DbSet<User> Users { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<PresenceType> PresenceTypes { get; set; }
        public DbSet<Presence> Presences { get; set; }
        public DbSet<Mentor> Mentors { get; set; }
        //public DbSet<Department> Departments { get; set; }
        public DbSet<Classroom> Classrooms { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Photo> Photos { get; set; }
        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<PresenceDetail> PresenceDetails { get; set; }
        public DbSet<PresencePhoto> PresencePhotos { get; set; }
        public DbSet<WaliKelas> WaliKelas { get; set; }
        public DbSet<WeeklyGuidance> WeeklyGuidances { get; set; }
        public DbSet<ReportFile> ReportFiles { get; set; }
        public DbSet<LockLocation> LockLocations { get; set; }
        public DbSet<PresenceFeedback> PresenceFeedbacks { get; set; }
        public DbSet<ReportFeedback> ReportFeedbacks { get; set; }
        public DbSet<StatusLockLocation> StatusLockLocations { get; set; }
        public DbSet<StudentValidation> StudentValidations { get; set; }
        public DbSet<ChatService> ChatServices { get; set; }
        public DbSet<DefaultChat> DefaultChats { get; set; }
        public DbSet<ChatContact> ChatContacts { get; set; }
        public DbSet<PresenceRecap> PresenceRecaps { get; set; }
        public DbSet<ReportRecap> ReportRecaps { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<CompanyLocation> CompanyLocations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PresenceDetail>(entity =>
            {
                entity.HasOne(d => d.FullBodyPhoto)
                      .WithMany()
                      .HasForeignKey(d => d.FullBodyPhotoid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.MedicalCertificatePhoto)
                      .WithMany()
                      .HasForeignKey(d => d.MedicalCertificatePhotoid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.TreatmentPhoto)
                      .WithMany()
                      .HasForeignKey(d => d.TreatmentPhotoid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.SickToCompanyPhoto)
                      .WithMany()
                      .HasForeignKey(d => d.SickToCompanyPhotoid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.SickToMentorPhoto)
                      .WithMany()
                      .HasForeignKey(d => d.SickToMentorPhotoid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.SickToWalasPhoto)
                      .WithMany()
                      .HasForeignKey(d => d.SickToWalasPhotoid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.PermitToCompanyPhoto)
                      .WithMany()
                      .HasForeignKey(d => d.PermitToCompanyPhotoid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.PermitToMentorPhoto)
                      .WithMany()
                      .HasForeignKey(d => d.PermitToMentorPhotoid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.PermitToWalasPhoto)
                      .WithMany()
                      .HasForeignKey(d => d.PermitToWalasPhotoid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.ActivityPhoto)
                      .WithMany()
                      .HasForeignKey(d => d.ActivityPhotoid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.HolidayFromCompanyPhoto)
                      .WithMany()
                      .HasForeignKey(d => d.HolidayFromCompanyPhotoid)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.WFHFromCompanyPhoto)
                      .WithMany()
                      .HasForeignKey(d => d.WFHFromCompanyPhotoid)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Report>()
                .HasOne(r => r.ReportFile)
                .WithMany()
                .HasForeignKey(r => r.ReportFileid)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Report>()
                .HasOne(r => r.ReportPhoto)
                .WithMany()
                .HasForeignKey(r => r.ReportPhotoid)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
