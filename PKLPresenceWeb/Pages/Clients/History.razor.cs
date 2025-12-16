using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PKLPresenceWeb.Model;
using System.Net.Http.Json;
using System.Text.Json;

namespace PKLPresenceWeb.Pages.Clients
{
    public partial class History : ComponentBase
    {
        private string photoUrl = "/images/default_profile.jpg";
        private bool IsLoading = true;

        // Presence History
        private int PresencePage = 1;
        private int TotalPagesPresence => PresenceHistory?.totalPages ?? 1;
        private PresenceHistoryResponse? PresenceHistory;

        // Report History
        private int ReportPage = 1;
        private int TotalPagesReport => ReportHistory?.totalPages ?? 1;
        private ReportHistoryResponse? ReportHistory;

        bool ShowDetailModal = false;
        PresenceItem? SelectedPresenceDetail = null;
        private List<PresenceItem> Presences = new();
        private int SelectedPresenceIndex = -1;
        Dictionary<string, string?> PreviewPresencePhotoUrls = new();

        private string PreviewPresenceReport = "";
        private List<PresenceTypeItem> PresenceTypes = new();
        int PreviewPresenceTypeId = 0;
        string PreviewPresenceTypeName = "";
        List<(string Label, string Field)> PreviewPresenceFields = new();

        private bool ShowWarningModal = false;
        private StudentDetail? StudentDetail;

        private async Task LoadPresenceTypes()
        {
            var types = await Http.GetFromJsonAsync<List<PresenceTypeItem>>(APIUrl.Endpoint("data/presence-types"));
            PresenceTypes = types ?? new();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (ShowDetailModal && SelectedPresenceDetail != null && (SelectedPresenceDetail.presence_type == "Hadir" || SelectedPresenceDetail.presence_type == "WFH"))
            {
                if (double.TryParse(SelectedPresenceDetail.lat, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
                    double.TryParse(SelectedPresenceDetail.longitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng))
                {
                    await JS.InvokeVoidAsync("renderPresenceMapWithMarker", "detailPresenceMap", lat, lng, null);
                }
            }
        }

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            if (!authState.User.Identity.IsAuthenticated)
            {
                Navigation.NavigateTo("/login", true);
                return;
            }

            var studentId = HistoryState.StudentId;
            if (string.IsNullOrWhiteSpace(studentId.ToString()))
            {
                ShowWarningModal = true;
                return;
            }

            var meJson = await JS.InvokeAsync<string>("localStorage.getItem", "meResponse");
            if (string.IsNullOrWhiteSpace(meJson))
                return;

            var doc = JsonDocument.Parse(meJson);
            var root = doc.RootElement;

            StudentDetail = await Http.GetFromJsonAsync<StudentDetail>(PKLPresenceWeb.Model.APIUrl.Endpoint($"students/{studentId}"));

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

            await LoadPresenceTypes();
            await LoadPresenceHistory();
            await LoadReportHistory();
            IsLoading = false;
        }

        private void CloseWarningModal()
        {
            ShowWarningModal = false;
            Navigation.NavigateTo("/participant", true);
        }

        private async Task LoadPresenceHistory()
        {
            IsLoading = true;
            StateHasChanged();
            var studentId = HistoryState.StudentId;
            var url = PKLPresenceWeb.Model.APIUrl.Endpoint($"presence/history?studentId={studentId}&page={PresencePage}");
            PresenceHistory = await Http.GetFromJsonAsync<PresenceHistoryResponse>(url);
            Presences = PresenceHistory?.data?
            .Select(MapToPresenceItem)
            .Where(p => !string.IsNullOrWhiteSpace(p.presence_type) && p.presence_type != "-")
            .ToList() ?? new List<PresenceItem>();

            IsLoading = false;
            StateHasChanged();
        }

        private async Task LoadReportHistory()
        {
            IsLoading = true;
            StateHasChanged();
            var studentId = HistoryState.StudentId;
            var url = PKLPresenceWeb.Model.APIUrl.Endpoint($"reports/history-reports?studentId={studentId}&page={ReportPage}");
            ReportHistory = await Http.GetFromJsonAsync<ReportHistoryResponse>(url);
            IsLoading = false;
            StateHasChanged();
        }

