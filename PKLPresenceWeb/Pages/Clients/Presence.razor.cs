using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using PKLPresenceWeb.Model;
using System.Net.Http.Json;
using System.Text.Json;

namespace PKLPresenceWeb.Pages.Clients
{
    public partial class Presence : ComponentBase
    {
        Dictionary<string, string?> PresencePhotoPreviews = new();
        private List<PresenceItem> AllPresences = new();
        private int SelectedPresenceIndex = -1;

        bool IsCompressing = false;
        private string UserRole = "";
        private int CurrentUserId = 0;
        private int CurrentClassId = 0;
        private int CurrentMentorId = 0;
        private List<PresenceItem> Presences = new();
        private List<ClassItem> ClassList = new();
        private string searchName = "";
        private int? selectedClassId;
        private bool isLoading = false;

        private int CurrentPage = 1;
        private int TotalPages = 1;

        private CancellationTokenSource? searchDebounceCts;

        // Date picker state
        private bool showDatePicker = false;
        private string selectedDate = DateTime.Today.ToString("yyyy-MM-dd");
        private string MinDate => DateTime.Today.AddYears(-1).ToString("yyyy-MM-dd"); // Optional: set min date
        private string MaxDate => DateTime.Today.ToString("yyyy-MM-dd");

        // State modal presence
        bool ShowPresenceModal = false;
        int SelectedPresenceTypeId = 0;
        string SelectedPresenceTypeName = "";
        Dictionary<string, IBrowserFile?> PresencePhotos = new();
        bool IsSubmittingPresence = false;

        string ModalTitle = "";
        bool ShowDetailModal = false;
        bool ShowFeedbackModal = false;
        PresenceItem? SelectedPresenceDetail = null;

        string FeedbackInput = "";
        bool IsSubmittingFeedback = false;

        // Untuk modal preview foto presence
        bool ShowPresencePhotoModal = false;
        int PreviewPresenceTypeId = 0;
        string PreviewPresenceTypeName = "";
        Dictionary<string, string?> PreviewPresencePhotoUrls = new();
        List<(string Label, string Field)> PreviewPresenceFields = new();
        bool IsLoadingPresencePhotos = false;

        string LocationInput = "";
        double CurrentLat = 0;
        double CurrentLng = 0;

        private bool IsLocationValid =>
        (SelectedPresenceTypeId != 1 && SelectedPresenceTypeId != 5) ||
        (CurrentLat != 0 && CurrentLng != 0);

        private bool ShowWebRTCModal = false;
        private string? WebRTCField = null;
        private string LastSuccessType = "";

        private string? ActiveWebRTCField = null;
        private DotNetObjectReference<Presence>? dotNetRef;

        private bool IsLockLocationOn = true;
        private string SliderColor => IsLockLocationOn ? "#14BD2D" : "#ccc";
        private string LockLocationSuccessMessage = "";

        private string PreviewPresenceReport = "";
        private bool IsReportFormValid =>
        ReportPresenceTypeId switch
        {
            1 => !string.IsNullOrWhiteSpace(ReportTextInput),
            2 => new[] { "MedicalCertificate", "SickToCompany", "SickToMentor", "SickToWalas" }
                    .All(f => ReportPhotos.ContainsKey(f) && ReportPhotos[f] != null),
            3 => ReportPhotos.ContainsKey("Activity") && ReportPhotos["Activity"] != null,
            5 => !string.IsNullOrWhiteSpace(ReportTextInput),
            _ => false
        };

        private string SuccessModalText = "";
        private string ErrorModalText = "";

        [JSInvokable]
        public async Task OnWebRTCCapture(string elementId, string dataUrl)
        {
            var field = elementId.Replace("webrtc_", "");
            var base64 = dataUrl.Substring(dataUrl.IndexOf(",") + 1);
            var bytes = Convert.FromBase64String(base64);
            var file = new BlazorInputFileStreamFile(field + ".jpg", "image/jpeg", bytes);

            if (IsReportWebRTC)
            {
                ReportPhotoPreviews[field] = dataUrl;
                ReportPhotos[field] = file;
            }
            else
            {
                PresencePhotoPreviews[field] = dataUrl;
                PresencePhotos[field] = file;
            }

            ShowWebRTCModal = false;
            WebRTCField = null;
            IsReportWebRTC = false;
            StateHasChanged();
        }

        // [JSInvokable]
        // public async Task OnWebRTCCapture(string elementId, string dataUrl)
        // {
        //     var field = elementId.Replace("webrtc_", "");

        //     var base64 = dataUrl.Substring(dataUrl.IndexOf(",") + 1);
        //     var originalBytes = Convert.FromBase64String(base64);

        //     Preview original (instan)
        //     if (IsReportWebRTC)
        //     {
        //         ReportPhotoPreviews[field] = dataUrl;
        //     }
        //     else
        //     {
        //         PresencePhotoPreviews[field] = dataUrl;
        //     }

        //     IsCompressing = true;
        //     2️⃣ Compress background
        //     _ = InvokeAsync(async () =>
        //     {
        //         var base64Original = Convert.ToBase64String(originalBytes);
        //         var compressed = await JS.InvokeAsync<string>(
        //             "photoCompressor.compressImage",
        //             base64Original,
        //             0.25
        //         );

        //         var compressedBase64 = compressed.Substring(compressed.IndexOf(",") + 1);
        //         var bytes = Convert.FromBase64String(compressedBase64);

        //         var file = new BlazorInputFileStreamFile($"{field}.jpg", "image/jpeg", bytes);

