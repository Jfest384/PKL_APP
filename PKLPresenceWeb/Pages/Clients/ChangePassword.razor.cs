using Microsoft.AspNetCore.Components;
using PKLPresenceWeb.Model;
using System.Net.Http.Json;

namespace PKLPresenceWeb.Pages.Clients
{
    public partial class ChangePassword : ComponentBase
    {
        private ChangePasswordModel model = new();
        private string currentPasswordError = "";
        private string newPasswordError = "";
        private string confirmPasswordError = "";
        private string apiErrorMessage = "";
        private string SuccessModalText = "";

        private string currentPasswordInputType = "password";
        private string newPasswordInputType = "password";
        private string confirmPasswordInputType = "password";
        private bool ShowChangeSuccessModal = false;

        private void ToggleCurrentPassword() => currentPasswordInputType = currentPasswordInputType == "password" ? "text" : "password";
        private void ToggleNewPassword() => newPasswordInputType = newPasswordInputType == "password" ? "text" : "password";
        private void ToggleConfirmPassword() => confirmPasswordInputType = confirmPasswordInputType == "password" ? "text" : "password";

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            if (!authState.User.Identity.IsAuthenticated)
            {
                Navigation.NavigateTo("/login", true);
            }
        }

        private async Task HandleSubmit()
        {
            ClearErrors();

            var checkResult = await CheckCurrentPassword(model.oldPassword);
            if (!checkResult)
            {
                currentPasswordError = "Current password is incorrect.";
                return;
            }

            if (!IsNewPasswordValid(model.newPassword))
            {
                newPasswordError = "Password must be at least 8 characters, contain a number and a special character.";
                return;
            }
            if (model.newPassword != model.confirmPassword)
            {
                confirmPasswordError = "Passwords do not match.";
                return;
            }

            var dto = new ChangePasswordDTO
            {
                OldPassword = model.oldPassword,
                NewPassword = model.newPassword,
                ConfirmPassword = model.confirmPassword
            };
            var response = await Http.PutAsJsonAsync(APIUrl.Endpoint("me/password"), dto);

            try
            {
                if (response.IsSuccessStatusCode)
                {
                    model = new();
                    await AlertService.ShowSuccessAsync(SuccessModalText);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    apiErrorMessage = !string.IsNullOrWhiteSpace(errorContent) ? errorContent : "Failed to change password. Please try again.";
                    await AlertService.ShowErrorAsync(apiErrorMessage);
                }
            }
            catch
            {
                await AlertService.ShowErrorAsync("An error occurred while connecting to the server.");
            }
        }

        private void ClearErrors()
        {
            currentPasswordError = "";
            newPasswordError = "";
            confirmPasswordError = "";
            apiErrorMessage = "";
            SuccessModalText = "";
        }

        private async Task<bool> CheckCurrentPassword(string currentPassword)
        {
            await Task.Delay(100);
            return true;
        }

        private bool IsNewPasswordValid(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return false;
            if (!password.Any(char.IsDigit))
                return false;
            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
                return false;
            return true;
        }

        private void GoBack() => Navigation.NavigateTo("/home/profile");
    }
}