        private async void ChangePresencePage(int page)
        {
            if (page >= 1 && page <= TotalPagesPresence && page != PresencePage)
            {
                PresencePage = page;
                await LoadPresenceHistory();
            }
        }

        private async void ChangeReportPage(int page)
        {
            if (page >= 1 && page <= TotalPagesReport && page != ReportPage)
            {
                ReportPage = page;
                await LoadReportHistory();
            }
        }

        private void GoBack() => Navigation.NavigateTo("/participant");
        private void GoToProfile() => Navigation.NavigateTo("/home/profile");

        private async void ShowReportDetail(ReportHistoryItem item, string type)
        {
            string? id = null;
            if (type == "photo")
                id = item.reportPhotoId;
            else if (type == "file")
                id = item.reportFileId;

            if (!string.IsNullOrWhiteSpace(id))
            {
                var url = PKLPresenceWeb.Model.APIUrl.Endpoint($"reports/preview/{id}");
                await JS.InvokeVoidAsync("window.open", url, "_blank");
            }
            else
            {
                await AlertService.ShowErrorAsync("ID tidak tersedia.");
            }
        }

        void ShowPrevPresence()
        {
            var list = Presences.Where(p => !string.IsNullOrWhiteSpace(p.presence_type) && p.presence_type != "-").ToList();
            if (SelectedPresenceIndex > 0)
            {
                SelectedPresenceIndex--;
                SelectedPresenceDetail = list[SelectedPresenceIndex];
                _ = LoadPresencePhotosAndReport(SelectedPresenceDetail);
            }
        }

        void ShowNextPresence()
        {
            var list = Presences.Where(p => !string.IsNullOrWhiteSpace(p.presence_type) && p.presence_type != "-").ToList();
            if (SelectedPresenceIndex < list.Count - 1)
            {
                SelectedPresenceIndex++;
                SelectedPresenceDetail = list[SelectedPresenceIndex];
                _ = LoadPresencePhotosAndReport(SelectedPresenceDetail);
            }
        }

        readonly Dictionary<int, List<(string Label, string Field)>> PresenceTypeFields = new()
    {
        { 1, new() { ("Foto Full Body", "FullBodyPhoto"), ("Location", "Location") } }, // Hadir
        { 2, new() { ("Saat Berobat", "Treatment") } }, // Sakit
        { 3, new() { ("Izin ke Perusahaan", "PermitToCompany"), ("Izin ke Pembimbing Sekolah", "PermitToMentor"), ("Izin ke Walas", "PermitToWalas") } }, // Izin
        { 4, new() { ("Info Libur dari Perusahaan", "HolidayFromCompany") } }, // Libur
        { 5, new() { ("Foto Full Body", "FullBodyPhoto"), ("Info WFH dari Perusahaan", "WFHFromCompany"), ("Location", "Location") } }, // WFH
    };

        private string MapPhotoTypeToField(string type)
        {
            return type switch
            {
                "photoBody" => "FullBodyPhoto",
                "medicalCertificate" => "MedicalCertificate",
                "activity" => "Activity",
                "treatment" => "Treatment",
                "sickToCompany" => "SickToCompany",
                "sickToMentor" => "SickToMentor",
                "sickToWalas" => "SickToWalas",
                "permitToCompany" => "PermitToCompany",
                "permitToMentor" => "PermitToMentor",
                "permitToWalas" => "PermitToWalas",
                "holidayFromCompany" => "HolidayFromCompany",
                "wfhFromCompany" => "WFHFromCompany",
                _ => type
            };
        }