        //         if (IsReportWebRTC)
        //             ReportPhotos[field] = file;
        //         else
        //             PresencePhotos[field] = file;

        //         IsCompressing = false;
        //         StateHasChanged();
        //     });

        //     ShowWebRTCModal = false;
        //     WebRTCField = null;
        //     IsReportWebRTC = false;
        //     StateHasChanged();
        // }


        private bool IsReportWebRTC = false;
        private void OnPhotoBoxClick(string field, bool isReport = false)
        {
            WebRTCField = field;
            ShowWebRTCModal = true;
            IsReportWebRTC = isReport;
        }

        private bool IsWebRTCField(string field)
            => field == "FullBodyPhoto" || field == "Treatment" || field == "Activity";

        private async Task StartWebRTC()
        {
            if (WebRTCField != null)
            {
                dotNetRef ??= DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("webrtcPhoto.start", $"webrtc_{WebRTCField}", dotNetRef);
            }
        }

        private async void CloseWebRTCModal()
        {
            if (WebRTCField != null)
                await JS.InvokeVoidAsync("webrtcPhoto.stop", $"webrtc_{WebRTCField}");
            ShowWebRTCModal = false;
            WebRTCField = null;
            StateHasChanged();
        }

        private async void CaptureWebRTCPhoto()
        {
            if (WebRTCField != null)
                await JS.InvokeVoidAsync("webrtcPhoto.capture", $"webrtc_{WebRTCField}");
        }

        private Dictionary<string, InputFile> inputFileRefs = new();

        private InputFile GetInputFileRef(string field)
        {
            if (!inputFileRefs.ContainsKey(field))
                inputFileRefs[field] = null;
            return inputFileRefs[field];
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (ShowWebRTCModal && WebRTCField != null)
                await StartWebRTC();

            if ((ShowDetailModal || ShowFeedbackModal) && SelectedPresenceDetail != null && (SelectedPresenceDetail.presence_type == "Hadir" || SelectedPresenceDetail.presence_type == "WFH"))
            {
                if (double.TryParse(SelectedPresenceDetail.lat, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
                    double.TryParse(SelectedPresenceDetail.longitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng))
                {
                    await JS.InvokeVoidAsync("renderPresenceMapWithMarker", "detailPresenceMap", lat, lng, null);
                }
            }
        }

        private bool HasPresenceTodayGlobal = false;
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

        // Student presence page
        private List<PresenceTypeItem> PresenceTypes = new();
        private int pageSize = 10;
        private int PageSize
        {
            get => pageSize;
            set
            {
                if (pageSize != value)
                {
                    pageSize = value;
                    CurrentPage = 1;
                    // Pastikan filter tetap digunakan
                    if (UserRole == "Student") _ = LoadStudentPresences();
                    else _ = LoadPresence(selectedDate);
                }
            }
        }

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            if (!authState.User.Identity.IsAuthenticated)
                Navigation.NavigateTo("/login", true);

            var meJson = await JS.InvokeAsync<string>("localStorage.getItem", "meResponse");
            using var doc = JsonDocument.Parse(meJson);
            var root = doc.RootElement;

            UserRole = root.TryGetProperty("role", out var roleProp) ? roleProp.GetString() ?? "" : "";
            CurrentUserId = root.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;

            if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Object)
            {
                if (UserRole.Contains("Mentor") && dataProp.TryGetProperty("mentorId", out var mentorIdProp))
                    CurrentMentorId = mentorIdProp.GetInt32();
                if (UserRole.Contains("Wali Kelas") && dataProp.TryGetProperty("classId", out var classIdProp))
                    CurrentClassId = classIdProp.GetInt32();
            }

            // Ambil foto profile jika ada
            if (root.TryGetProperty("profile", out var profileProp) && profileProp.ValueKind != JsonValueKind.Null)
            {
                var photoResponse = await Http.GetAsync(APIUrl.Endpoint("me/photo"));
                if (photoResponse.IsSuccessStatusCode)
                {
                    var bytes = await photoResponse.Content.ReadAsByteArrayAsync();
                    var contentType = photoResponse.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    var base64 = Convert.ToBase64String(bytes);
                    photoUrl = $"data:{contentType};base64,{base64}";
                }
            }

            if (UserRole == "Student")
            {
                await LoadPresenceTypes();
                await LoadStudentPresences();

                // Cek presensi hari ini (ambil data presensi hari ini saja)
                var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
                var url = APIUrl.Endpoint($"presence?date={todayStr}");
                var response = await Http.GetFromJsonAsync<PresenceListResponse>(url);
                HasPresenceTodayGlobal = response?.data?.Any() == true;
            }
            else
            {
                await LoadClasses();
                await LoadPresenceTypes();
                await LoadPresence();
                await LoadMentors();
                await LoadLockLocationStatusAsync();
            }
        }

        private async Task LoadPresenceTypes()
        {
            var types = await Http.GetFromJsonAsync<List<PresenceTypeItem>>(APIUrl.Endpoint("data/presence-types"));
            PresenceTypes = types ?? new();
        }

        private async Task LoadStudentPresences()
        {
            var url = APIUrl.Endpoint($"presence?page={CurrentPage}&pageSize={PageSize}");
            var response = await Http.GetFromJsonAsync<PresenceListResponse>(url);
            if (response != null)
            {
                Presences = response.data ?? new();
                TotalPages = response.totalPages;
            }
            StateHasChanged();
        }

