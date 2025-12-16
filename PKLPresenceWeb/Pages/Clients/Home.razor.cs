using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using PKLPresenceWeb.Model;
using System.Text.Json;

namespace PKLPresenceWeb.Pages.Clients
{
    public partial class HomeBase : ComponentBase
    {
        [Inject] protected IJSRuntime JS { get; set; } = default!;
        [Inject] protected HttpClient Http { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

        public string fullName = "";
        public string secondLine = "";
        public string userRole = "";
        public string photoUrl = "/images/default_profile.jpg";

        protected override async Task OnInitializedAsync()
        {
            var meJson = await JS.InvokeAsync<string>("localStorage.getItem", "meResponse");
            if (string.IsNullOrWhiteSpace(meJson))
                return;

            var root = JsonDocument.Parse(meJson).RootElement;
            fullName = root.TryGetProperty("fullname", out var fnProp) ? fnProp.GetString() ?? "" : "";
            userRole = root.TryGetProperty("role", out var roleProp) ? roleProp.GetString() ?? "" : "";

            // Data Student
            if (userRole.ToLower() == "student" &&
                root.TryGetProperty("data", out var dataProp))
            {
                if (dataProp.TryGetProperty("classroom", out var classroomProp))
                    secondLine = classroomProp.GetString() ?? "";
            }
            else secondLine = userRole;

            // Foto (juga tanpa header Authorization)
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
        }

        public void GoToProfile() => Navigation.NavigateTo("/home/profile");
    }
}