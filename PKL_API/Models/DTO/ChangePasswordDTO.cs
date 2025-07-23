using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models.DTO
{
    public class ChangePasswordDTO
    {
        [Required]
        public string OldPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare("NewPassword", ErrorMessage = "Confirmation password does not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