        private async void OnPresenceTypeChanged(int typeId)
        {
            SelectedPresenceTypeId = typeId;
            SelectedPresenceTypeName = PresenceTypes.FirstOrDefault(x => x.id == typeId)?.name ?? "";
            PresencePhotos.Clear();
            ShowPresenceModal = true;

            if (typeId == 1 || typeId == 5)
            {
                var position = await JS.InvokeAsync<GeolocationPosition>("getCurrentPosition");
                CurrentLat = position.coords.latitude;
                CurrentLng = position.coords.longitude;
                await JS.InvokeVoidAsync("renderPresenceMapWithMarker", "presenceMap", CurrentLat, CurrentLng, DotNetObjectReference.Create(this));
            }
            StateHasChanged();
        }

        [JSInvokable]
        public void UpdateLocationFromMap(double lat, double lng)
        {
            CurrentLat = lat;
            CurrentLng = lng;
            StateHasChanged();
        }

        async Task LoadClasses()
        {
            var response = await Http.GetFromJsonAsync<ClassListResponse>(APIUrl.Endpoint($"classes?page={CurrentPage}"));
            if (response != null) ClassList = response.classrooms ?? new();
        }

        async Task LoadPresence(string dateFilter = null)
        {
            isLoading = true;
            StateHasChanged();

            string url = APIUrl.Endpoint("presence");
            if (!string.IsNullOrWhiteSpace(searchName))
                url += $"?search={searchName}";
            if (selectedClassId.HasValue)
                url += (url.Contains("?") ? "&" : "?") + $"classId={selectedClassId}";
            if (!string.IsNullOrWhiteSpace(dateFilter ?? selectedDate))
                url += (url.Contains("?") ? "&" : "?") + $"date={dateFilter ?? selectedDate}";

            // Ambil semua data (tanpa paginasi)
            url += (url.Contains("?") ? "&" : "?") + "pageSize=1000";

            var response = await Http.GetFromJsonAsync<PresenceListResponse>(url);
            if (response != null)
            {
                AllPresences = response.data ?? new();
                TotalPages = (int)Math.Ceiling((double)AllPresences.Count / PageSize);
                Presences = GetSortedPresences()
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();
            }
            isLoading = false;
            StateHasChanged();
        }

        private async void OnFilterChanged(ChangeEventArgs e)
        {
            searchName = e.Value?.ToString() ?? "";
            searchDebounceCts?.Cancel();
            searchDebounceCts = new CancellationTokenSource();
            var token = searchDebounceCts.Token;
            try
            {
                await Task.Delay(300, token);
                if (!token.IsCancellationRequested)
                {
                    CurrentPage = 1;
                    await LoadPresence(selectedDate); // pastikan date tetap dikirim
                }
            }
            catch (TaskCanceledException) { }
        }

        private async void OnClassChange(ChangeEventArgs e)
        {
            selectedClassId = int.TryParse(e.Value?.ToString(), out var id) ? id : null;
            CurrentPage = 1;
            await LoadPresence(selectedDate); // pastikan date tetap dikirim
        }

        void GoBack() => Navigation.NavigateTo("/home");

        async void ShowDetail(PresenceItem item)
        {
            var list = Presences.Where(p => !string.IsNullOrWhiteSpace(p.presence_type) && p.presence_type != "-").ToList();
            SelectedPresenceIndex = list.FindIndex(p => p.id_presence == item.id_presence);
            SelectedPresenceDetail = item;
            ModalTitle = "Presence Detail";
            ShowDetailModal = true;
            ShowFeedbackModal = false;
            await LoadPresencePhotosAndReport(item);
        }

        void CloseDetailModal()
        {
            ShowDetailModal = false;
            ShowFeedbackModal = false;
            SelectedPresenceDetail = null;
            FeedbackInput = "";
            IsSubmittingFeedback = false;
            SelectedPresenceIndex = -1;
        }

        async void ShowFeedback(PresenceItem item)
        {
            var list = Presences.Where(p => !string.IsNullOrWhiteSpace(p.presence_type) && p.presence_type != "-").ToList();
            SelectedPresenceIndex = list.FindIndex(p => p.id_presence == item.id_presence);
            SelectedPresenceDetail = item;
            ModalTitle = "Give Feedback";
            ShowFeedbackModal = true;
            ShowDetailModal = false;
            FeedbackInput = "";
            await LoadPresencePhotosAndReport(item);
        }

        private async Task ChangePage(int page)
        {
            if (page >= 1 && page <= TotalPages && page != CurrentPage)
            {
                CurrentPage = page;
                if (UserRole == "Student") await LoadStudentPresences();
                else
                {
                    Presences = GetSortedPresences()
                        .Skip((CurrentPage - 1) * PageSize)
                        .Take(PageSize)
                        .ToList();
                    StateHasChanged();
                }
            }
        }

        async Task OnDateSelected(ChangeEventArgs e)
        {
            var dateValue = e.Value?.ToString();
            if (string.IsNullOrWhiteSpace(dateValue))
                return;

            showDatePicker = false;
            selectedDate = dateValue;
            CurrentPage = 1;
            await LoadPresence(selectedDate);
        }

        void TriggerFileInput(string field)
        {
            JS.InvokeVoidAsync("triggerFileInput", $"fileInput_{field}");
        }

