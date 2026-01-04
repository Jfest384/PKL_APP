using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using PKLPresenceWeb.Model;
using System.Net.Http.Json;
using System.Text.Json;

namespace PKLPresenceWeb.Pages.Clients
{
    public partial class MyProfile : ComponentBase
    {
        private string profilePhotoPreview = "/images/default_profile.jpg";
        private IBrowserFile? profilePhotoFile;
        private bool isPhotoChanged = false;

        private string nisnipFullname = "";
        private string? nis = null;
        private string? nip = null;
        private string fullname = "";
        private bool showNisNipFullnameError = false;

        private List<ClassItem> classList = new();
        private int TotalPages = 1;
        private int CurrentPage = 1;
        private int? selectedClassId;
        private List<CompanyLocationItem> companyLocationList = new();
        private int? selectedCompanyLocationId;

        private string email = "";
        private string emailError = "";

        private bool? gender = null;

        private string role = "";
        private string IdLabel => role == "Student" ? "NIS" : "NIP";
        private string IdPlaceholder => role == "Student" ? "12345678" : "1987654321";

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            if (!authState.User.Identity.IsAuthenticated)
            {
                Navigation.NavigateTo("/login", true);
                return;
            }

            try
            {
                var userResponse = await Http.GetFromJsonAsync<UserResponse>(APIUrl.Endpoint("me"));
                if (userResponse == null) return;

                role = userResponse.role ?? "";
                email = userResponse.email ?? "";
                gender = userResponse.gender;

                var userData = userResponse.data;
                if (role == "Student")
                {
                    nis = userData.nis;
                    fullname = userData.fullname;
                    selectedClassId = userData.classId;
                    nisnipFullname = $"{nis} - {fullname}";

                    await LoadClasses();
                    var companyResp = await Http.GetFromJsonAsync<CompanyLocationListResponse>(APIUrl.Endpoint("data/company-locations?page=1&pageSize=1000"));
                    companyLocationList = companyResp?.companyLocations ?? new();

                    selectedClassId = classList.FirstOrDefault(c => c.name == userData.classroom)?.id;
                    selectedCompanyLocationId = companyLocationList.FirstOrDefault(c => c.LocationName == userData.companyLocation)?.id;
                }
                else
                {
                    nip = userData.nip;
                    fullname = userData.fullname;
                    nisnipFullname = $"{nip} - {fullname}";
                }

                // Ambil foto profil seperti sebelumnya
                var photoReq = new HttpRequestMessage(HttpMethod.Get, APIUrl.Endpoint("me/photo"));
                var photoResp = await Http.SendAsync(photoReq);
                if (photoResp.IsSuccessStatusCode)
                {
                    var bytes = await photoResp.Content.ReadAsByteArrayAsync();
                    var contentType = photoResp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    var base64 = Convert.ToBase64String(bytes);
                    profilePhotoPreview = $"data:{contentType};base64,{base64}";
                }
            }
            catch (Exception) { }
        }

        private void TriggerPhotoInput()
        {
            JS.InvokeVoidAsync("document.getElementById", "profilePhotoInput").AsTask();
            JS.InvokeVoidAsync("eval", "document.getElementById('profilePhotoInput').click()");
        }

        private async Task OnPhotoChanged(InputFileChangeEventArgs e)
        {
            if (e.File != null)
            {
                const long maxFileSize = 2 * 1024 * 1024; // 2 MB
                if (e.File.Size > maxFileSize)
                {
                    await AlertService.ShowErrorAsync("Ukuran file maksimal 2 MB.");
                    return;
                }

                profilePhotoFile = e.File;
                isPhotoChanged = true;
                var buffer = new byte[e.File.Size];
                await e.File.OpenReadStream(maxFileSize).ReadAsync(buffer);
                var base64 = Convert.ToBase64String(buffer);
                profilePhotoPreview = $"data:{e.File.ContentType};base64,{base64}";
            }
        }

        private void OnNisNipFullnameChanged(ChangeEventArgs e)
        {
            nisnipFullname = e.Value?.ToString() ?? "";
            ParseNisNipFullname();
        }

        private void ParseNisNipFullname()
        {
            showNisNipFullnameError = false;
            nis = null;
            nip = null;
            fullname = "";
            var parts = nisnipFullname.Split('-', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                if (role == "Student") nis = parts[0];
                else nip = parts[0];
                fullname = parts[1];
            }
            else showNisNipFullnameError = true;
        }

        private void ValidateEmail(ChangeEventArgs e)
        {
            email = e.Value?.ToString() ?? "";
            emailError = "";
            if (string.IsNullOrWhiteSpace(email))
                emailError = "Email tidak boleh kosong";
            else if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                emailError = "Format email tidak valid";
        }

        private void SetGender(bool isMale)
        {
            gender = isMale;
        }

        private bool IsFormValid =>
            !string.IsNullOrWhiteSpace(nisnipFullname) &&
            !showNisNipFullnameError &&
            ((role == "Student" && !string.IsNullOrWhiteSpace(nis)) || (role != "Student" && !string.IsNullOrWhiteSpace(nip))) &&
            !string.IsNullOrWhiteSpace(fullname) &&
            (role != "Student" || (selectedClassId.HasValue && selectedCompanyLocationId.HasValue)) &&
            !string.IsNullOrWhiteSpace(email) &&
            string.IsNullOrEmpty(emailError) &&
            gender.HasValue;

        private async Task SaveProfile()
        {
            if (!IsFormValid) return;
            bool success = false;

            // 1. Update profile photo (if changed)
            if (isPhotoChanged && profilePhotoFile != null)
            {
                var content = new MultipartFormDataContent();
                var stream = profilePhotoFile.OpenReadStream(long.MaxValue);
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(profilePhotoFile.ContentType);
                content.Add(fileContent, "image", profilePhotoFile.Name);

                var req = new HttpRequestMessage(HttpMethod.Put, APIUrl.Endpoint("me/photo"));
                req.Content = content;
                var resp = await Http.SendAsync(req);
                if (resp.IsSuccessStatusCode)
                    success = true;
            }

            // 2. Update profile data
            var data = new Dictionary<string, object?>
            {
                { "nis", role == "Student" ? nis : null },
                { "nip", role != "Student" ? nip : null },
                { "fullname", fullname },
                { "classroomid", role == "Student" ? selectedClassId : 0 },
                { "companyLocationId", role == "Student" ? selectedCompanyLocationId : 0 },
                { "email", email },
                { "gender", gender }
            };

            var json = JsonSerializer.Serialize(data);
            var req2 = new HttpRequestMessage(HttpMethod.Put, APIUrl.Endpoint("me"));
            req2.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var resp2 = await Http.SendAsync(req2);
            if (resp2.IsSuccessStatusCode)
                success = true;

            if (success)
            {
                var responseMe = await Http.GetAsync(APIUrl.Endpoint("me"));
                var json2 = await responseMe.Content.ReadAsStringAsync();
                await JS.InvokeVoidAsync("localStorage.setItem", "meResponse", json2);
                await AlertService.ShowSuccessAsync("Data Profile berhasil diubah.");
                StateHasChanged();
                Navigation.NavigateTo("/home/profile/me", forceLoad: true);
            }
        }

        private async Task LoadClasses()
        {
            var response = await Http.GetFromJsonAsync<ClassListResponse>(APIUrl.Endpoint($"classes?page={CurrentPage}"));
            if (response != null)
            {
                classList = response.classrooms ?? new();
                TotalPages = response.totalPages;
            }
        }

        private void GoBack() => Navigation.NavigateTo("/home/profile");
    }
}