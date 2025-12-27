using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.Json;

namespace PKLPresenceWeb.Layout
{
    public partial class BottomNav : ComponentBase
    {
        private string currentPath = "";
        private bool isStudent = false;
        private bool isDataComplete = true;
        private bool showWarningModal = false;
        private bool isInitialized = false;

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
                        var email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() ?? "" : "";
                        var companyName = dataProp.TryGetProperty("company", out var companyProp) ? companyProp.GetString() ?? "" : "";
                        isDataComplete =
                            !string.IsNullOrWhiteSpace(email) && email != "-" &&
                            !string.IsNullOrWhiteSpace(companyName) && companyName != "-";
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
            if (isStudent && !isDataComplete)
                showWarningModal = true;
            else Navigation.NavigateTo($"/{menu}");
        }

        private void CloseModal()
        {
            showWarningModal = false;
            NavigationManager.NavigateTo("/home/profile/me");
        }

        public void Dispose()
        {
            Navigation.LocationChanged -= OnLocationChanged;
        }
    }
}