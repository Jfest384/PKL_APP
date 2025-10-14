namespace PKL_API.Models.DTO
{
    public class AssignDTO
    {
        public bool status { get; set; }
        //    [Column("id_mentor")]
        //    public int Mentorid { get; set; }
        //    [Column("id_company")]
        //    public int Companyid { get; set; }
        //    [Column("id_student")]
        //    public int Studentid { get; set; }
    }

    public class EditStudentLockDTO
    {
        public int studentId { get; set; }
        public int status { get; set; } // 0 = unlock, 1 = lock
    }
}
