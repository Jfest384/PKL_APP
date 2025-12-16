using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using PKLPresenceWeb.Model;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace PKLPresenceWeb.Pages.Clients
{
    public partial class InternReport : ComponentBase
    {
        private string UserRole = "";
        private int CurrentUserId = 0;
        private int CurrentClassId = 0;
        private int CurrentMentorId = 0;
        private List<ReportItem> Reports = new();
        private List<ReportItem> AllReports = new();
        private List<ClassItem> ClassList = new();
        private string searchName = "";
        private int? selectedClassId;
        private bool isLoading = false;

        private int CurrentPage = 1;
        private int TotalPages = 1;

        private CancellationTokenSource? searchDebounceCts;
        private bool showDatePicker = false;
        private string selectedDate = DateTime.Today.ToString("yyyy-MM-dd");
        private string MinDate => DateTime.Today.AddYears(-1).ToString("yyyy-MM-dd");
        private string MaxDate => DateTime.Today.ToString("yyyy-MM-dd");

        bool ShowReportModal = false;
        int SelectedReportTypeId = 0;
        string SelectedReportTypeName = "";
        Dictionary<string, IBrowserFile?> ReportPhotos = new();
        bool IsSubmittingReport = false;

        string ModalTitle = "";
        bool ShowFeedbackModal = false;
        ReportItem? SelectedReportDetail = null;

        string FeedbackInput = "";
        string ContentInput = "";
        bool IsSubmittingFeedback = false;
        bool IsSubmittingPresence = false;

        private string SuccessModalText = "";
        private bool HasReportToday =>
        Reports.Any(p =>
            DateTime.TryParseExact(
                p.date,
                "dddd, dd MMMM yyyy",
                new CultureInfo("id-ID"),
                DateTimeStyles.None,
                out var dt
            ) && dt.Date == DateTime.Today
        );

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
                    if (UserRole == "Student") _ = LoadStudentReports();
                    else _ = LoadReports(selectedDate);
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

            if (UserRole != "Student")
            {
                await LoadClasses();
                await LoadReports();
                await LoadMentors();
            }
            else await LoadStudentReports();
        }

        private void ShowAddReport()
        {
            ShowReportModal = true;
            StateHasChanged();
        }

        async Task LoadClasses()
        {
            var response = await Http.GetFromJsonAsync<ClassListResponse>(APIUrl.Endpoint($"classes?page={CurrentPage}"));
            if (response != null) ClassList = response.classrooms ?? new();
        }

        void GoBack() => Navigation.NavigateTo("/home");

        void CloseDetailModal()
        {
            ShowFeedbackModal = false;
            ShowReportModal = false;
            SelectedReportDetail = null;
            FeedbackInput = "";
            IsSubmittingFeedback = false;
        }

        void ShowFeedback(ReportItem item)
        {
            SelectedReportDetail = item;
            ShowReportDetailModal = true;
            ShowFeedbackModal = true;
            FeedbackInput = "";
        }

        void ShowReportDetail(ReportItem item)
        {
            SelectedReportDetail = item;
            ShowReportDetailModal = true;
            ShowFeedbackModal = false;
        }

        void CloseReportDetailModal()
        {
            ShowReportDetailModal = false;
            ShowFeedbackModal = false;
            SelectedReportDetail = null;
            FeedbackInput = "";
            IsSubmittingFeedback = false;
        }

        private async Task ChangePage(int page)
        {
            if (page >= 1 && page <= TotalPages && page != CurrentPage)
            {
                CurrentPage = page;
                if (UserRole == "Student") await LoadStudentReports();
                else
                {
                    Reports = GetSortedReports()
                        .Skip((CurrentPage - 1) * PageSize)
                        .Take(PageSize)
                        .ToList();
                    StateHasChanged();
                }
            }
        }

        void TriggerFileInput(string field)
        {
            JS.InvokeVoidAsync("triggerFileInput", $"fileInput_{field}");
        }

        void CancelReport()
        {
            ShowReportModal = false;
            ContentInput = "";
            WebRTCPhotoDataUrl = null;
            UploadedReportFile = null;
        }

        async Task ConfirmReport()
        {
            if (IsSubmittingReport) return;
            if (string.IsNullOrWhiteSpace(ContentInput))
            {
                CloseDetailModal();
                return;
            }
            long maxSize = 10 * 1024 * 1024;

            if (WebRTCPhotoDataUrl != null)
            {
                var base64 = WebRTCPhotoDataUrl.Substring(WebRTCPhotoDataUrl.IndexOf(",") + 1);
                var bytes = Convert.FromBase64String(base64);

                if (bytes.Length > maxSize)
                {
                    CloseDetailModal();
                    await AlertService.ShowErrorAsync("Ukuran File Anda terlalu besar. Size maximal hanya 10 MB.");
                    return;
                }
            }

            if (UploadedReportFile != null)
            {
                if (UploadedReportFile.Size > maxSize)
                {
                    CloseDetailModal();
                    await AlertService.ShowErrorAsync("Ukuran File Anda terlalu besar. Size maximal hanya 10 MB.");
                    return;
                }
            }

            IsSubmittingReport = true;
            var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(ContentInput), "description");

            if (WebRTCPhotoDataUrl != null)
            {
                var base64 = WebRTCPhotoDataUrl.Substring(WebRTCPhotoDataUrl.IndexOf(",") + 1);
                var bytes = Convert.FromBase64String(base64);

                var mimeType = WebRTCPhotoDataUrl.Substring(5, WebRTCPhotoDataUrl.IndexOf(";") - 5);
                var ext = mimeType switch
                {
                    "image/png" => ".png",
                    "image/jpeg" => ".jpg",
                    "image/jpg" => ".jpg",
                    _ => ".jpg"
                };

                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
                formData.Add(fileContent, "guidancePhoto", $"guidance{ext}");
            }

            if (UploadedReportFile != null)
            {
                var stream = UploadedReportFile.OpenReadStream(maxSize);
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(UploadedReportFile.ContentType);
                formData.Add(fileContent, "reportFile", UploadedReportFile.Name);
            }

            var response = await Http.PostAsync(APIUrl.Endpoint("reports"), formData);
            IsSubmittingReport = false;

            if (response.IsSuccessStatusCode)
            {
                CloseDetailModal();
                ShowReportModal = false;
                await AlertService.ShowSuccessAsync("Report berhasil disubmit.");
                await LoadReports();
            }
            else
            {
                await AlertService.ShowErrorAsync("Gagal submit report.");
            }
        }

        async Task OnFeedbackButtonClick()
        {
            if (string.IsNullOrWhiteSpace(FeedbackInput))
            {
                CloseDetailModal();
                return;
            }
            if (SelectedReportDetail == null) return;

            IsSubmittingFeedback = true;
            var feedbackObj = new { feedback = FeedbackInput };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(feedbackObj),
                System.Text.Encoding.UTF8,
                "application/json"
            );
            var response = await Http.PutAsync(APIUrl.Endpoint($"reports/feedback/{SelectedReportDetail.id}"), content);
            IsSubmittingFeedback = false;

            if (response.IsSuccessStatusCode)
            {
                CloseDetailModal();
                await AlertService.ShowSuccessAsync("Feedback berhasil ditambahkan.");
                await LoadReports();
            }
            else await AlertService.ShowErrorAsync("Gagal submit feedback.");
        }

        private void OnContentInput(ChangeEventArgs e)
        {
            var value = e.Value?.ToString() ?? string.Empty;
            if (value.Length > 200)
                value = value.Substring(0, 200);
            ContentInput = value;
        }

        private void GoToProfile() => Navigation.NavigateTo("/home/profile");
        private string photoUrl = "/images/default_profile.jpg";

        private bool ShowWebRTCModal = false;
        private string? WebRTCPhotoDataUrl = null;
        private DotNetObjectReference<InternReport>? dotNetRef;

        private void OpenWebRTCModal()
        {
            ShowWebRTCModal = true;
            StateHasChanged();
        }

        private async void CloseWebRTCModal()
        {
            await JS.InvokeVoidAsync("webrtcPhoto.stop", "webrtc_capture");
            ShowWebRTCModal = false;
            StateHasChanged();
        }

        private async void CaptureWebRTCPhoto()
        {
            await JS.InvokeVoidAsync("webrtcPhoto.capture", "webrtc_capture", "image/jpeg", 0.92);
        }

        [JSInvokable]
        public async Task OnWebRTCCapture(string elementId, string dataUrl)
        {
            var mimeType = dataUrl.Substring(5, dataUrl.IndexOf(";") - 5);
            var ext = mimeType switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                _ => ".jpg"
            };
            var field = elementId.Replace("webrtc_", "");
            var base64 = dataUrl.Substring(dataUrl.IndexOf(",") + 1);
            var bytes = Convert.FromBase64String(base64);
            var file = new BlazorInputFileStreamFile(field + ext, mimeType, bytes);

            WebRTCPhotoDataUrl = dataUrl;
            ShowWebRTCModal = false;
            StateHasChanged();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (ShowWebRTCModal)
            {
                dotNetRef ??= DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("webrtcPhoto.start", "webrtc_capture", dotNetRef);
            }
        }

        private IBrowserFile? UploadedReportFile = null;
        private string UploadedReportFileName => UploadedReportFile != null ? $"{UploadedReportFile.Name} ({UploadedReportFile.ContentType})" : "No file choosen";

        private void OnReportFileSelected(InputFileChangeEventArgs e)
        {
            UploadedReportFile = e.File;
        }

        private void OnFeedbackInput(ChangeEventArgs e)
        {
            var value = e.Value?.ToString() ?? string.Empty;
            if (value.Length > 100)
                value = value.Substring(0, 100);
            FeedbackInput = value;
        }

        bool ShowReportDetailModal = false;
        bool ShowPreviewModal = false;

        void ShowPreviewModalHandler()
        {
            ShowPreviewModal = true;
        }

        void ClosePreviewModal()
        {
            ShowPreviewModal = false;
        }

        void OpenFileInNewTab(string fileId)
        {
            var url = APIUrl.Endpoint($"reports/preview/{fileId}");
            JS.InvokeVoidAsync("window.open", url, "_blank");
        }

        private bool IsReportFormValid =>
        !string.IsNullOrWhiteSpace(ContentInput)
        && WebRTCPhotoDataUrl != null
           && UploadedReportFile != null;

        async Task SetTimer(int seconds)
        {
            await JS.InvokeVoidAsync("window.webrtcPhoto.SetTimer", $"webrtc_capture", seconds);
        }

        async Task SwitchCamera()
        {
            await JS.InvokeVoidAsync("window.webrtcPhoto.switchCamera", $"webrtc_capture");
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
            Reports = GetSortedReports()
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();
            StateHasChanged();
        }

        private IEnumerable<ReportItem> GetSortedReports()
        {
            IEnumerable<ReportItem> query = AllReports;

            switch (SortColumn)
            {
                case nameof(ReportItem.nis):
                    query = SortAscending
                        ? query.OrderBy(x => x.nis)
                        : query.OrderByDescending(x => x.nis);
                    break;
                case nameof(ReportItem.name):
                    query = SortAscending
                        ? query.OrderBy(x => x.name)
                        : query.OrderByDescending(x => x.name);
                    break;
                case nameof(ReportItem.classroom_name):
                    query = SortAscending
                        ? query.OrderBy(x => x.classroom_name)
                        : query.OrderByDescending(x => x.classroom_name);
                    break;
                case nameof(ReportItem.time):
                    query = SortAscending
                        ? query.OrderBy(x => x.time)
                        : query.OrderByDescending(x => x.time);
                    break;
                case nameof(ReportItem.company_name):
                    query = SortAscending
                        ? query.OrderBy(x => x.company_name)
                        : query.OrderByDescending(x => x.company_name);
                    break;
                case nameof(ReportItem.description):
                    query = SortAscending
                        ? query.OrderBy(x => x.description)
                        : query.OrderByDescending(x => x.description);
                    break;
                case nameof(ReportItem.feedback):
                    query = SortAscending
                        ? query.OrderBy(x => x.feedback)
                        : query.OrderByDescending(x => x.feedback);
                    break;
                case "Bimbingan":
                    query = SortAscending
                        ? query.OrderBy(x => x.isGuidance == "✔️" ? 0 : 1)
                        : query.OrderByDescending(x => x.isGuidance == "✔️" ? 0 : 1);
                    break;
            }
            return query;
        }

        private async Task LoadStudentReports()
        {
            var url = APIUrl.Endpoint($"reports?page={CurrentPage}&pageSize={PageSize}");
            var response = await Http.GetFromJsonAsync<ReportListResponse>(url);
            if (response != null)
            {
                Reports = response.data ?? new();
                TotalPages = response.totalPages;
            }
            StateHasChanged();
        }

        async Task LoadReports(string dateFilter = null)
        {
            isLoading = true;
            StateHasChanged();

            string url = APIUrl.Endpoint("reports");
            if (!string.IsNullOrWhiteSpace(searchName))
                url += $"?search={searchName}";
            if (selectedClassId.HasValue)
                url += (url.Contains("?") ? "&" : "?") + $"classId={selectedClassId}";
            if (!string.IsNullOrWhiteSpace(dateFilter ?? selectedDate))
                url += (url.Contains("?") ? "&" : "?") + $"date={dateFilter ?? selectedDate}";

            // Ambil semua data (tanpa paginasi)
            url += (url.Contains("?") ? "&" : "?") + "pageSize=1000";

            var response = await Http.GetFromJsonAsync<ReportListResponse>(url);
            if (response != null)
            {
                AllReports = response.data ?? new();
                TotalPages = (int)Math.Ceiling((double)AllReports.Count / PageSize);
                Reports = GetSortedReports()
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();
            }
            isLoading = false;
            StateHasChanged();
        }

        async Task OnDateSelected(ChangeEventArgs e)
        {
            var dateValue = e.Value?.ToString();
            if (string.IsNullOrWhiteSpace(dateValue)) return;

            showDatePicker = false;
            selectedDate = dateValue;
            CurrentPage = 1;
            await LoadReports(selectedDate);
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
                    await LoadReports(selectedDate);
                }
            }
            catch (TaskCanceledException) { }
        }

        private async void OnClassChange(ChangeEventArgs e)
        {
            selectedClassId = int.TryParse(e.Value?.ToString(), out var id) ? id : null;
            CurrentPage = 1;
            await LoadReports(selectedDate);
        }

        private int SelectedReportIndex => Reports.FindIndex(r => r.id == SelectedReportDetail?.id);
        private bool IsPrevReportDisabled => GetPrevReportIndex() == -1;
        private bool IsNextReportDisabled => GetNextReportIndex() == -1;

        private int GetPrevReportIndex()
        {
            if (Reports == null || Reports.Count == 0 || SelectedReportDetail == null) return -1;
            for (int i = SelectedReportIndex - 1; i >= 0; i--)
            {
                if (!string.IsNullOrWhiteSpace(Reports[i].id) && Reports[i].id != "-")
                    return i;
            }
            return -1;
        }

        private int GetNextReportIndex()
        {
            if (Reports == null || Reports.Count == 0 || SelectedReportDetail == null) return -1;
            for (int i = SelectedReportIndex + 1; i < Reports.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(Reports[i].id) && Reports[i].id != "-")
                    return i;
            }
            return -1;
        }

        private void ShowPrevReport()
        {
            var prevIdx = GetPrevReportIndex();
            if (prevIdx != -1) SelectedReportDetail = Reports[prevIdx];
        }

        private void ShowNextReport()
        {
            var nextIdx = GetNextReportIndex();
            if (nextIdx != -1) SelectedReportDetail = Reports[nextIdx];
        }

        // --- Modal Print Per Mentor ---
        private bool ShowPrintMentorModal = false;
        private List<MentorItem> MentorList = new();
        private int? SelectedMentorId = null;
        private string PrintStartDate = "";
        private string PrintEndDate = "";
        private string MessagePrintSuccess = "Rekap data Bimbingan Laporan berhasil diunduh.";
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
            var url = APIUrl.Endpoint($"reports/mentor/{SelectedMentorId}/print?startDate={PrintStartDate}&endDate={PrintEndDate}");
            var response = await Http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = FileHelper.GetFileNameFromContentDisposition(response);
                await JS.InvokeVoidAsync("downloadFileFromBytes", fileName, "application/pdf", bytes);

                CancelPrintMentorModal();
                await AlertService.ShowSuccessAsync(MessagePrintSuccess);
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
            var url = APIUrl.Endpoint($"reports/class/{SelectedClassPrintId}/print?startDate={PrintStartDate}&endDate={PrintEndDate}");
            var response = await Http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = FileHelper.GetFileNameFromContentDisposition(response);
                await JS.InvokeVoidAsync("downloadFileFromBytes", fileName, "application/pdf", bytes);

                CancelPrintClassModal();
                await AlertService.ShowSuccessAsync(MessagePrintSuccess);
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
                url = APIUrl.Endpoint($"reports/combined/{CurrentUserId}/print?startDate={PrintStartDate}&endDate={PrintEndDate}");
            else if (isMentor)
                url = APIUrl.Endpoint($"reports/mentor/{CurrentMentorId}/print?startDate={PrintStartDate}&endDate={PrintEndDate}");
            else if (isWalas)
                url = APIUrl.Endpoint($"reports/class/{CurrentClassId}/print?startDate={PrintStartDate}&endDate={PrintEndDate}");

            var response = await Http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = FileHelper.GetFileNameFromContentDisposition(response);
                await JS.InvokeVoidAsync("downloadFileFromBytes", fileName, "application/pdf", bytes);

                CancelPrintCombinedModal();
                await AlertService.ShowSuccessAsync(MessagePrintSuccess);
                StateHasChanged();
            }
        }

        private async void PrintStudentReport(ReportItem item)
        {
            var studentId = item.studentId;
            var date = item.date;

            if (DateTime.TryParse(date, out var dt))
                date = dt.ToString("yyyy-MM-dd");

            var url = APIUrl.Endpoint($"reports/student/{studentId}/print?date={date}");
            var response = await Http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = FileHelper.GetFileNameFromContentDisposition(response);
                await JS.InvokeVoidAsync("downloadFileFromBytes", fileName, "application/pdf", bytes);

                await AlertService.ShowSuccessAsync(MessagePrintSuccess);
                StateHasChanged();
            }
            else await AlertService.ShowErrorAsync("Gagal mengunduh file presensi.");
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
                    await LoadReports(selectedDate);
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
                    await LoadReports(selectedDate);
                    StateHasChanged();
                }
            }
        }

        // State untuk delete Presensi
        private bool ShowDeleteReportModal = false;
        private string? DeleteReportId = null;
        private string DeleteReportName = "";
        private string DeleteReportDate = "";

        private void ShowDeleteReportConfirmation(ReportItem item)
        {
            DeleteReportId = item.id;
            DeleteReportName = item.name;
            DeleteReportDate = FormatTanggalIndo(item.date);
            ShowDeleteReportModal = true;
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

        private async Task ConfirmDeleteReportAsync()
        {
            if (DeleteReportId == null) return;
            var response = await Http.DeleteAsync(APIUrl.Endpoint($"reports/delete/{DeleteReportId}"));
            ShowDeleteReportModal = false;
            DeleteReportId = null;
            DeleteReportName = "";
            DeleteReportDate = "";

            if (response.IsSuccessStatusCode)
            {
                await AlertService.ShowSuccessAsync("Data bimbingan laporan siswa berhasil dihapus.");
                await LoadReports(selectedDate);
            }
            else await AlertService.ShowErrorAsync("Gagal menghapus data bimbingan laporan.");
        }

        private void CancelDeleteReport()
        {
            ShowDeleteReportModal = false;
            DeleteReportId = null;
            DeleteReportName = "";
            DeleteReportDate = "";
        }
    }
}