        async Task OnPhotoChanged(InputFileChangeEventArgs e, string field)
        {
            var file = e.File;
            if (file != null)
            {
                PresencePhotos[field] = file;

                // 1️⃣ Baca original untuk preview instan
                using var stream = file.OpenReadStream(long.MaxValue);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var originalBytes = ms.ToArray();

                // Preview langsung original (tanpa delay)
                PresencePhotoPreviews[field] = $"data:{file.ContentType};base64,{Convert.ToBase64String(originalBytes)}";

                IsCompressing = true;
                // 2️⃣ KOMPres berjalan di background (tidak mengganggu UI)
                _ = InvokeAsync(async () =>
                {
                    var base64Original = Convert.ToBase64String(originalBytes);

                    var compressed = await JS.InvokeAsync<string>(
                        "photoCompressor.compressImageBase64",
                        base64Original,
                        0.25
                    );

                    var base64 = compressed.Substring(compressed.IndexOf(",") + 1);
                    var bytes = Convert.FromBase64String(base64);

                    PresencePhotos[field] = new BlazorInputFileStreamFile($"{field}.jpg", "image/jpeg", bytes);
                    IsCompressing = false;
                });
            }
            else PresencePhotoPreviews[field] = null;

            if (inputFileRefs.TryGetValue(field, out var inputRef) && inputRef != null)
                await JS.InvokeVoidAsync("resetInputFile", $"fileInput_{field}");

            StateHasChanged();
        }


        void CancelPresenceModal()
        {
            ShowPresenceModal = false;
            PresencePhotos.Clear();
            PresencePhotoPreviews.Clear();
            SelectedPresenceTypeId = 0;
            SelectedPresenceTypeName = "";

            if (ActiveWebRTCField != null)
            {
                JS.InvokeVoidAsync("webrtcPhoto.stop", $"webrtc_{ActiveWebRTCField}");
                ActiveWebRTCField = null;
            }
        }

        string PresenceErrorMessage = "";
        async Task ConfirmPresenceModal()
        {
            try
            {
                if (IsCompressing)
                {
                    await AlertService.ShowInfoAsync("Tunggu sebentar, sedang memproses foto...");
                    return;
                }

                IsSubmittingPresence = true;
                PresenceErrorMessage = "";
                var content = new MultipartFormDataContent();

                // Tambahkan foto sesuai field
                foreach (var field in PresenceTypeFields.GetValueOrDefault(SelectedPresenceTypeId, new()))
                {
                    if (PresencePhotos.TryGetValue(field.Field, out var file) && file != null)
                    {
                        var stream = file.OpenReadStream(long.MaxValue);
                        var fileContent = new StreamContent(stream);
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                        content.Add(fileContent, field.Field, file.Name);
                    }
                    else content.Add(new StringContent(""), field.Field);
                }

                // Parse koordinat jika presensi "Hadir"
                if ((SelectedPresenceTypeId == 1 || SelectedPresenceTypeId == 5) && !string.IsNullOrWhiteSpace(LocationInput))
                {
                    var parts = LocationInput.Split(',');
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
                        double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng))
                    {
                        CurrentLat = lat;
                        CurrentLng = lng;
                    }
                    else
                    {
                        await AlertService.ShowErrorAsync("Format lokasi salah. Contoh: 1.040000,103.990000");
                        IsSubmittingPresence = false;
                        return;
                    }
                }

                content.Add(new StringContent(SelectedPresenceTypeId.ToString()), "PresenceTypeid");
                if (SelectedPresenceTypeId == 1 || SelectedPresenceTypeId == 5)
                {
                    content.Add(new StringContent(CurrentLng.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Long");
                    content.Add(new StringContent(CurrentLat.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Lat");
                }

                ShowPresenceModal = false;
                var response = await Http.PostAsync(APIUrl.Endpoint("presence"), content);
                IsSubmittingPresence = false;

                if (response.IsSuccessStatusCode)
                {
                    LastSuccessType = "presence";
                    PresencePhotos.Clear();
                    SelectedPresenceTypeId = 0;
                    SelectedPresenceTypeName = "";
                    LocationInput = "";
                    SuccessModalText = LastSuccessType == "report" ? "Daily Report berhasil disubmit!" : "Presensi berhasil dilakukan!";
                    await AlertService.ShowSuccessAsync(SuccessModalText);
                    await Task.Delay(3000);
                    Navigation.NavigateTo(Navigation.Uri, forceLoad: true);
                }
                else
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        if (errorText.Contains("Ukuran file terlalu besar", StringComparison.OrdinalIgnoreCase))
                            PresenceErrorMessage = "Ukuran file terlalu besar. Maksimal ukuran file hanya 5 MB.";

                        else if (errorText.Contains("terlalu jauh", StringComparison.OrdinalIgnoreCase))
                            PresenceErrorMessage = "Anda berada terlalu jauh dari tempat PKL!";

                        else PresenceErrorMessage = "Gagal submit presensi.";

                        ErrorModalText = PresenceErrorMessage;
                        PresencePhotos.Clear();
                        SelectedPresenceTypeId = 0;
                        SelectedPresenceTypeName = "";
                        LocationInput = "";
                        await AlertService.ShowErrorAsync(ErrorModalText);
                        StateHasChanged();
                    }
                    else await AlertService.ShowErrorAsync("Gagal submit presensi.");
                }
            }
            catch
            {
                IsSubmittingPresence = false;
                await AlertService.ShowErrorAsync("Terjadi kesalahan saat submit presensi.");
            }
        }

        private bool IsPresenceFormValid =>
        PresenceTypeFields.TryGetValue(SelectedPresenceTypeId, out var fields) &&
        fields
            .Where(f => f.Field != "Location")
            .All(f => PresencePhotos.ContainsKey(f.Field) && PresencePhotos[f.Field] != null);

