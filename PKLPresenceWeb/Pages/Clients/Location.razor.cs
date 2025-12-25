using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PKLPresenceWeb.Model;
using System.ComponentModel.Design;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace PKLPresenceWeb.Pages.Clients
{
    public partial class Location : ComponentBase
    {
        private string photoUrl = "/images/default_profile.jpg";
        private bool IsLoading = true;

        // Company Detail
        private CompanyDetailResponse? CompanyDetail;

        // Locations
        private List<CompanyLocationInfo> CompanyLocations = new();
        private CompanyLocationInfo? SelectedLocation;
        private int SelectedLocationIndex = -1;

        // Modal
        private bool ShowDetailModal = false;
        private bool ShowWarningModal = false;

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            if (!authState.User.Identity.IsAuthenticated)
            {
                Navigation.NavigateTo("/login", true);
                return;
            }

            var companyId = CompanyState.CompanyId;
            if (string.IsNullOrWhiteSpace(companyId.ToString()))
            {
                await AlertService.ShowWarningAsync("Tidak ada perusahaan yang dipilih untuk ditampilkan data lokasinya.");
                CloseWarningModal();
            }

            // Ambil data user dari localStorage
            var meJson = await JS.InvokeAsync<string>("localStorage.getItem", "meResponse");
            if (string.IsNullOrWhiteSpace(meJson))
                return;

            var root = JsonDocument.Parse(meJson).RootElement;

            // Ambil foto profile
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

            await LoadCompanyDetail();
            IsLoading = false;
        }

        private async Task LoadCompanyDetail()
        {
            IsLoading = true;
            StateHasChanged();

            var url = APIUrl.Endpoint("data/companies/details");
            var companyId = CompanyState.CompanyId;
            var response = await Http.PostAsJsonAsync(url, companyId);
            if (response.IsSuccessStatusCode)
            {
                CompanyDetail = await response.Content.ReadFromJsonAsync<CompanyDetailResponse>();
                CompanyLocations = CompanyDetail?.locations?
                    .ToList() ?? new List<CompanyLocationInfo>();
            }
            else
            {
                CompanyDetail = null;
                CompanyLocations = new List<CompanyLocationInfo>();
            }

            IsLoading = false;
            StateHasChanged();
        }

        private void OpenLocationDetail(CompanyLocationInfo location, int index)
        {
            SelectedLocation = location;
            SelectedLocationIndex = index;
            ShowDetailModal = true;
        }

        private void CloseDetailModal()
        {
            ShowDetailModal = false;
            SelectedLocation = null;
            SelectedLocationIndex = -1;
        }

        private void CloseWarningModal()
        {
            Navigation.NavigateTo("/participant", true);
        }

        private bool ShowCompanyModal = false;
        private string ShowCompanyModalMode = "add";
        private int? EditCompanyId = null;

        private string CoordinateInput = "";
        private bool IsCompanyValid =>
            !string.IsNullOrWhiteSpace(NewCompany?.Name) &&
            !string.IsNullOrWhiteSpace(NewCompany?.Address) &&
            !string.IsNullOrWhiteSpace(NewCompany?.Lat) &&
            !string.IsNullOrWhiteSpace(NewCompany?.Long);

        private CompanyLocationModel NewCompany = new();
        private const string LocationIQKey = "pk.e2145fd6b15e111a0fddb4586b415ed0";

        [Inject] private IJSRuntime JSRuntime { get; set; }
        private DotNetObjectReference<Location>? _dotNetRef;
        double CurrentLat = 0;
        double CurrentLng = 0;

        private async Task ShowAddCompanyModal()
        {
            ShowCompanyModalMode = "add";
            EditCompanyId = null;
            ShowCompanyModal = true;

            await Task.Delay(100);
            var pos = await JS.InvokeAsync<GeolocationPosition>("getCurrentPosition");
            await UpdateMapAndAddress(pos.coords.latitude, pos.coords.longitude);
        }

        private async Task WaitForElementAsync(string elementId, int timeoutMs = 2000, int pollIntervalMs = 50)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    var exists = await JSRuntime.InvokeAsync<bool>("eval", $"document.getElementById('{elementId}') !== null");
                    if (exists) return;
                }
                catch { }
                await Task.Delay(pollIntervalMs);
            }
        }

        private async Task OnEditCompanyClicked(CompanyLocationInfo company)
        {
            if (company == null) return;
            var companyId = CompanyState.CompanyId;
            try
            {
                var response = await Http.PostAsJsonAsync(APIUrl.Endpoint("data/companies/details"), companyId);
                if (!response.IsSuccessStatusCode)
                {
                    await AlertService.ShowErrorAsync("Gagal mengambil data perusahaan.");
                    return;
                }

                var result = await response.Content.ReadFromJsonAsync<CompanyDetailResponse>();
                if (result?.company == null)
                {
                    await AlertService.ShowErrorAsync("Response perusahaan tidak valid.");
                    return;
                }

                var activeLocation = result.locations?.FirstOrDefault();

                NewCompany = new CompanyLocationModel
                {
                    Name = activeLocation?.locationName ?? string.Empty,
                    Address = activeLocation?.address ?? string.Empty,
                    Lat = activeLocation?.lat.ToString("F12", CultureInfo.InvariantCulture) ?? string.Empty,
                    Long = activeLocation?.longitude.ToString("F12", CultureInfo.InvariantCulture) ?? string.Empty
                };

                CoordinateInput = string.IsNullOrWhiteSpace(NewCompany.Lat) || string.IsNullOrWhiteSpace(NewCompany.Long)
                    ? string.Empty
                    : $"{NewCompany.Lat}, {NewCompany.Long}";

                ShowCompanyModalMode = "edit";
                EditCompanyId = activeLocation?.id;
                ShowCompanyModal = true;

                StateHasChanged();
                await Task.Yield();
                await WaitForElementAsync("companyMap", timeoutMs: 3000);

                double lat = 0, lon = 0;
                bool hasCoords =
                    double.TryParse(NewCompany.Lat, NumberStyles.Any, CultureInfo.InvariantCulture, out lat) &&
                    double.TryParse(NewCompany.Long, NumberStyles.Any, CultureInfo.InvariantCulture, out lon);

                if (!hasCoords)
                {
                    try
                    {
                        var pos = await JS.InvokeAsync<GeolocationPosition>("getCurrentPosition");
                        lat = pos.coords.latitude;
                        lon = pos.coords.longitude;

                        NewCompany.Lat = lat.ToString("F12", CultureInfo.InvariantCulture);
                        NewCompany.Long = lon.ToString("F12", CultureInfo.InvariantCulture);
                        CoordinateInput = $"{NewCompany.Lat}, {NewCompany.Long}";
                    }
                    catch
                    {
                        lat = 0;
                        lon = 0;
                    }
                }

                try
                {
                    await UpdateMapAndAddress(lat, lon);
                }
                catch
                {
                    await Task.Delay(120);
                    await UpdateMapAndAddress(lat, lon);
                }
                StateHasChanged();
            }
            catch (Exception ex)
            {
                await AlertService.ShowErrorAsync($"Error: {ex.Message}");
            }
        }

        private void CancelAddCompany()
        {
            ShowCompanyModal = false;
            NewCompany = new();
            CoordinateInput = "";
            ShowCompanyModalMode = "add";
            EditCompanyId = null;

            _dotNetRef?.Dispose();
            _dotNetRef = null;
        }

        private async Task ConfirmAddCompany()
        {
            var companyId = CompanyState.CompanyId;
            if (ShowCompanyModalMode == "add")
            {
                var payload = new
                {
                    companyid = companyId,
                    name = NewCompany.Name,
                    address = NewCompany.Address,
                    lat = NewCompany.Lat,
                    @long = NewCompany.Long
                };
                var response = await Http.PostAsJsonAsync(APIUrl.Endpoint("data/company-location/add"), payload);

                if (response.IsSuccessStatusCode)
                {
                    ShowCompanyModal = false;
                    await AlertService.ShowSuccessAsync("Lokasi baru berhasil ditambahkan.");
                    await LoadCompanyDetail();
                }
            }
            else
            {
                if (!EditCompanyId.HasValue)
                {
                    await AlertService.ShowErrorAsync("Company id tidak ditemukan.");
                    return;
                }

                if (!double.TryParse(NewCompany.Lat, NumberStyles.Any, CultureInfo.InvariantCulture, out var lat))
                {
                    await AlertService.ShowErrorAsync("Koordinat lat tidak valid.");
                    return;
                }
                if (!double.TryParse(NewCompany.Long, NumberStyles.Any, CultureInfo.InvariantCulture, out var lon))
                {
                    await AlertService.ShowErrorAsync("Koordinat long tidak valid.");
                    return;
                }

                var payload = new
                {
                    name = NewCompany.Name,
                    address = NewCompany.Address,
                    lat = NewCompany.Lat,
                    @long = NewCompany.Long
                };

                var response = await Http.PutAsJsonAsync(APIUrl.Endpoint($"data/company-location/{EditCompanyId}"), payload);
                if (response.IsSuccessStatusCode)
                {
                    CancelAddCompany();
                    await AlertService.ShowSuccessAsync("Data lokasi perusahaan berhasil diubah.");
                    await LoadCompanyDetail();
                }
                else await AlertService.ShowErrorAsync("Gagal mengupdate perusahaan.");
            }
        }

        private void OpenGoogleMaps()
        {
            if (!string.IsNullOrWhiteSpace(NewCompany.Lat) &&
                !string.IsNullOrWhiteSpace(NewCompany.Long))
            {
                string url =
                    $"https://www.google.com/maps/search/?api=1&query={NewCompany.Lat},{NewCompany.Long}";
                JS.InvokeVoidAsync("open", url, "_blank");
            }
        }

        private async Task OnCoordinateChanged(ChangeEventArgs e)
        {
            CoordinateInput = e.Value?.ToString() ?? "";
            var parts = CoordinateInput.Split(',');
            if (parts.Length != 2) return;

            if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) &&
                double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double lon))
            {
                await UpdateMapAndAddress(lat, lon);
            }
        }

        [JSInvokable]
        public Task NotifyAddressFromJs(string address)
        {
            NewCompany.Address = address ?? string.Empty;
            StateHasChanged();
            return Task.CompletedTask;
        }

        private async Task OnAddressClicked()
        {
            try
            {
                var value = await JS.InvokeAsync<string>("eval", "document.getElementById('company-address')?.value || ''");
                if (value is not null)
                {
                    NewCompany.Address = value;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                await JS.InvokeVoidAsync("console.error", $"OnAddressClicked error: {ex.Message}");
            }
        }

        [JSInvokable]
        public async Task OnMapClicked(double lat, double lng)
        {
            NewCompany.Lat = lat.ToString("F12", CultureInfo.InvariantCulture);
            NewCompany.Long = lng.ToString("F12", CultureInfo.InvariantCulture);
            CoordinateInput = $"{NewCompany.Lat}, {NewCompany.Long}";

            await GetAddressFromCoordinates(lat, lng);
            StateHasChanged();
        }

        private async Task GetAddressFromCoordinates(double lat, double lon)
        {
            var url =
                $"https://us1.locationiq.com/v1/reverse?key={LocationIQKey}&lat={lat}&lon={lon}&format=json";

            try
            {
                var json = await Http.GetFromJsonAsync<JsonElement>(url);
                if (json.TryGetProperty("display_name", out var addr))
                {
                    NewCompany.Address = addr.GetString() ?? "";
                    StateHasChanged();
                }
            }
            catch { }
        }

        private async Task UpdateMapAndAddress(double lat, double lon)
        {
            NewCompany.Lat = lat.ToString("F12", CultureInfo.InvariantCulture);
            NewCompany.Long = lon.ToString("F12", CultureInfo.InvariantCulture);

            if (_dotNetRef == null)
                _dotNetRef = DotNetObjectReference.Create(this);

            await JS.InvokeVoidAsync(
                "renderCompanyMap",
                "companyMap",
                lat, lon,
                LocationIQKey,
                _dotNetRef
            );
            await GetAddressFromCoordinates(lat, lon);
        }

        private bool ShowDeleteClassModal = false;
        private int? DeleteCompanyLocationId = null;
        private string DeleteCompanyLocationName = "";

        private void OnDeleteCompanyClicked(CompanyLocationInfo company)
        {
            DeleteCompanyLocationId = company.id;
            DeleteCompanyLocationName = company.locationName;
            ShowDeleteClassModal = true;
            StateHasChanged();
        }

        private void CancelDeleteCompany()
        {
            ShowDeleteClassModal = false;
            DeleteCompanyLocationId = null;
            DeleteCompanyLocationName = "";
            StateHasChanged();
        }

        private async void ConfirmDeleteCompany()
        {
            ShowDeleteClassModal = false;

            if (!DeleteCompanyLocationId.HasValue)
                return;

            var request = new HttpRequestMessage(HttpMethod.Delete, APIUrl.Endpoint("data/company-location/delete"))
            {
                Content = new StringContent(JsonSerializer.Serialize(DeleteCompanyLocationId.Value), System.Text.Encoding.UTF8, "application/json")
            };
            var response = await Http.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                DeleteCompanyLocationId = null;
                DeleteCompanyLocationName = "";
                await AlertService.ShowSuccessAsync("Lokasi perusahaan berhasil dihapus.");
                await LoadCompanyDetail();
            }
            else await AlertService.ShowErrorAsync("Gagal menghapus company. Silakan coba lagi.");
        }

        private void GoBack() => Navigation.NavigateTo("/participant");
        private void GoToProfile() => Navigation.NavigateTo("/home/profile");
    }
}
