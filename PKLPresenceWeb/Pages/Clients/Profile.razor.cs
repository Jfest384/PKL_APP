using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PKLPresenceWeb.Model;
using System.Net.Http.Json;
using System.Text.Json;

namespace PKLPresenceWeb.Pages.Clients
{
    public partial class Profile : ComponentBase
    {
        private string fullName = "";
        private string email = "";
        private string userRole = "";
        private string photoUrl = "/images/default_profile.jpg";
        private bool isMessageActive = false;
        private bool isLoadingSwitch = false;
        private bool isRekapDisabled = false;
        private bool isDark = false;
        private string MessageStatus = "";

        protected override async Task OnInitializedAsync()
        {
            var savedTheme = await JS.InvokeAsync<string>("themeManager.getTheme");
            isDark = savedTheme == "dark";

            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            if (!authState.User.Identity.IsAuthenticated)
            {
                Navigation.NavigateTo("/login", true);
                return;
            }

            var meJson = await JS.InvokeAsync<string>("localStorage.getItem", "meResponse");
            if (string.IsNullOrWhiteSpace(meJson)) return;
            var root = JsonDocument.Parse(meJson).RootElement;

            fullName = root.TryGetProperty("fullname", out var fnProp) ? fnProp.GetString() ?? "" : "";
            email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() ?? "" : "";
            userRole = root.TryGetProperty("role", out var roleProp) ? roleProp.GetString() ?? "" : "";

            // Ambil foto profile jika ada (opsional, tetap gunakan API jika perlu)
            if (root.TryGetProperty("profile", out var profileProp) &&
                profileProp.ValueKind != JsonValueKind.Null)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, APIUrl.Endpoint("me/photo"));
                var response = await Http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    var base64 = Convert.ToBase64String(bytes);
                    photoUrl = $"data:{contentType};base64,{base64}";
                }
            }

            if (userRole == "Student")
            {
                int studentId = 0;
                if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Object)
                {
                    if (dataProp.TryGetProperty("id", out var idProp))
                        studentId = idProp.GetInt32();
                }

                var payload = new { studentId };
                var response = await Http.PostAsJsonAsync(APIUrl.Endpoint("recap/validation"), payload);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var result = doc.RootElement.GetProperty("result").GetString();
                    isRekapDisabled = result == "No";
                }
            }

            await LoadWahaSessionStatus();
        }

        private async Task Logout()
        {
            var savedTheme = await JS.InvokeAsync<string>("localStorage.getItem", "theme");
            var response = await Http.PostAsync(APIUrl.Endpoint("authentication/logout"), null);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("message", out var msgProp) && msgProp.GetString() == "Logged out")
                {
                    await JS.InvokeVoidAsync("localStorage.clear");
                    if (!string.IsNullOrEmpty(savedTheme))
                        await JS.InvokeVoidAsync("localStorage.setItem", "theme", savedTheme);
                    Navigation.NavigateTo("/login", true);
                }
            }
        }

        private async void OnActivateMessageChanged(bool value)
        {
            isLoadingSwitch = true;
            try
            {
                HttpResponseMessage res;
                if (value)
                    res = await Http.PostAsync(APIUrl.Endpoint("waha/sessions/default/start"), null);
                else
                    res = await Http.PostAsync(APIUrl.Endpoint("waha/sessions/default/stop"), null);

                if (res.IsSuccessStatusCode)
                {
                    var waha = await res.Content.ReadFromJsonAsync<WahaSession>();
                    var status = waha?.status ?? "";
                    isMessageActive = status == "STARTING" || status == "WORKING";

                    MessageStatus = value ? "Message berhasil diaktifkan." : "Message berhasil dimatikan.";
                    await AlertService.ShowSuccessAsync(MessageStatus);
                }
                else await LoadWahaSessionStatus();
            }
            catch
            {
                await LoadWahaSessionStatus();
            }
            finally
            {
                isLoadingSwitch = false;
                StateHasChanged();
            }
        }

        public record MenuItemData(string icon, string text, string link);
        private async void GoBack() => await JS.InvokeVoidAsync("goBack");

        private async Task LoadWahaSessionStatus()
        {
            try
            {
                isLoadingSwitch = true;
                var res = await Http.GetAsync(APIUrl.Endpoint("waha/sessions/default"));
                if (res.IsSuccessStatusCode)
                {
                    var waha = await res.Content.ReadFromJsonAsync<WahaSession>();
                    var status = waha?.status ?? "";
                    isMessageActive = status == "STARTING" || status == "WORKING";
                }
            }
            catch { }
            finally { isLoadingSwitch = false; StateHasChanged(); }
        }

        private async Task ToggleThemeFromProfile()
        {
            isDark = !isDark;
            await JS.InvokeVoidAsync("themeManager.applyTheme", isDark);
            Navigation.NavigateTo(Navigation.Uri, forceLoad: true);
        }
    }
}