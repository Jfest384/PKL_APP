using Microsoft.AspNetCore.Components;
using PKLPresenceWeb.Model;
using System.Net.Http.Json;

namespace PKLPresenceWeb.Pages.Clients
{
    public partial class Approval : ComponentBase
    {
        [Parameter] public string? Id { get; set; }

        [Inject] protected HttpClient Http { get; set; } = default!;

        private PresenceRecap? PresenceRecap;
        private ReportRecap? ReportRecap;
        private bool IsLoading = true;
        private string? ErrorMessage;

        protected override async Task OnInitializedAsync()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                ErrorMessage = "Invalid request: missing ID.";
                IsLoading = false;
                return;
            }

            try
            {
                var presenceResponse = await Http.GetAsync(APIUrl.Endpoint($"recap/presence?id={Id}"));
                if (presenceResponse.IsSuccessStatusCode)
                    PresenceRecap = await presenceResponse.Content.ReadFromJsonAsync<PresenceRecap>();
                else
                    ErrorMessage = await presenceResponse.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }

            try
            {
                var reportResponse = await Http.GetAsync(APIUrl.Endpoint($"recap/report?id={Id}"));
                if (reportResponse.IsSuccessStatusCode)
                    ReportRecap = await reportResponse.Content.ReadFromJsonAsync<ReportRecap>();
                else
                    ErrorMessage = await reportResponse.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            IsLoading = false;
        }
    }
}