        private async Task<string?> GetPhotoBase64Async(string photoUrl)
        {
            try
            {
                var token = await JS.InvokeAsync<string>("localStorage.getItem", "authToken");
                var request = new HttpRequestMessage(HttpMethod.Get, photoUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var ext = System.IO.Path.GetExtension(photoUrl).ToLower();
                var mime = ext == ".png" ? "image/png" : "image/jpeg";
                return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }
            catch { return null; }
        }

        private async Task LoadPresencePhotosAndReport(PresenceItem item)
        {
            PreviewPresencePhotoUrls.Clear();
            PreviewPresenceReport = item.report ?? "";

            var type = PresenceTypes.FirstOrDefault(x =>
                x.name.Trim().Equals(item.presence_type.Trim(), StringComparison.OrdinalIgnoreCase));
            if (type == null)
            {
                await AlertService.ShowErrorAsync($"PresenceType master: {string.Join(", ", PresenceTypes.Select(x => x.name))}\nDari item: {item.presence_type}");
                return;
            }

            PreviewPresenceTypeId = type.id;
            PreviewPresenceTypeName = type.name;
            if (PreviewPresenceTypeId == 2) // Sakit
            {
                PreviewPresenceFields = new()
            {
                ("Saat Berobat", "Treatment"),
                ("Surat MC", "MedicalCertificate"),
                ("Info Sakit ke Perusahaan", "SickToCompany"),
                ("Info Sakit ke Pembimbing Sekolah", "SickToMentor"),
                ("Info Sakit ke Walas", "SickToWalas")
            };
            }
            else if (PreviewPresenceTypeId == 3) // Izin
            {
                PreviewPresenceFields = new()
            {
                ("Izin ke Perusahaan", "PermitToCompany"),
                ("Izin ke Pembimbing Sekolah", "PermitToMentor"),
                ("Izin ke Walas", "PermitToWalas"),
                ("Kegiatan yang Dilakukan", "Activity")
            };
            }
            else if (PreviewPresenceTypeId == 4) // Libur
            {
                PreviewPresenceFields = new()
            {
                ("Info Libur dari Perusahaan", "HolidayFromCompany")
            };
            }
            else if (PreviewPresenceTypeId == 1) // Hadir
            {
                PreviewPresenceFields = new()
            {
                ("Foto Full Body", "FullBodyPhoto")
            };
            }
            else if (PreviewPresenceTypeId == 5) // Hadir
            {
                PreviewPresenceFields = new()
            {
                ("Foto Full Body", "FullBodyPhoto"),
                ("Info WFH dari Perusahaan", "WFHFromCompany")
            };
            }
            else PreviewPresenceFields = PresenceTypeFields.GetValueOrDefault(PreviewPresenceTypeId, new());

            try
            {
                var photos = await Http.GetFromJsonAsync<List<PresencePhotoResponse>>(APIUrl.Endpoint($"presence/{item.id_presence}/photos"));
                if (photos != null)
                {
                    foreach (var field in PreviewPresenceFields)
                    {
                        var photo = photos.FirstOrDefault(p => MapPhotoTypeToField(p.type) == field.Field);
                        var apiBase = "https://presensi.smksabdev.my.id";
                        if (photo != null)
                        {
                            var base64 = await GetPhotoBase64Async($"{apiBase}{photo.url}");
                            PreviewPresencePhotoUrls[field.Field] = base64;
                        }
                        else PreviewPresencePhotoUrls[field.Field] = null;
                    }
                }
            }
            catch { await AlertService.ShowErrorAsync("Gagal mengambil foto presensi."); }
            StateHasChanged();
        }

        void CloseDetailModal()
        {
            ShowDetailModal = false;
            SelectedPresenceDetail = null;
            SelectedPresenceIndex = -1;
        }

        async void ShowDetail(PresenceItem item)
        {
            var list = Presences.Where(p => !string.IsNullOrWhiteSpace(p.presence_type) && p.presence_type != "-").ToList();
            SelectedPresenceIndex = list.FindIndex(p => p.id_presence == item.id_presence);
            SelectedPresenceDetail = item;
            ShowDetailModal = true;
            await LoadPresencePhotosAndReport(item);
        }

        private PresenceItem MapToPresenceItem(PresenceHistoryItem history)
        {
            return new PresenceItem
            {
                id_presence = history.id_presence, // sesuaikan jika nama property berbeda
                nis = history.nis,
                name = history.name,
                date = history.date,
                time = history.time,
                presence_type = history.presence_type,
                report = history.report,
                // tambahkan property lain jika diperlukan, atau set default/null
                classId = 0,
                classroom_name = "",
                isPresence = "",
                lat = history.lat,
                longitude = history.longitude,
                isComplete = ""
            };
        }
    }
}