        async Task OnFeedbackButtonClick()
        {
            if (string.IsNullOrWhiteSpace(FeedbackInput))
            {
                CloseDetailModal();
                return;
            }

            if (SelectedPresenceDetail == null)
                return;

            IsSubmittingFeedback = true;
            var feedbackObj = new { feedback = FeedbackInput };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(feedbackObj),
                System.Text.Encoding.UTF8,
                "application/json"
            );
            var response = await Http.PutAsync(APIUrl.Endpoint($"presence/feedback/{SelectedPresenceDetail.id_presence}"), content);
            IsSubmittingFeedback = false;

            if (response.IsSuccessStatusCode)
            {
                CloseDetailModal();
                await AlertService.ShowSuccessAsync("Feedback berhasil ditambahkan.");
                await LoadPresence();
            }
            else await AlertService.ShowErrorAsync("Gagal submit feedback.");
        }

        private int GetPresenceTypeId(PresenceItem item)
        {
            var type = PresenceTypes.FirstOrDefault(x => x.name.Trim().Equals(item.presence_type.Trim(), StringComparison.OrdinalIgnoreCase));
            return type?.id ?? 0;
        }

        bool ShowReportModalFlag = false;
        int ReportPresenceId = 0;
        int ReportPresenceTypeId = 0;
        string ReportPresenceTypeName = "";
        string ReportTextInput = "";
        Dictionary<string, IBrowserFile?> ReportPhotos = new();
        Dictionary<string, string?> ReportPhotoPreviews = new();
        string ReportActivityInput = "";
        private string ReportModalMode = "add";

        private void ShowReportModal(PresenceItem item)
        {
            ReportPresenceId = int.TryParse(item.id_presence, out var id) ? id : 0;
            ReportPresenceTypeId = GetPresenceTypeId(item);
            ReportPresenceTypeName = PresenceTypes.FirstOrDefault(x => x.id == ReportPresenceTypeId)?.name ?? "";
            ReportPhotos.Clear();
            ReportPhotoPreviews.Clear();

            // Tentukan mode: add atau edit
            if (!string.IsNullOrWhiteSpace(item.report) && item.report != "-" && (ReportPresenceTypeId == 1 || ReportPresenceTypeId == 5))
            {
                ReportModalMode = "edit";
                ReportTextInput = item.report ?? "";
            }
            else if (ReportPresenceTypeId == 2 || ReportPresenceTypeId == 3)
            {
                ReportModalMode = "edit";
                _ = LoadReportPhotoPreviewsAsync(item);
            }
            else
            {
                ReportModalMode = "add";
                ReportTextInput = "";
            }

            ReportActivityInput = "";
            ShowReportModalFlag = true;
        }

        private async Task OnReportPhotoChanged(InputFileChangeEventArgs e, string field)
        {
            var file = e.File;
            if (file != null)
            {
                ReportPhotos[field] = file;

                using var stream = file.OpenReadStream(long.MaxValue);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var originalBytes = ms.ToArray();

                // Preview original (instan)
                ReportPhotoPreviews[field] = $"data:{file.ContentType};base64,{Convert.ToBase64String(originalBytes)}";

                IsCompressing = true;
                // KOMpres background
                _ = InvokeAsync(async () =>
                {
                    var base64Original = Convert.ToBase64String(originalBytes);

                    var compressed = await JS.InvokeAsync<string>(
                        "photoCompressor.compressImageBase64",
                        base64Original,
                        0.25
                    );

                    var base64 = compressed.Substring(compressed.IndexOf(",") + 1);
                    var bytes = Convert.FromBase64String(base64);

                    ReportPhotos[field] = new BlazorInputFileStreamFile($"{field}.jpg", "image/jpeg", bytes);
                    IsCompressing = false;
                });
            }
            else ReportPhotoPreviews[field] = null;
            StateHasChanged();
        }

        private async Task LoadReportPhotoPreviewsAsync(PresenceItem item)
        {
            try
            {
                var photos = await Http.GetFromJsonAsync<List<PresencePhotoResponse>>(APIUrl.Endpoint($"presence/{item.id_presence}/photos"));
                if (photos != null)
                {
                    foreach (var photo in photos)
                    {
                        var field = MapPhotoTypeToField(photo.type);
                        var apiBase = "https://presensi.smksabdev.my.id";
                        var base64 = await GetPhotoBase64Async($"{apiBase}{photo.url}");
                        if (!string.IsNullOrEmpty(field) && !string.IsNullOrEmpty(base64))
                            ReportPhotoPreviews[field] = base64;
                    }
                }
            }
            catch { }
            StateHasChanged();
        }

        private void CancelReportModal()
        {
            ShowReportModalFlag = false;
            ReportPresenceId = 0;
            ReportPresenceTypeId = 0;
            ReportTextInput = "";
            ReportActivityInput = "";
            ReportPhotos.Clear();
            ReportPhotoPreviews.Clear();
            ReportModalMode = "add";
        }

        private async Task ConfirmReportModal()
        {
            try
            {
                if (IsCompressing)
                {
                    await AlertService.ShowInfoAsync("Tunggu sebentar, sedang memproses foto...");
                    return;
                }

                var content = new MultipartFormDataContent();
                if (ReportPresenceTypeId == 1 || ReportPresenceTypeId == 5)
                    content.Add(new StringContent(ReportTextInput ?? ""), "daily_report");

                else if (ReportPresenceTypeId == 2)
                {
                    foreach (var field in new[] { "MedicalCertificate", "SickToCompany", "SickToMentor", "SickToWalas" })
                    {
                        if (ReportPhotos.TryGetValue(field, out var file) && file != null)
                        {
                            var stream = file.OpenReadStream(long.MaxValue);
                            var fileContent = new StreamContent(stream);
                            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                            content.Add(fileContent, field, file.Name);
                        }
                        else content.Add(new StringContent(""), field);
                    }
                }
                else if (ReportPresenceTypeId == 3)
                {
                    if (ReportPhotos.TryGetValue("Activity", out var file) && file != null)
                    {
                        var stream = file.OpenReadStream(long.MaxValue);
                        var fileContent = new StreamContent(stream);
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                        content.Add(fileContent, "Activity", file.Name);
                    }
                    else content.Add(new StringContent(""), "Activity");
                }

                ShowReportModalFlag = false;
                var response = await Http.PutAsync(APIUrl.Endpoint($"presence/{ReportPresenceId}/edit"), content);
                if (response.IsSuccessStatusCode)
                {
                    LastSuccessType = "report";
                    CancelReportModal();
                    SuccessModalText = LastSuccessType == "report" ? "Daily Report berhasil disubmit!" : "Presensi berhasil dilakukan!";
                    await AlertService.ShowSuccessAsync(SuccessModalText);
                    await Task.Delay(3000);
                    Navigation.NavigateTo(Navigation.Uri, forceLoad: true);
                }
                else
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest &&
                        errorText.Contains("Ukuran file terlalu besar", StringComparison.OrdinalIgnoreCase))
                    {
                        await AlertService.ShowErrorAsync("Ukuran file terlalu besar. Maksimal ukuran file hanya 5 MB.");
                        StateHasChanged();
                    }
                    else await AlertService.ShowErrorAsync("Gagal submit report.");

                }
            }
            catch
            {
                await AlertService.ShowErrorAsync("Terjadi kesalahan saat submit report.");
            }
        }

        private void OnContentInput(ChangeEventArgs e)
        {
            var value = e.Value?.ToString() ?? string.Empty;
            if (value.Length > 200)
                value = value.Substring(0, 200);
            ReportTextInput = value;
        }

        private void GoToProfile() => Navigation.NavigateTo("/home/profile");
        private string photoUrl = "/images/default_profile.jpg";

        private string SortColumn = "";
        private bool SortAscending = true;

        private void SortBy(string column)
        {
            if (SortColumn == column) SortAscending = !SortAscending;
            else
            {
                SortColumn = column;
                SortAscending = true;
            }
            Presences = GetSortedPresences()
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();
            StateHasChanged();
        }

        private IEnumerable<PresenceItem> GetSortedPresences()
        {
            IEnumerable<PresenceItem> query = AllPresences;

            switch (SortColumn)
            {
                case nameof(PresenceItem.nis):
                    query = SortAscending
                        ? query.OrderBy(x => x.nis)
                        : query.OrderByDescending(x => x.nis);
                    break;
                case nameof(PresenceItem.name):
                    query = SortAscending
                        ? query.OrderBy(x => x.name)
                        : query.OrderByDescending(x => x.name);
                    break;
                case nameof(PresenceItem.classroom_name):
                    query = SortAscending
                        ? query.OrderBy(x => x.classroom_name)
                        : query.OrderByDescending(x => x.classroom_name);
                    break;
                case "Today":
                    query = SortAscending
                        ? query.OrderBy(x => x.isPresence == "✔️" ? 0 : 1)
                        : query.OrderByDescending(x => x.isPresence == "✔️" ? 0 : 1);
                    break;
                case nameof(PresenceItem.time):
                    query = SortAscending
                        ? query.OrderBy(x => x.time)
                        : query.OrderByDescending(x => x.time);
                    break;
                case nameof(PresenceItem.presence_type):
                    int GetStatusOrder(string status) => status switch
                    {
                        "Hadir" => 0,
                        "Sakit" => 1,
                        "Izin" => 2,
                        "Libur" => 3,
                        "WFH" => 4,
                        _ => 5
                    };
                    query = SortAscending
                        ? query.OrderBy(x => GetStatusOrder(x.presence_type))
                        : query.OrderByDescending(x => GetStatusOrder(x.presence_type));
                    break;
                case "Report":
                    query = SortAscending
                        ? query.OrderBy(x => x.isComplete == "✔️" ? 0 : 1)
                        : query.OrderByDescending(x => x.isComplete == "✔️" ? 0 : 1);
                    break;
            }
            return query;
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

        async Task SetTimer(int seconds)
        {
            await JS.InvokeVoidAsync("window.webrtcPhoto.SetTimer", $"webrtc_{WebRTCField}", seconds);
        }

        async Task SwitchCamera()
        {
            await JS.InvokeVoidAsync("window.webrtcPhoto.switchCamera", $"webrtc_{WebRTCField}");
        }

        private int WebRTCCaptureCountdown = 0;
        private bool IsWebRTCCaptureCounting = false;

        [JSInvokable]
        public void UpdateWebRTCCaptureCountdown(int value)
        {
            WebRTCCaptureCountdown = value;
            IsWebRTCCaptureCounting = value > 0;
            InvokeAsync(StateHasChanged);
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
            else if (PreviewPresenceTypeId == 5) // WFH
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
            catch
            {
                await AlertService.ShowErrorAsync("Gagal mengambil foto presensi.");
            }
            StateHasChanged();
        }

        private async Task OnLockLocationSwitchChanged(ChangeEventArgs e)
        {
            var newStatus = (bool)e.Value;
            var response = await Http.PutAsJsonAsync(APIUrl.Endpoint("assign/status-lock-location"), newStatus ? 1 : 0);
            if (response.IsSuccessStatusCode)
            {
                IsLockLocationOn = newStatus;
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                SuccessModalText = LockLocationSuccessMessage = doc.RootElement.GetProperty("message").GetString() ?? "";
                await AlertService.ShowSuccessAsync(SuccessModalText);
                StateHasChanged();
            }
            else await AlertService.ShowErrorAsync("Gagal mengubah status Lock Location.");
        }

        // State untuk delete Presensi
        private bool ShowDeletePresenceModal = false;
        private string? DeletePresenceId = null;
        private string DeletePresenceName = "";
        private string DeletePresenceDate = "";

        private void ShowDeletePresenceConfirmation(PresenceItem item)
        {
            DeletePresenceId = item.id_presence;
            DeletePresenceName = item.name;
            DeletePresenceDate = FormatTanggalIndo(item.date);
            ShowDeletePresenceModal = true;
        }

        private string FormatTanggalIndo(string tanggal)
        {
            if (DateTime.TryParse(tanggal, out var dt))
            {
                var bulan = dt.ToString("MMMM", new System.Globalization.CultureInfo("id-ID"));
                return $"{dt.Day} {bulan} {dt.Year}";
            }
            return tanggal;
        }

        private async Task ConfirmDeletePresenceAsync()
        {
            if (DeletePresenceId == null) return;
            var response = await Http.DeleteAsync(APIUrl.Endpoint($"presence/delete/{DeletePresenceId}"));
            ShowDeletePresenceModal = false;
            DeletePresenceId = null;
            DeletePresenceName = "";
            DeletePresenceDate = "";

            if (response.IsSuccessStatusCode)
            {
                await AlertService.ShowSuccessAsync("Data presensi siswa berhasil dihapus.");
                await LoadPresence(selectedDate);
            }
            else await AlertService.ShowErrorAsync("Gagal menghapus presensi.");
        }

        private void CancelDeletePresence()
        {
            ShowDeletePresenceModal = false;
            DeletePresenceId = null;
            DeletePresenceName = "";
            DeletePresenceDate = "";
        }

        // --- Modal Print Per Mentor ---
        private bool ShowPrintMentorModal = false;
        private List<MentorItem> MentorList = new();
        private int? SelectedMentorId = null;
        private string PrintStartDate = "";
        private string PrintEndDate = "";
        private bool CanPrint =>
            (SelectedMentorId.HasValue || SelectedClassPrintId.HasValue || CurrentUserId != 0) &&
            !string.IsNullOrWhiteSpace(PrintStartDate) &&
            !string.IsNullOrWhiteSpace(PrintEndDate) &&
            DateTime.TryParse(PrintStartDate, out var start) &&
            DateTime.TryParse(PrintEndDate, out var end) &&
            end >= start &&
            end <= DateTime.Today;

        private void PrintPerMentor()
        {
            ShowPrintMentorModal = true;
            SelectedMentorId = null;
            PrintStartDate = "";
            PrintEndDate = "";
        }

        private async Task LoadMentors()
        {
            var mentors = await Http.GetFromJsonAsync<MentorListResponse>(APIUrl.Endpoint($"mentors?page={CurrentPage}"));
            if (mentors != null) MentorList = mentors.data ?? new();
        }

        private void OnMentorSelected(ChangeEventArgs e)
        {
            SelectedMentorId = int.TryParse(e.Value?.ToString(), out var id) ? id : null;
        }

        private void OnPrintMentorStartDateChanged(ChangeEventArgs e)
        {
            var value = e.Value?.ToString() ?? "";
            if (DateTime.TryParse(value, out var dt))
            {
                PrintStartDate = value;
                if (!string.IsNullOrWhiteSpace(PrintEndDate) && DateTime.Parse(PrintEndDate) < dt)
                    PrintEndDate = "";
            }
        }

        private void OnPrintMentorEndDateChanged(ChangeEventArgs e)
        {
            var value = e.Value?.ToString() ?? "";
            if (DateTime.TryParse(value, out var dt))
                PrintEndDate = value;
        }

        private void CancelPrintMentorModal()
        {
            ShowPrintMentorModal = false;
            SelectedMentorId = null;
            PrintStartDate = "";
            PrintEndDate = "";
        }

        private async void PrintMentor()
        {
            if (!CanPrint || !SelectedMentorId.HasValue) return;
            var url = APIUrl.Endpoint($"presence/mentor/{SelectedMentorId}/print?startDate={PrintStartDate}&endDate={PrintEndDate}");
            var response = await Http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = FileHelper.GetFileNameFromContentDisposition(response);
                await JS.InvokeVoidAsync("downloadFileFromBytes", fileName, "application/pdf", bytes);

                CancelPrintMentorModal();
                await AlertService.ShowSuccessAsync("Rekap data Presensi berhasil diunduh.");
                StateHasChanged();
            }
        }

        // --- Modal Print Per Class ---
        private bool ShowPrintClassModal = false;
        private int? SelectedClassPrintId = null;
        private bool ShowPrintCombinedModal = false;
        private void PrintPerClass()
        {
            ShowPrintClassModal = true;
            SelectedClassPrintId = null;
            PrintStartDate = "";
            PrintEndDate = "";
        }

        private void OnClassPrintSelected(ChangeEventArgs e)
        {
            SelectedClassPrintId = int.TryParse(e.Value?.ToString(), out var id) ? id : null;
        }

        private void OnPrintClassStartDateChanged(ChangeEventArgs e)
        {
            var value = e.Value?.ToString() ?? "";
            if (DateTime.TryParse(value, out var dt))
            {
                PrintStartDate = value;
                if (!string.IsNullOrWhiteSpace(PrintEndDate) && DateTime.Parse(PrintEndDate) < dt)
                    PrintEndDate = "";
            }
        }

        private void OnPrintClassEndDateChanged(ChangeEventArgs e)
        {
            var value = e.Value?.ToString() ?? "";
            if (DateTime.TryParse(value, out var dt))
                PrintEndDate = value;
        }

        private void CancelPrintClassModal()
        {
            ShowPrintClassModal = false;
            SelectedClassPrintId = null;
            PrintStartDate = "";
            PrintEndDate = "";
        }

        private async void PrintClass()
        {
            if (!CanPrint || !SelectedClassPrintId.HasValue) return;
            var url = APIUrl.Endpoint($"presence/class/{SelectedClassPrintId}/print?startDate={PrintStartDate}&endDate={PrintEndDate}");
            var response = await Http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = FileHelper.GetFileNameFromContentDisposition(response);
                await JS.InvokeVoidAsync("downloadFileFromBytes", fileName, "application/pdf", bytes);

                CancelPrintClassModal();
                await AlertService.ShowSuccessAsync("Rekap data Presensi berhasil diunduh.");
                StateHasChanged();
            }
        }

        private void Print()
        {
            ShowPrintCombinedModal = true;
            PrintStartDate = "";
            PrintEndDate = "";
        }

        private void OnPrintCombinedStartDateChanged(ChangeEventArgs e)
        {
            var value = e.Value?.ToString() ?? "";
            if (DateTime.TryParse(value, out var dt))
            {
                PrintStartDate = value;
                if (!string.IsNullOrWhiteSpace(PrintEndDate) && DateTime.Parse(PrintEndDate) < dt)
                    PrintEndDate = "";
            }
        }

        private void OnPrintCombinedEndDateChanged(ChangeEventArgs e)
        {
            var value = e.Value?.ToString() ?? "";
            if (DateTime.TryParse(value, out var dt))
                PrintEndDate = value;
        }

        private void CancelPrintCombinedModal()
        {
            ShowPrintCombinedModal = false;
            PrintStartDate = "";
            PrintEndDate = "";
        }

        private async void ConfirmPrintCombinedAsync()
        {
            if (!CanPrint || CurrentUserId == 0) return;
            string url = "";

            bool isMentor = UserRole.Contains("Mentor");
            bool isWalas = UserRole.Contains("Wali Kelas");

            if (isMentor && isWalas)
                url = APIUrl.Endpoint($"presence/combined/{CurrentUserId}/print?startDate={PrintStartDate}&endDate={PrintEndDate}");
            else if (isMentor)
                url = APIUrl.Endpoint($"presence/mentor/{CurrentMentorId}/print?startDate={PrintStartDate}&endDate={PrintEndDate}");
            else if (isWalas)
                url = APIUrl.Endpoint($"presence/class/{CurrentClassId}/print?startDate={PrintStartDate}&endDate={PrintEndDate}");

            var response = await Http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = FileHelper.GetFileNameFromContentDisposition(response);
                await JS.InvokeVoidAsync("downloadFileFromBytes", fileName, "application/pdf", bytes);

                CancelPrintCombinedModal();
                await AlertService.ShowSuccessAsync("Rekap data Presensi berhasil diunduh.");
                StateHasChanged();
            }
        }

        private async void PrintStudentPresence(PresenceItem item)
        {
            var studentId = item.studentId;
            var date = item.date;

            if (DateTime.TryParse(date, out var dt))
                date = dt.ToString("yyyy-MM-dd");

            var url = APIUrl.Endpoint($"presence/byStudent/{studentId}/print?date={date}");
            var response = await Http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = FileHelper.GetFileNameFromContentDisposition(response);
                await JS.InvokeVoidAsync("downloadFileFromBytes", fileName, "application/pdf", bytes);

                await AlertService.ShowSuccessAsync("Rekap data Presensi berhasil diunduh.");
                StateHasChanged();
            }
            else await AlertService.ShowErrorAsync("Gagal mengunduh file presensi.");
        }

        private async Task LoadLockLocationStatusAsync()
        {
            try
            {
                var response = await Http.GetAsync(APIUrl.Endpoint("assign/status-lock-location"));
                if (response.IsSuccessStatusCode)
                {
                    var text = await response.Content.ReadAsStringAsync();
                    IsLockLocationOn = text.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                IsLockLocationOn = false;
            }
        }

        private bool IsTodaySelected =>
        DateTime.TryParse(selectedDate, out var selDate) &&
        selDate.Date == DateTime.Today;

        private async void PrevDate()
        {
            if (DateTime.TryParse(selectedDate, out var selDate))
            {
                var prev = selDate.AddDays(-1);
                if (prev >= DateTime.Parse(MinDate))
                {
                    selectedDate = prev.ToString("yyyy-MM-dd");
                    CurrentPage = 1;
                    await LoadPresence(selectedDate);
                    StateHasChanged();
                }
            }
        }

        private async void NextDate()
        {
            if (DateTime.TryParse(selectedDate, out var selDate))
            {
                var next = selDate.AddDays(1);
                if (next <= DateTime.Today)
                {
                    selectedDate = next.ToString("yyyy-MM-dd");
                    CurrentPage = 1;
                    await LoadPresence(selectedDate);
                    StateHasChanged();
                }
            }
        }

        private bool IsToday(string date)
        {
            if (DateTime.TryParse(date, out var dt))
                return dt.Date == DateTime.Today;
            return false;
        }
    }
}