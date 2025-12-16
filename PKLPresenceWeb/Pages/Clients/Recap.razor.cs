using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PKLPresenceWeb.Model;
using System.Net.Http.Json;

namespace PKLPresenceWeb.Pages.Clients
{
    public partial class Recap : ComponentBase
    {
        private PresenceRecap? PresenceRecap;
        private ReportRecap? ReportRecap;
        private bool IsLoading = true;

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            if (!authState.User.Identity.IsAuthenticated)
            {
                Navigation.NavigateTo("/login", true);
                return;
            }

            var meJson = await JS.InvokeAsync<string>("localStorage.getItem", "meResponse");
            if (string.IsNullOrWhiteSpace(meJson))
            {
                IsLoading = false;
                return;
            }

            int studentId = 0;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(meJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("id", out var idProp))
                    studentId = idProp.GetInt32();
            }
            catch
            {
                IsLoading = false;
                return;
            }

            try
            {
                var payload = new { studentId };
                var presenceResponse = await Http.PostAsJsonAsync(APIUrl.Endpoint("recap/presence"), payload);
                if (presenceResponse.IsSuccessStatusCode)
                {
                    PresenceRecap = await presenceResponse.Content.ReadFromJsonAsync<PresenceRecap>();
                }
            }
            catch { }

            try
            {
                var payload = new { studentId };
                var reportResponse = await Http.PostAsJsonAsync(APIUrl.Endpoint("recap/report"), payload);
                if (reportResponse.IsSuccessStatusCode)
                {
                    ReportRecap = await reportResponse.Content.ReadFromJsonAsync<ReportRecap>();
                }
            }
            catch { }

            IsLoading = false;
        }

        private void GoBack() => Navigation.NavigateTo("/home/profile");

        private async Task PrintRecap()
        {
            var meJson = await JS.InvokeAsync<string>("localStorage.getItem", "meResponse");
            if (string.IsNullOrWhiteSpace(meJson)) return;

            int studentId = 0;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(meJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("id", out var idProp))
                    studentId = idProp.GetInt32();
            }
            catch
            {
                await AlertService.ShowErrorAsync("Gagal mengambil studentId.");
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, APIUrl.Endpoint("recap/print"));
                request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { studentId }), System.Text.Encoding.UTF8, "application/json");

                var response = await Http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var fileBytes = await response.Content.ReadAsByteArrayAsync();
                    var fileName = FileHelper.GetFileNameFromContentDisposition(response);
                    await JS.InvokeVoidAsync("downloadFileFromBytes", fileName, "application/pdf", fileBytes);
                    await AlertService.ShowSuccessAsync("Rekap Presensi dan Bimbingan Laporan berhasil diunduh.");
                }
            }
            catch
            {
                await AlertService.ShowErrorAsync("Terjadi kesalahan saat mengunduh file recap.");
            }
        }
    }
}