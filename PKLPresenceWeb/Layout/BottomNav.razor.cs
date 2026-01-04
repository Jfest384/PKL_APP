using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PKLPresenceWeb.Helper;
using PKLPresenceWeb.Model;
using System.Text.Json;

namespace PKLPresenceWeb.Layout
{
    public partial class BottomNav : ComponentBase
    {
        private string currentPath = "";
        private bool isStudent = false;
        private bool isDataComplete = true;
        private bool isInitialized = false;
        private bool isPKLStudent = true;

        private string ParticipantMenuLabel =>
            isAdminOrKepalaJurusan ? "Management" : "Participant";

        private string ParticipantMenuIcon =>
            isAdminOrKepalaJurusan ? "bi-gear" : "bi-person";

        private bool isAdminOrKepalaJurusan =>
            userRole == "Admin" || userRole == "Kepala Jurusan";

        private string userRole = "";

        protected override async Task OnInitializedAsync()
        {
            UpdatePath();
            Navigation.LocationChanged += OnLocationChanged;
            await CheckUserRoleAndValidateData();
        }

        private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
        {
            UpdatePath();
            StateHasChanged();
        }

        private void UpdatePath()
        {
            currentPath = Navigation.ToBaseRelativePath(Navigation.Uri).Split('?')[0];
        }

        private string GetClass(string path) =>
            IsActive(path) ? "nav-item-custom active" : "nav-item-custom";

        private bool IsActive(string path) =>
            string.Equals(currentPath, path, StringComparison.OrdinalIgnoreCase);

        private async Task CheckUserRoleAndValidateData()
        {
            try
            {
                var meJson = await JS.InvokeAsync<string>("localStorage.getItem", "meResponse");
                if (string.IsNullOrWhiteSpace(meJson))
                {
                    isDataComplete = false;
                    isInitialized = true;
                    return;
                }

                using var doc = JsonDocument.Parse(meJson);
                var root = doc.RootElement;

                userRole = root.TryGetProperty("role", out var roleProp) ? roleProp.GetString() ?? "" : "";
                isStudent = userRole == "Student";

                if (isStudent)
                {
                    if (root.TryGetProperty("data", out var dataProp))
                    {
                        if (dataProp.TryGetProperty("isPKL", out var isPKLProp))
                        {
                            bool isPKL = false;

                            if (isPKLProp.ValueKind == JsonValueKind.True) isPKL = true;
                            else if (isPKLProp.ValueKind == JsonValueKind.False) isPKL = false;
                            else if (isPKLProp.ValueKind == JsonValueKind.String)
                                bool.TryParse(isPKLProp.GetString(), out isPKL);

                            isPKLStudent = isPKL;
                            if (!isPKL)
                            {
                                isDataComplete = false;
                                isInitialized = true;
                                return;
                            }
                        }

                        var email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() ?? "" : "";
                        var companyLocationName = dataProp.TryGetProperty("companyLocation", out var companyProp) ? companyProp.GetString() ?? "" : "";
                        isDataComplete =
                            !string.IsNullOrWhiteSpace(email) && email != "-" &&
                            !string.IsNullOrWhiteSpace(companyLocationName) && companyLocationName != "-";
                    }
                    else isDataComplete = false;
                }
                else isDataComplete = true;
            }
            catch
            {
                isDataComplete = false;
            }
            isInitialized = true;
        }

        private void HandleMenuClick(string menu)
        {
            if (!isInitialized) return;
            if (isStudent && !isPKLStudent)
            {
                ShowNotPKLAlert();
                return;
            }
            else if (isStudent && !isDataComplete)
            {
                ShowValidationAlert();
                return;
            }
            Navigation.NavigateTo($"/{menu}");
        }

        private async Task CompletingData()
        {
            NavigationManager.NavigateTo("/home/profile/me");
        }

        public void Dispose()
        {
            Navigation.LocationChanged -= OnLocationChanged;
        }

        private async void ShowValidationAlert()
        {
            var result = await JS.InvokeAsync<SwalResult>("Swal.fire", new
            {
                title = "Warning!",
                text = "Pastikan sudah melengkapi data diri Anda.",
                icon = "warning",
                showConfirmButton = true,
                confirmButtonText = "Complete",
                width = "90%",
                customClass = new { popup = "my-swal-popup" }
            });

            if (result.isConfirmed)
                await CompletingData();
        }

        private async void ShowNotPKLAlert()
        {
            await AlertService.ShowWarningAsync(
                "Anda tidak dapat mengakses web ini lebih lanjut, karena Anda tidak terdaftar sebagai siswa PKL."
            );
        }

    }
}