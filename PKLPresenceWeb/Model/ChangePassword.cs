namespace PKLPresenceWeb.Model
{
    public class ChangePasswordModel
    {
        public string oldPassword { get; set; } = "";
        public string newPassword { get; set; } = "";
        public string confirmPassword { get; set; } = "";
    }

    public class ChangePasswordDTO
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }
}
