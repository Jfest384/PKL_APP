using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PKLPresenceWeb.Model;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace PKLPresenceWeb.Pages.Clients
{
    public partial class Participant : ComponentBase
    {
        private string UserRole = "";
        private string SearchText = "";
        private int? SelectedClassId;
        private int CurrentPage = 1;
        private int TotalPages = 1;
        private bool IsLoading = false;

        private List<ClassItem> Classes = new();
        private List<string> VisibleTabs = new();
        private string CurrentTab = "";

        private List<StudentItem> Students = new();
        private List<MentorItem> Mentors = new();
        private List<StudentPKLItem> StudentPKLs = new();
        private List<ClassItem> Classrooms = new();

        private CancellationTokenSource? searchDebounceCts;
        private string AddStudentSearchText = "";
        private string CompanySearchText = "";

        bool ShowAddStudentToMentorModal = false;
        int? SelectedMentorId;
        string? SelectedMentorName;
        List<StudentPKLItem> AvailableStudents = new();
        HashSet<int> SelectedStudentIds = new();
        bool IsLoadingStudents = false;

        // Modal Pilih PT
        bool ShowPilihPTModal = false;
        int? SelectedStudentIdForPT = null;
        int? SelectedCompanyLocationId = null;
        List<CompanyItem> Companies = new();
        List<CompanyLocationItem> CompanyLocations = new();
        List<CompanyLocationGroup> GroupedCompanyLocations = new();
        bool IsLoadingCompanies = false;

        private WahaSession? WahaSession;

        private bool ShowAddModal = false;
        private string AddEditClassMode = "add"; // "add" atau "edit"
        private int? EditClassId = null;
        private ClassItem EditClassData = new();
        private NewClass NewClass = new()
        {
            name = string.Empty,
            WaliKelasid = 0,
            year = DateTime.Now.Year,
            description = string.Empty,
            contactId = string.Empty
        };
        private int? SelectedWalasId;
        private List<TeacherItem> TeacherList = new();
        private bool IsAddClassValid =>
            !string.IsNullOrWhiteSpace(NewClass.name)
            && SelectedWalasId.HasValue
            && NewClass.year > 0
            && !string.IsNullOrWhiteSpace(NewClass.description);

        // 1. ShowAssignPKLError → Swal alert error
        private async void ShowAssignPKLErrorAlert()
        {
            await JS.InvokeVoidAsync("Swal.fire", new
            {
                title = "Error!",
                text = AssignPKLModalMode == "publish"
                    ? "Silakan pilih minimal satu siswa terlebih dahulu sebelum menerbitkan rekap."
                    : $"Silakan pilih minimal satu siswa terlebih dahulu sebelum {(AssignPKLModalMode == "assign" ? "assign" : "delete")}.",
                icon = "error",
                timer = 3000,
                showConfirmButton = false,
                width = "90%",
                customClass = new
                {
                    popup = "my-swal-popup",
                    title = "my-swal-title",
                    htmlContainer = "my-swal-text",
                    icon = "my-swal-icon"
                }
            });
        }

        // 2. ShowAssignClassFailedModal → Swal alert error + button
        private async void ShowAssignClassFailedAlert()
        {
            await JS.InvokeVoidAsync("Swal.fire", new
            {
                title = "Gagal",
                text = AssignClassFailedText,
                icon = "error",
                showConfirmButton = true,
                confirmButtonText = "Return to List",
                width = "90%",
                customClass = new { popup = "my-swal-popup" }
            });
            ReturnToTKJTab();
        }

        // 6. ShowDeleteMentorSuccessModal → Swal alert success
        private async void ShowDeleteMentorSuccessAlert(string mode)
        {
            await JS.InvokeVoidAsync("Swal.fire", new
            {
                title = "Success!",
                text = mode == "deleteMentor"
                    ? "Pembimbing PKL berhasil dihapus."
                    : "Pengaturan Default Chat berhasil diperbarui.",
                icon = "success",
                timer = 3000,
                showConfirmButton = false,
                width = "90%",
                customClass = new { popup = "my-swal-popup" }
            });
        }

        // 7. ShowDeleteClassSuccessModal → Swal alert success
        private async void ShowDeleteClassSuccessAlert(string mode)
        {
            await JS.InvokeVoidAsync("Swal.fire", new
            {
                title = "Success!",
                text = mode == "company"
                    ? "Company berhasil dihapus."
                    : "Kelas berhasil dihapus.",
                icon = "success",
                timer = 3000,
                showConfirmButton = false,
                width = "90%",
                customClass = new { popup = "my-swal-popup" }
            });
        }

        // 11. ShowVpnWarningModal → Swal alert warning + button
        private async void ShowVpnWarningAlert()
        {
            await JS.InvokeVoidAsync("Swal.fire", new
            {
                title = "Koneksi",
                text = "Anda Harus terhubung dengan VPN TKJ untuk mendapatkan data.",
                icon = "warning",
                showConfirmButton = true,
                confirmButtonText = "Close",
                width = "90%",
                customClass = new { popup = "my-swal-popup" }
            });
            CloseVpnWarningModal();
        }

        // 12. ShowWahaStoppedModal → Swal alert info/warning + button (jika perlu)
        private async void ShowWahaStoppedAlert()
        {
            if (WahaStoppedModalMode == "Waha")
            {
                var result = await JS.InvokeAsync<SwalResult>("Swal.fire", new
                {
                    title = "Pesan",
                    text = "Status Message sedang tidak aktif.",
                    icon = "info",
                    showConfirmButton = true,
                    confirmButtonText = "Activate",
                    width = "90%",
                    customClass = new { popup = "my-swal-popup" }
                });

                if (result.isConfirmed)
                    await ActivateMessageTab();
            }
            else if (WahaStoppedModalMode == "DefaultChat")
            {
                await JS.InvokeVoidAsync("Swal.fire", new
                {
                    title = "Service",
                    text = $"Default Chat dengan Service {DefaultChatServiceName} sudah ada dalam data.",
                    icon = "warning",
                    timer = 3000,
                    showConfirmButton = false,
                    width = "90%",
                    customClass = new { popup = "my-swal-popup" }
                });
            }
        }

        private void OnAddClassClicked()
        {
            AddEditClassMode = "add";
            ShowAddModal = true;
            NewClass = new()
            {
                name = string.Empty,
                WaliKelasid = 0,
                year = DateTime.Now.Year,
                description = string.Empty,
                contactId = string.Empty
            };
            SelectedWalasId = null;
        }

        private void OnEditClassClicked(ClassItem kelas)
        {
            AddEditClassMode = "edit";
            ShowAddModal = true;
            EditClassId = kelas.id;
            NewClass = new()
            {
                name = kelas.name,
                WaliKelasid = kelas.id_walas,
                year = kelas.year,
                description = kelas.description,
                contactId = kelas.chatContactid
            };
            SelectedWalasId = kelas.id_walas;
        }

        private async void ConfirmAddOrEdit()
        {
            if (SelectedWalasId.HasValue)
            {
                ShowAddModal = false;
                NewClass.WaliKelasid = SelectedWalasId.Value;
                HttpResponseMessage res;
                if (AddEditClassMode == "add")
                    res = await Http.PostAsJsonAsync(APIUrl.Endpoint("classes"), NewClass);
                else
                {
                    var editDto = new
                    {
                        NewClass.name,
                        NewClass.WaliKelasid,
                        NewClass.year,
                        NewClass.description,
                        NewClass.contactId
                    };
                    res = await Http.PutAsJsonAsync(APIUrl.Endpoint($"classes/{EditClassId}"), editDto);
                }
                if (res.IsSuccessStatusCode)
                {
                    NewClass = new()
                    {
                        name = string.Empty,
                        WaliKelasid = 0,
                        year = DateTime.Now.Year,
                        description = string.Empty,
                        contactId = string.Empty
                    };

                    SelectedWalasId = null;
                    EditClassId = null;
                    SuccessModalText = AddEditClassMode == "add"
                            ? "Kelas baru berhasil ditambahkan."
                            : "Data Kelas terkait berhasil diupdate.";

                    await AlertService.ShowSuccessAsync(SuccessModalText);
                    await LoadData();
                }
            }
        }

        private void CancelAdd()
        {
            NewClass = new()
            {
                name = string.Empty,
                WaliKelasid = 0,
                year = DateTime.Now.Year,
                description = string.Empty,
                contactId = string.Empty
            };
            SelectedWalasId = null;
            ShowAddModal = false;
        }

        private void OnContentInput(ChangeEventArgs e)
        {
            var value = e.Value?.ToString() ?? string.Empty;
            if (value.Length > 200)
                value = value.Substring(0, 200);
            NewClass.description = value;
        }

        private bool ShowAddStudentModal = false;
        private string SuccessModalText = "";
        private NewStudent NewStudent = new()
        {
            nis = string.Empty,
            name = string.Empty,
            Classid = 0
        };
        private int? SelectedClassIdForAdd;
        private List<ClassItem> ClassList = new();
        private bool IsAddStudentValid =>
            !string.IsNullOrWhiteSpace(NewStudent.nis)
            && !string.IsNullOrWhiteSpace(NewStudent.name)
            && SelectedClassIdForAdd.HasValue;

        private async void ConfirmAddStudent()
        {
            if (SelectedClassIdForAdd.HasValue)
            {
                ShowAddStudentModal = false;
                NewStudent.Classid = SelectedClassIdForAdd.Value;
                var res = await Http.PostAsJsonAsync(APIUrl.Endpoint("students/add"), NewStudent);
                if (res.IsSuccessStatusCode)
                {
                    CancelAddStudent();
                    await AlertService.ShowSuccessAsync("Siswa baru berhasil ditambahkan.");
                    await LoadData();
                }
            }
        }

        private void CancelAddStudent()
        {
            ShowAddStudentModal = false;
            NewStudent = new()
            {
                nis = string.Empty,
                name = string.Empty,
                Classid = 0
            };
            SelectedClassIdForAdd = null;
        }

        private Dictionary<int, int> StudentCountPerClass = new();

        void CloseDetailModal()
        {
            ShowStudentDetailModal = false;
        }

        private async Task LoadWalas()
        {
            TeacherList = await Http.GetFromJsonAsync<List<TeacherItem>>(APIUrl.Endpoint("teachers"));
        }

        private bool IsMessageTabDisabled = false;
        private async Task ActivateMessageTab()
        {
            Navigation.NavigateTo("/home/profile", forceLoad: true);
        }

        private async Task ShowWahaStoppedInfoModalAsync()
        {
            ShowWahaStoppedAlert();
            StateHasChanged();
        }

        private void CloseVpnWarningModal()
        {
            Navigation.NavigateTo("/home", forceLoad: true);
        }

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            if (!authState.User.Identity.IsAuthenticated)
                Navigation.NavigateTo("/login", true);

            var meJson = await JS.InvokeAsync<string>("localStorage.getItem", "meResponse");
            if (string.IsNullOrWhiteSpace(meJson))
                return;

            var doc = JsonDocument.Parse(meJson);
            var root = doc.RootElement;
            UserRole = root.TryGetProperty("role", out var roleProp) ? roleProp.GetString() ?? "" : "";

            // ===== Ambil foto profile =====
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

            if (UserRole == "Admin" || UserRole == "Kepala Jurusan")
            {
                await LoadClasses();
                VisibleTabs = new() { "Siswa TKJ", "Pembimbing PKL", "Siswa PKL", "Manajemen Kelas", "Daftar PT", "Message" };
                try
                {
                    WahaSession = await Http.GetFromJsonAsync<WahaSession>(APIUrl.Endpoint("waha/sessions/default"));
                    if (WahaSession?.status == "STOPPED")
                    {
                        IsMessageTabDisabled = true;
                        await ShowWahaStoppedInfoModalAsync();
                    }
                }
                catch (HttpRequestException)
                {
                    ShowVpnWarningAlert();
                    StateHasChanged();
                    return;
                }
                catch (Exception)
                {
                    ShowVpnWarningAlert();
                    StateHasChanged();
                    return;
                }
            }
            else if (UserRole == "Student")
                VisibleTabs = new() { "Siswa PKL", "Pembimbing PKL" };
            else
            {
                await LoadClasses();
                VisibleTabs = new() { "Siswa TKJ", "Pembimbing PKL", "Siswa PKL" };
            }

            CurrentTab = VisibleTabs.FirstOrDefault() ?? "";
            await LoadWalas();
            await LoadData();

            if (CurrentTab == "Message" && !IsMessageTabDisabled)
            {
                await LoadDefaultChats();
                await LoadDefaultChatDetails();
                await LoadContacts();
            }
        }

        private async Task LoadClasses()
        {
            var response = await Http.GetFromJsonAsync<ClassListResponse>(APIUrl.Endpoint($"classes?page={CurrentPage}"));
            if (response != null)
            {
                Classes = response.classrooms ?? new();
                TotalPages = response.totalPages;
            }
            ClassList = response?.classrooms ?? new();
        }

        private List<StudentPKLItem> CachedStudentPKLs = new();
        private async Task LoadData()
        {
            IsLoading = true;
            StateHasChanged();
            string url = "";

            if (CurrentTab == "Siswa TKJ")
            {
                url = APIUrl.Endpoint($"students?page={CurrentPage}&pageSize={PageSize}");
                if (!string.IsNullOrWhiteSpace(SearchText)) url += $"&name={SearchText}";
                if (SelectedClassId.HasValue) url += $"&id_class={SelectedClassId}";

                var res = await Http.GetFromJsonAsync<StudentListResponse>(url);
                Students = res?.students ?? new();
                int totalItems = res?.totalItems ?? Students.Count;
                TotalPages = (int)Math.Ceiling((double)totalItems / PageSize);

                if (CachedStudentPKLs.Count == 0)
                    CachedStudentPKLs = await GetAllStudentPKLsAsync();
                StudentPKLs = CachedStudentPKLs;
            }
            else if (CurrentTab == "Pembimbing PKL")
            {
                url = APIUrl.Endpoint($"mentors?page={CurrentPage}&pageSize={PageSize}");
                if (!string.IsNullOrWhiteSpace(MentorSearchText)) url += $"&name={MentorSearchText}";
                var res = await Http.GetFromJsonAsync<MentorListResponse>(url);
                Mentors = res?.data ?? new();
                int totalItems = res?.totalItems ?? Mentors.Count;
                TotalPages = (int)Math.Ceiling((double)totalItems / PageSize);
            }
            else if (CurrentTab == "Siswa PKL")
            {
                url = APIUrl.Endpoint($"students/pkl?page={CurrentPage}&pageSize={PageSize}");
                if (!string.IsNullOrWhiteSpace(SearchText)) url += $"&name={SearchText}";
                if (SelectedClassId.HasValue) url += $"&id_class={SelectedClassId}";

                var res = await Http.GetFromJsonAsync<StudentPKLListResponse>(url);
                StudentPKLs = res?.students ?? new();
                int totalItems = res?.totalItems ?? StudentPKLs.Count;
                TotalPages = (int)Math.Ceiling((double)totalItems / PageSize);
            }
            else if (CurrentTab == "Manajemen Kelas")
            {
                url = APIUrl.Endpoint($"classes?page={CurrentPage}&pageSize={PageSize}");
                if (!string.IsNullOrWhiteSpace(SearchText)) url += $"&name={SearchText}";
                var res = await Http.GetFromJsonAsync<ClassListResponse>(url);
                Classrooms = res?.classrooms ?? new();
                int totalItems = res?.totalItems ?? Classrooms.Count;
                TotalPages = (int)Math.Ceiling((double)totalItems / PageSize);

                CachedAllStudents = await GetAllStudentsAsync(ignoreSearch: CurrentTab == "Manajemen Kelas");
                var students = CachedAllStudents;
                StudentCountPerClass = students
                    .GroupBy(s => s.classroomid)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            else if (CurrentTab == "Daftar PT")
            {
                url = APIUrl.Endpoint($"data/companies?page={CurrentPage}&pageSize={PageSize}");
                if (!string.IsNullOrWhiteSpace(SearchText)) url += $"&name={SearchText}";
                var res = await Http.GetFromJsonAsync<CompanyListResponse>(url);
                Companies = res?.companies ?? new();
                int totalItems = res?.totalItems ?? Companies.Count;
                TotalPages = (int)Math.Ceiling((double)totalItems / PageSize);
            }

            IsLoading = false;
            StateHasChanged();
        }

        private bool IsStudentInPKL(int studentId)
        {
            return StudentPKLs.Any(pkl => pkl.id == studentId);
        }

        private async Task ChangeTab(string tab)
        {
            CurrentTab = tab;
            CurrentPage = 1;
            SearchText = string.Empty;
            SelectedClassId = null;
            ShowConfirmationModal = false;
            IsSortingAllData = false; // reset
            SelectedSiswaPKLIds.Clear();
            AssignedStudents.Clear();
            await LoadData();

            if (tab == "Message")
            {
                WahaSession = await Http.GetFromJsonAsync<WahaSession>(APIUrl.Endpoint("waha/sessions/default"));
                if (WahaSession?.status == "STARTING")
                {
                    await PollWahaSessionStatus();
                    return;
                }

                await LoadDefaultChats();
                await LoadDefaultChatDetails();
                await LoadContacts();
            }
        }

        private async Task PollWahaSessionStatus()
        {
            int maxTries = 3;
            int tryCount = 0;
            while (tryCount < maxTries)
            {
                await Task.Delay(2000);
                WahaSession = await Http.GetFromJsonAsync<WahaSession>(APIUrl.Endpoint("waha/sessions/default"));
                tryCount++;
                if (WahaSession?.status == "WORKING")
                {
                    Navigation.NavigateTo("/participant", forceLoad: true);
                    return;
                }

            }
        }

        private async Task ChangePage(int page)
        {
            if (page >= 1 && page <= TotalPages && page != CurrentPage)
            {
                CurrentPage = page;
                await LoadData();
            }
        }

        private async void OnClassChange(ChangeEventArgs e)
        {
            SelectedClassId = int.TryParse(e.Value?.ToString(), out var id) ? id : null;
            CachedAllStudents.Clear();
            IsSortingAllData = false;
            LastStudentSearchText = "";
            LastStudentClassId = null;
            CurrentPage = 1;
            await LoadData();
        }

        private async void OnSearchChanged(ChangeEventArgs e)
        {
            SearchText = e.Value?.ToString() ?? "";
            CachedAllStudents.Clear();
            IsSortingAllData = false;
            LastStudentSearchText = "";
            LastStudentClassId = null;
            searchDebounceCts?.Cancel();
            searchDebounceCts = new CancellationTokenSource();
            var token = searchDebounceCts.Token;
            try
            {
                await Task.Delay(300, token);
                if (!token.IsCancellationRequested)
                {
                    CurrentPage = 1;
                    await LoadData();
                }
            }
            catch (TaskCanceledException) { }
        }

        private List<string[]> GetCurrentData()
        {
            if (CurrentTab == "Siswa TKJ")
            {
                var source = IsSortingAllData ? CachedAllStudents : Students;
                return source.Select(s => new[] { s.id.ToString(), s.nis, s.fullname, s.class_name, s.classroomid.ToString(), s.isPKL.ToString() }).ToList();
            }
            else if (CurrentTab == "Siswa PKL")
            {
                var source = IsSortingAllData ? CachedAllStudentPKLs : StudentPKLs;
                return source.Select(p => new[] { p.id.ToString(), p.nis, p.fullname, p.class_name, p.mentor_name, p.company_name }).ToList();
            }
            else if (CurrentTab == "Manajemen Kelas")
            {
                var source = IsSortingAllData ? CachedAllClassrooms : Classrooms;
                return source.Select(c => new[] { c.id.ToString(), c.name, c.students.ToString(), c.walas, c.year.ToString(), c.description }).ToList();
            }
            else if (CurrentTab == "Pembimbing PKL")
            {
                var source = IsSortingAllData ? CachedAllMentors : Mentors;
                return source.Select(m => new[] { m.id.ToString(), m.nip, m.fullname, string.Join(", ", m.classes) }).ToList();
            }
            else if (CurrentTab == "Daftar PT")
            {
                var source = IsSortingAllData ? CachedAllCompanies : Companies;
                return source.Select(t => new[] { t.id.ToString(), t.name }).ToList();
            }
            return new();
        }

        private List<string> GetHeaders()
        {
            if (CurrentTab == "Siswa TKJ") return new() { "NIS", "Nama", "Kelas" };
            if (CurrentTab == "Pembimbing PKL" && UserRole != "Student") return new() { "NIP", "Nama", "Kelas Dibimbing" };
            if (CurrentTab == "Pembimbing PKL" && UserRole == "Student") return new() { "Nama", "Kelas Dibimbing" };
            if (CurrentTab == "Siswa PKL") return new() { "NIS", "Nama", "Kelas", "Pembimbing", "Tempat PKL" };
            if (CurrentTab == "Manajemen Kelas") return new() { "Kelas", "Total Siswa", "Wali Kelas", "Tahun", "Deskripsi" };
            if (CurrentTab == "Daftar PT") return new() { "Nama" };
            return new();
        }

        private void GoBack() => Navigation.NavigateTo("/home");
        private int? SelectedStudentIdForClassManagement = null;
        private string? SelectedIsPKLForClassManagement = null;

        bool ShowConfirmationModal = false;

        async void OnAddStudentToMentor(string[] mentorRow)
        {
            SelectedMentorId = mentorRow.Length > 0 ? int.TryParse(mentorRow[0], out var id) ? id : (int?)null : null;
            SelectedMentorName = mentorRow.Length > 0 ? mentorRow[0] : null;

            ShowAddStudentToMentorModal = true;
            SelectedStudentIds.Clear();
            IsLoadingStudents = true;

            var allStudents = await GetAllStudentPKLsAsync();

            AvailableStudents = allStudents
                .Where(s => string.IsNullOrWhiteSpace(s.mentor_name) || s.mentor_name == "-")
                .ToList();
            IsLoadingStudents = false;

            StateHasChanged();
        }

        void OnStudentCheckboxChanged(ChangeEventArgs e, int studentId)
        {
            var isChecked = e.Value is bool b && b;
            if (isChecked) SelectedStudentIds.Add(studentId);
            else SelectedStudentIds.Remove(studentId);
        }

        async Task ConfirmAddStudentToMentor()
        {
            if (SelectedMentorId.HasValue && SelectedStudentIds.Count > 0)
            {
                var payload = SelectedStudentIds.ToArray();
                var response = await Http.PutAsJsonAsync(APIUrl.Endpoint($"assign/mentor/{SelectedMentorId}"), payload);
                if (response.IsSuccessStatusCode)
                {
                    CancelAddStudentToMentor();
                    ShowAddStudentToMentorModal = false;
                    await AlertService.ShowSuccessAsync("Siswa telah berhasil di-assign ke pembimbing PKL yang dipilih.");
                    await LoadData();
                }
            }
        }

        void CancelAddStudentToMentor()
        {
            ShowAddStudentToMentorModal = false;
            SelectedMentorId = null;
            SelectedMentorName = null;
            SelectedStudentIds.Clear();
            AddStudentSearchText = "";
        }

        async void OnPilihTempatPKL(string[] studentRow)
        {
            SelectedStudentIdForPT = int.Parse(studentRow[0]);
            SelectedCompanyLocationId = null;
            ShowPilihPTModal = true;
            IsLoadingCompanies = true;
            Companies.Clear();

            try
            {
                //var result = await Http.GetFromJsonAsync<CompanyLocationListResponse>(APIUrl.Endpoint("data/company-locations?page=1&pageSize=1000"));
                //if (result != null)
                //    CompanyLocations = result.companyLocations;

                var compResp = await Http.GetFromJsonAsync<CompanyListResponse>(APIUrl.Endpoint("data/companies?page=1&pageSize=1000"));
                if (compResp != null)
                    Companies = compResp.companies;

                var locResp = await Http.GetFromJsonAsync<CompanyLocationListResponse>(
                    APIUrl.Endpoint("data/company-locations?page=1&pageSize=1000"));

                if (locResp != null)
                    CompanyLocations = locResp.companyLocations;

                GroupedCompanyLocations = Companies
                    .Select(c => new CompanyLocationGroup
                    {
                        CompanyId = c.id,
                        CompanyName = c.name,
                        Locations = CompanyLocations.Where(l => l.companyid == c.id).ToList()
                    })
                    .Where(g => g.Locations.Any()).ToList();
            }
            finally
            {
                IsLoadingCompanies = false;
                StateHasChanged();
            }
        }

        void OnCompanyRadioChanged(ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out var id))
                SelectedCompanyLocationId = id;
        }

        async Task ConfirmPilihPT()
        {
            if (SelectedStudentIdForPT.HasValue && SelectedCompanyLocationId.HasValue)
            {
                var url = APIUrl.Endpoint($"assign/company-location/{SelectedStudentIdForPT}?companyLocationId={SelectedCompanyLocationId}");
                var response = await Http.PutAsync(url, null);
                if (response.IsSuccessStatusCode)
                {
                    ClosePilihPTModal();
                    ShowPilihPTModal = false;
                    await AlertService.ShowSuccessAsync("Data Lokasi PKL berhasil diupdate.");
                    await LoadData();
                }
                else await AlertService.ShowErrorAsync("Gagal assign tempat PKL. Coba lagi setelah beberapa saat.");
            }
        }

        void ClosePilihPTModal()
        {
            ShowPilihPTModal = false;
            SelectedStudentIdForPT = null;
            SelectedCompanyLocationId = null;
            CompanySearchText = "";
            Companies.Clear();
        }

        private string GetTableCode()
        {
            if (CurrentTab == "Siswa TKJ")
            {
                if (UserRole == "Admin" || UserRole == "Kepala Jurusan")
                    return "tkj-admin";
                if (UserRole != "Admin" || UserRole != "Kepala Jurusan" || UserRole != "Student")
                    return "tkj-table";
            }
            else if (CurrentTab == "Siswa PKL")
            {
                if (UserRole == "Admin" || UserRole == "Kepala Jurusan")
                    return "pkl-admin";
                if (UserRole == "Teacher" || UserRole == "Student")
                    return "pkl-table";
                if (UserRole == "Mentor" || UserRole == "Wali Kelas" || UserRole == "Wali Kelas & Mentor")
                    return "pkl-mentor";
            }
            else if (CurrentTab == "Pembimbing PKL")
            {
                if (UserRole == "Admin" || UserRole == "Kepala Jurusan")
                    return "pembimbing-admin";
                if (UserRole == "Student")
                    return "pembimbing-student";
                else
                    return "pembimbing-table";
            }
            else if (CurrentTab == "Manajemen Kelas")
                return "class";
            else if (CurrentTab == "Daftar PT")
                return "pt";
            return "";
        }

        private void GoToProfile() => Navigation.NavigateTo("/home/profile");
        private string photoUrl = "/images/default_profile.jpg";

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
                    _ = LoadData();
                }
            }
        }

        private string SortColumn = "";
        private bool SortAscending = true;

        private async void SortBy(string column)
        {
            if (SortColumn == column)
                SortAscending = !SortAscending;
            else
            {
                SortColumn = column;
                SortAscending = true;
            }
            CurrentPage = 1;
            IsSortingAllData = true;

            if (CurrentTab == "Siswa TKJ")
            {
                if (CachedAllStudents.Count > 0 && LastStudentSearchText == SearchText && LastStudentClassId == SelectedClassId)
                    Students = CachedAllStudents;
                else
                {
                    var url = APIUrl.Endpoint($"students?page=1&pageSize=1000");
                    if (!string.IsNullOrWhiteSpace(SearchText)) url += $"&name={SearchText}";
                    if (SelectedClassId.HasValue) url += $"&id_class={SelectedClassId}";
                    var res = await Http.GetFromJsonAsync<StudentListResponse>(url);
                    CachedAllStudents = res?.students ?? new();
                    Students = CachedAllStudents;
                    LastStudentSearchText = SearchText;
                    LastStudentClassId = SelectedClassId;
                }
            }
            else if (CurrentTab == "Siswa PKL")
            {
                if (CachedAllStudentPKLs.Count > 0 && LastStudentSearchText == SearchText && LastStudentClassId == SelectedClassId)
                    StudentPKLs = CachedAllStudentPKLs;
                else
                {
                    var url = APIUrl.Endpoint($"students/pkl?page=1&pageSize=1000");
                    if (!string.IsNullOrWhiteSpace(SearchText)) url += $"&name={SearchText}";
                    if (SelectedClassId.HasValue) url += $"&id_class={SelectedClassId}";
                    var res = await Http.GetFromJsonAsync<StudentPKLListResponse>(url);
                    CachedAllStudentPKLs = res?.students ?? new();
                    StudentPKLs = CachedAllStudentPKLs;
                    LastStudentSearchText = SearchText;
                    LastStudentClassId = SelectedClassId;
                }
            }
            else if (CurrentTab == "Manajemen Kelas")
            {
                if (CachedAllClassrooms.Count > 0 && LastStudentSearchText == SearchText)
                    Classrooms = CachedAllClassrooms;
                else
                {
                    var url = APIUrl.Endpoint($"classes?page=1&pageSize=1000");
                    if (!string.IsNullOrWhiteSpace(SearchText)) url += $"&name={SearchText}";
                    var res = await Http.GetFromJsonAsync<ClassListResponse>(url);
                    CachedAllClassrooms = res?.classrooms ?? new();
                    Classrooms = CachedAllClassrooms;
                    LastStudentSearchText = SearchText;
                }
            }
            else if (CurrentTab == "Pembimbing PKL")
            {
                if (CachedAllMentors.Count > 0)
                    Mentors = CachedAllMentors;
                else
                {
                    var res = await Http.GetFromJsonAsync<MentorListResponse>(
                        APIUrl.Endpoint($"mentors?page=1&pageSize=1000"));
                    CachedAllMentors = res?.data ?? new();
                    Mentors = CachedAllMentors;
                }
            }
            else if (CurrentTab == "Daftar PT")
            {
                if (CachedAllCompanies.Count > 0 && LastStudentSearchText == SearchText)
                    Companies = CachedAllCompanies;
                else
                {
                    var url = APIUrl.Endpoint($"data/companies?page=1&pageSize=1000");
                    if (!string.IsNullOrWhiteSpace(SearchText)) url += $"&name={SearchText}";
                    var res = await Http.GetFromJsonAsync<CompanyListResponse>(url);
                    CachedAllCompanies = res?.companies ?? new();
                    Companies = CachedAllCompanies;
                    LastStudentSearchText = SearchText;
                }
            }
            StateHasChanged();
        }

        private List<string[]> GetSortedData()
        {
            var data = GetCurrentData();

            if (string.IsNullOrEmpty(SortColumn))
                return data;

            Func<string[], object> keySelector = row => row[0];

            if (CurrentTab == "Siswa TKJ")
            {
                if (SortColumn == "NIS") keySelector = row => row[1];
                else if (SortColumn == "Nama") keySelector = row => row[2];
                else if (SortColumn == "Kelas") keySelector = row => row[3];
            }
            else if (CurrentTab == "Pembimbing PKL")
            {
                if (SortColumn == "NIP") keySelector = row => row[1];
                else if (SortColumn == "Nama") keySelector = row => row[2];
            }
            else if (CurrentTab == "Siswa PKL")
            {
                if (SortColumn == "NIS") keySelector = row => row[1];
                else if (SortColumn == "Nama") keySelector = row => row[2];
                else if (SortColumn == "Kelas") keySelector = row => row[3];
                else if (SortColumn == "Pembimbing") keySelector = row => row[4];
                else if (SortColumn == "Tempat PKL") keySelector = row => row[5];
            }
            else if (CurrentTab == "Manajemen Kelas")
            {
                if (SortColumn == "Kelas") keySelector = row => row[1];
                else if (SortColumn == "Total Siswa") keySelector = row => int.TryParse(row[2], out var v) ? v : 0;
                else if (SortColumn == "Wali Kelas") keySelector = row => row[3];
                else if (SortColumn == "Tahun") keySelector = row => row[4];
            }
            else if (CurrentTab == "Daftar PT")
            {
                if (SortColumn == "Nama") keySelector = row => row[1];
            }

            if (SortColumn == "NIS" || SortColumn == "Total Siswa")
            {
                return SortAscending
                    ? data.OrderBy(row => int.TryParse(keySelector(row)?.ToString(), out var v) ? v : 0).ToList()
                    : data.OrderByDescending(row => int.TryParse(keySelector(row)?.ToString(), out var v) ? v : 0).ToList();
            }
            else
            {
                return SortAscending
                    ? data.OrderBy(row => keySelector(row)?.ToString()).ToList()
                    : data.OrderByDescending(row => keySelector(row)?.ToString()).ToList();
            }
        }

        private List<string[]> GetPagedSortedData()
        {
            var sorted = GetSortedData();
            if (IsSortingAllData)
                return sorted.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
            return sorted;
        }

        private bool IsSortingAllData = false;
        private List<StudentItem> CachedAllStudents = new();
        private List<StudentPKLItem> CachedAllStudentPKLs = new();
        private List<MentorItem> CachedAllMentors = new();
        private List<ClassItem> CachedAllClassrooms = new();
        private List<CompanyItem> CachedAllCompanies = new();
        private List<CompanyLocationItem> CachedAllCompanyLocations = new();

        private string LastStudentSearchText = "";
        private int? LastStudentClassId = null;
        private bool IsAllStudentsFetched = false;
        private bool IsAllClassroomsFetched = false;

        private int page = 1;
        private int totalPages = 1;

        private async Task<List<StudentItem>> GetAllStudentsAsync(bool ignoreSearch = false)
        {
            if (IsAllStudentsFetched && CachedAllStudents.Count > 0)
                return CachedAllStudents;

            var url = APIUrl.Endpoint("students?page=1&pageSize=1000");
            if (!ignoreSearch && !string.IsNullOrWhiteSpace(SearchText)) url += $"&name={SearchText}";
            if (SelectedClassId.HasValue) url += $"&id_class={SelectedClassId}";

            var res = await Http.GetFromJsonAsync<StudentListResponse>(url);
            CachedAllStudents = res?.students ?? new();
            IsAllStudentsFetched = true;

            LastStudentSearchText = ignoreSearch ? "" : SearchText;
            LastStudentClassId = SelectedClassId;

            return CachedAllStudents;
        }

        private async Task<List<StudentPKLItem>> GetAllStudentPKLsAsync()
        {
            var allPKLs = new List<StudentPKLItem>();
            do
            {
                var res = await Http.GetFromJsonAsync<StudentPKLListResponse>(APIUrl.Endpoint($"students/pkl?pageSize=1000"));
                if (res?.students != null)
                    allPKLs.AddRange(res.students);
                totalPages = res?.totalPages ?? 1;
                page++;
            } while (page <= totalPages);
            return allPKLs;
        }

        private async Task<List<MentorItem>> GetAllMentorsAsync()
        {
            var allMentors = new List<MentorItem>();
            do
            {
                var res = await Http.GetFromJsonAsync<MentorListResponse>(APIUrl.Endpoint($"mentors?page={page}"));
                if (res?.data != null)
                    allMentors.AddRange(res.data);
                totalPages = res?.totalPages ?? 1;
                page++;
            } while (page <= totalPages);
            return allMentors;
        }

        private async Task<List<ClassItem>> GetAllClassroomsAsync()
        {
            if (IsAllClassroomsFetched && CachedAllClassrooms.Count > 0)
                return CachedAllClassrooms;

            var allClasses = new List<ClassItem>();
            do
            {
                var res = await Http.GetFromJsonAsync<ClassListResponse>(APIUrl.Endpoint($"classes?page={page}"));
                if (res?.classrooms != null)
                    allClasses.AddRange(res.classrooms);
                totalPages = res?.totalPages ?? 1;
                page++;
            } while (page <= totalPages);

            CachedAllClassrooms = allClasses;
            IsAllClassroomsFetched = true;
            return CachedAllClassrooms;
        }

        private bool ShowAddMentorModal = false;
        private string MentorSearchText = "";
        private string TeacherSearchText = "";
        private bool IsLoadingTeachers = false;
        private HashSet<int> SelectedTeacherIds = new();
        private Dictionary<int, int> SelectedTeacherUserIds = new();
        private CancellationTokenSource? mentorSearchDebounceCts;

        private void OpenAddMentorModal()
        {
            ShowAddMentorModal = true;
            MentorSearchText = "";
            TeacherList.Clear();
            SelectedTeacherIds.Clear();
            SelectedTeacherUserIds.Clear();
            _ = LoadTeachers();
        }

        private async Task LoadTeachers()
        {
            IsLoadingTeachers = true;
            StateHasChanged();
            var url = APIUrl.Endpoint("teachers");
            if (!string.IsNullOrWhiteSpace(TeacherSearchText))
                url += $"?name={TeacherSearchText}";
            var result = await Http.GetFromJsonAsync<List<TeacherItem>>(url);
            TeacherList = result ?? new();
            IsLoadingTeachers = false;
            StateHasChanged();
        }

        private async void OnMentorSearchChanged(ChangeEventArgs e)
        {
            TeacherSearchText = e.Value?.ToString() ?? "";
            mentorSearchDebounceCts?.Cancel();
            mentorSearchDebounceCts = new CancellationTokenSource();
            var token = mentorSearchDebounceCts.Token;
            try
            {
                await Task.Delay(350, token);
                if (!token.IsCancellationRequested)
                    await LoadTeachers();
            }
            catch (TaskCanceledException) { }
        }

        private async void OnMentorDataSearchChanged(ChangeEventArgs e)
        {
            MentorSearchText = e.Value?.ToString() ?? "";
            mentorSearchDebounceCts?.Cancel();
            mentorSearchDebounceCts = new CancellationTokenSource();
            var token = mentorSearchDebounceCts.Token;
            try
            {
                await Task.Delay(350, token);
                if (!token.IsCancellationRequested)
                    await LoadData();
            }
            catch (TaskCanceledException) { }
        }

        private void OnTeacherCheckboxChanged(ChangeEventArgs e, int teacherId, int userId)
        {
            var isChecked = e.Value is bool b && b;
            if (isChecked)
            {
                SelectedTeacherIds.Add(teacherId);
                SelectedTeacherUserIds[teacherId] = userId;
            }
            else
            {
                SelectedTeacherIds.Remove(teacherId);
                SelectedTeacherUserIds.Remove(teacherId);
            }
        }

        private void CancelAddMentor()
        {
            ShowAddMentorModal = false;
            MentorSearchText = "";
            TeacherList.Clear();
            SelectedTeacherIds.Clear();
            SelectedTeacherUserIds.Clear();
        }

        private async void ConfirmAddMentor()
        {
            // Siapkan payload
            var payload = SelectedTeacherIds.Select(id => new MentorDTO
            {
                id_teacher = id,
                id_user = SelectedTeacherUserIds[id]
            }).ToList();

            ShowAddMentorModal = false;
            var response = await Http.PostAsJsonAsync(APIUrl.Endpoint("mentors"), payload);
            if (response.IsSuccessStatusCode)
            {
                MentorSearchText = "";
                TeacherList.Clear();
                SelectedTeacherIds.Clear();
                SelectedTeacherUserIds.Clear();
                await AlertService.ShowSuccessAsync("Pembimbing PKL berhasil ditambahkan.");
                await LoadData();
            }
            else await AlertService.ShowErrorAsync("Gagal menambah mentor. Silakan coba lagi.");
        }

        private string AssignPKLModalMode = "assign";
        private HashSet<int> SelectedSiswaPKLIds = new();
        private bool ShowAssignPKLModal = false;
        private List<EditStudentBatchDTO> AssignBatchData = new();

        private void OnRowCheckboxChanged(ChangeEventArgs e, string[] item)
        {
            var id = int.Parse(item[0]);
            var isChecked = e.Value is bool b && b;
            if (isChecked)
                SelectedSiswaPKLIds.Add(id);
            else
                SelectedSiswaPKLIds.Remove(id);
        }

        private bool IsHeaderCheckboxChecked
        {
            get
            {
                var pageData = GetPagedSortedData();
                if (pageData.Count == 0) return false;
                return pageData.All(item => SelectedSiswaPKLIds.Contains(int.Parse(item[0])));
            }
            set
            {
                var pageData = GetPagedSortedData();
                if (pageData.Count == 0) return;
                if (value)
                {
                    foreach (var item in pageData)
                        SelectedSiswaPKLIds.Add(int.Parse(item[0]));
                }
                else
                {
                    foreach (var item in pageData)
                        SelectedSiswaPKLIds.Remove(int.Parse(item[0]));
                }
            }
        }

        private bool IsAssignPKLMode = true;
        private void OnAssignStudentPKLClicked()
        {
            AssignPKLModalMode = "assign";
            IsAssignPKLMode = true;
            if (SelectedSiswaPKLIds.Count == 0)
            {
                ShowAssignPKLErrorAlert();
                StateHasChanged();
                return;
            }
            AssignBatchData = GetPagedSortedData()
                .Where(item => SelectedSiswaPKLIds.Contains(int.Parse(item[0])))
                .Select(item => new EditStudentBatchDTO
                {
                    studentId = int.Parse(item[0]),
                    isPKL = true,
                    idClass = int.Parse(item[4])
                }).ToList();
            ShowAssignPKLModal = true;
            StateHasChanged();
        }

        private void OnDeleteStudentPKLClicked()
        {
            AssignPKLModalMode = "delete";
            IsAssignPKLMode = false;
            if (SelectedSiswaPKLIds.Count == 0)
            {
                ShowAssignPKLErrorAlert();
                StateHasChanged();
                return;
            }
            AssignBatchData = GetPagedSortedData()
                .Where(item => SelectedSiswaPKLIds.Contains(int.Parse(item[0])))
                .Select(item => new EditStudentBatchDTO
                {
                    studentId = int.Parse(item[0]),
                    isPKL = false,
                    idClass = int.TryParse(item[4], out var idClassValue) ? idClassValue : null
                }).ToList();
            ShowAssignPKLModal = true;
            StateHasChanged();
        }

        private async void OnPublishRecapClicked()
        {
            AssignPKLModalMode = "publish";
            if (SelectedSiswaPKLIds.Count == 0)
            {
                ShowAssignPKLErrorAlert();
                StateHasChanged();
                return;
            }
            ShowAssignPKLModal = true;
            StateHasChanged();
        }

        private async Task ConfirmAssignOrDeletePKL()
        {
            var response = await Http.PutAsJsonAsync(APIUrl.Endpoint("assign/batch"), AssignBatchData);
            if (response.IsSuccessStatusCode)
            {
                AssignBatchData.Clear();
                SelectedSiswaPKLIds.Clear();
                ShowAssignPKLModal = false;
                SuccessModalText = IsAssignPKLMode
                    ? "Siswa berhasil ditambahkan ke data Siswa PKL."
                    : "Siswa berhasil dihapus dari data Siswa PKL.";
                await AlertService.ShowSuccessAsync(SuccessModalText);
                await LoadData();
            }
        }

        private async Task ConfirmPublishRecap()
        {
            var payload = new
            {
                studentIds = SelectedSiswaPKLIds.ToList(),
                date = DateTime.UtcNow
            };
            var response = await Http.PostAsJsonAsync(APIUrl.Endpoint("recap/publish"), payload);
            ShowAssignPKLModal = false;
            AssignBatchData.Clear();
            SelectedSiswaPKLIds.Clear();
            StateHasChanged();
            if (response.IsSuccessStatusCode)
                await AlertService.ShowSuccessAsync("Rekap berhasil diterbitkan.");
            await LoadData();
        }

        private void CancelAssignPKL()
        {
            ShowAssignPKLModal = false;
            AssignBatchData.Clear();
            SelectedSiswaPKLIds.Clear();
            StateHasChanged();
        }

        private bool ShowStudentDetailModal = false;
        private StudentDetail? SelectedStudentDetail;
        private string? SelectedStudentPhotoUrl = null;

        private async void OnShowStudentDetail(int studentId)
        {
            ShowStudentDetailModal = true;
            SelectedStudentDetail = null;
            SelectedStudentPhotoUrl = null;

            // Fetch student detail
            var detail = await Http.GetFromJsonAsync<StudentDetail>(APIUrl.Endpoint($"students/{studentId}"));
            SelectedStudentDetail = detail;

            // Fetch photo
            var photoRequest = new HttpRequestMessage(HttpMethod.Get, APIUrl.Endpoint($"students/{studentId}/photo"));
            var photoResponse = await Http.SendAsync(photoRequest);
            if (photoResponse.IsSuccessStatusCode)
            {
                var bytes = await photoResponse.Content.ReadAsByteArrayAsync();
                var contentType = photoResponse.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                var base64 = Convert.ToBase64String(bytes);
                SelectedStudentPhotoUrl = $"data:{contentType};base64,{base64}";
            }
            else SelectedStudentPhotoUrl = "/images/default_profile.jpg";
            StateHasChanged();
        }

        private void CloseStudentDetailModal()
        {
            ShowStudentDetailModal = false;
            SelectedStudentDetail = null;
            SelectedStudentPhotoUrl = null;
        }

        private List<StudentPKLItem> FilteredAvailableStudents =>
        string.IsNullOrWhiteSpace(AddStudentSearchText)
            ? AvailableStudents
            : AvailableStudents.Where(s =>
                (s.nis?.Contains(AddStudentSearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.fullname?.Contains(AddStudentSearchText, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();

        private void OnAddStudentSearchChanged(ChangeEventArgs e)
        {
            AddStudentSearchText = e.Value?.ToString() ?? "";
            StateHasChanged();
        }

        //private List<CompanyLocationItem> FilteredCompanyLocations =>
        //string.IsNullOrWhiteSpace(CompanySearchText)
        //    ? CompanyLocations
        //    : CompanyLocations.Where(c =>
        //        (c.LocationName?.Contains(CompanySearchText, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

        //private List<CompanyLocationGroup> FilteredGroupedLocations =>
        //string.IsNullOrWhiteSpace(CompanySearchText)
        //    ? GroupedCompanyLocations
        //    : GroupedCompanyLocations
        //        .Select(g => new CompanyLocationGroup
        //        {
        //            CompanyId = g.CompanyId,
        //            CompanyName = g.CompanyName,
        //            Locations = g.Locations
        //                .Where(l => l.LocationName.Contains(CompanySearchText, StringComparison.OrdinalIgnoreCase))
        //                .ToList()
        //        })
        //        .Where(g => g.Locations.Any())
        //        .ToList();

        private List<CompanyLocationGroup> FilteredGroupedLocations =>
        string.IsNullOrWhiteSpace(CompanySearchText)
            ? GroupedCompanyLocations
            : GroupedCompanyLocations
                .Where(g =>
                    g.CompanyName.Contains(CompanySearchText, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();

        private void OnCompanySearchChanged(ChangeEventArgs e)
        {
            CompanySearchText = e.Value?.ToString() ?? "";
            StateHasChanged();
        }

        private void GoToHistory(string[] item)
        {
            HistoryState.StudentId = int.Parse(item[0]);
            Navigation.NavigateTo("/participant/history");
        }

        private List<(int studentId, bool isPKL)> AssignedStudents = new();
        private bool ShowAssignClassModal = false;
        private string AssignClassModalText = "";
        private string AssignClassFailedText = "";
        private string AssignClassName = "";
        private int AssignClassId = 0;
        private List<EditStudentBatchDTO> AssignClassBatchData = new();
        private string AssignClassSuccessText = "";

        private void OnAssignClassClicked()
        {
            AssignedStudents = GetPagedSortedData()
                .Where(item => SelectedSiswaPKLIds.Contains(int.Parse(item[0])))
                .Select(item => (
                    studentId: int.Parse(item[0]),
                    isPKL: item.Length > 5 && bool.TryParse(item[5], out var isPKLValue) ? isPKLValue : false
                ))
                .ToList();

            CurrentTab = "Manajemen Kelas";
            CurrentPage = 1;
            SearchText = string.Empty;
            SelectedClassId = null;
            _ = LoadData();
            StateHasChanged();
        }

        private void OnAssignClassToStudents(int classId, string className)
        {
            if (AssignedStudents == null || AssignedStudents.Count == 0)
            {
                AssignClassFailedText = "Silakan pilih minimal satu siswa terlebih dahulu sebelum melanjutkan.";
                ShowAssignClassFailedAlert();
                StateHasChanged();
                return;
            }

            AssignClassId = classId;
            AssignClassName = className;
            AssignClassBatchData = AssignedStudents
                .Select(s => new EditStudentBatchDTO
                {
                    studentId = s.studentId,
                    isPKL = s.isPKL,
                    idClass = classId
                })
                .ToList();

            AssignClassModalText = $"Apakah Anda yakin ingin memasukkan {AssignClassBatchData.Count} siswa ke Kelas {className}?";
            ShowAssignClassModal = true;
            StateHasChanged();
        }

        private void CancelAssignClass()
        {
            ShowAssignClassModal = false;
            AssignClassBatchData.Clear();
            AssignedStudents.Clear();
            AssignClassId = 0;
            AssignClassName = "";
            StateHasChanged();
        }

        private async Task ConfirmAssignClass()
        {
            ShowAssignClassModal = false;
            var response = await Http.PutAsJsonAsync(APIUrl.Endpoint("assign/batch"), AssignClassBatchData);
            if (response.IsSuccessStatusCode)
            {
                AssignClassBatchData.Clear();
                AssignedStudents.Clear();
                AssignClassId = 0;
                AssignClassName = "";

                await AlertService.ShowSuccessAsync("Siswa berhasil ditambahkan ke Kelas.");
                await Task.Delay(3000);
                Navigation.NavigateTo("/participant", forceLoad: true);
            }
        }

        private void ReturnToTKJTab()
        {
            CurrentTab = "Siswa TKJ";
            CurrentPage = 1;
            SearchText = string.Empty;
            SelectedClassId = null;
            AssignedStudents.Clear();
            StateHasChanged();
            _ = LoadData();
        }

        private bool ShowDeleteStudentTKJModal = false;
        private List<int> DeleteStudentTKJIds = new();
        private string DeleteStudentTKJErrorText = "";

        // State untuk delete mentor
        private bool ShowDeleteMentorModal = false;
        private int? DeleteMentorId = null;
        private int? DeleteDefaultChatId = null;
        private string DeleteMentorName = "";
        private string DeleteMentorModalMode = "deleteMentor";

        private bool ShowDeleteClassModal = false;
        private int? DeleteClassId = null;
        private string DeleteClassName = "";

        private string DeleteMode = "class";
        private int? DeleteCompanyId = null;
        private string DeleteCompanyName = "";

        private void OnDeleteStudentTKJClicked()
        {
            AssignPKLModalMode = "delete";
            DeleteStudentTKJIds = GetPagedSortedData()
                .Where(item => SelectedSiswaPKLIds.Contains(int.Parse(item[0])))
                .Select(item => int.Parse(item[0]))
                .ToList();

            if (DeleteStudentTKJIds.Count == 0)
            {
                ShowAssignPKLErrorAlert();
                StateHasChanged();
                return;
            }

            ShowDeleteStudentTKJModal = true;
            StateHasChanged();
        }

        private void CancelDeleteStudentTKJ()
        {
            ShowDeleteStudentTKJModal = false;
            DeleteStudentTKJIds.Clear();
            SelectedSiswaPKLIds.Clear();
            StateHasChanged();
        }

        private async void ConfirmDeleteStudentTKJ()
        {
            if (DeleteStudentTKJIds.Count == 0)
                return;

            ShowDeleteStudentTKJModal = false;
            var request = new HttpRequestMessage(HttpMethod.Delete, APIUrl.Endpoint("students/delete"))
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(DeleteStudentTKJIds), System.Text.Encoding.UTF8, "application/json")
            };
            var response = await Http.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                DeleteStudentTKJIds.Clear();
                SelectedSiswaPKLIds.Clear();
                await AlertService.ShowSuccessAsync("Siswa berhasil dihapus dari data Siswa TKJ.");
                await LoadData();
            }
            else await AlertService.ShowErrorAsync("Gagal menghapus siswa. Silakan coba lagi.");
        }

        private void OnDeleteMentorClicked(string[] mentorRow)
        {
            DeleteMentorModalMode = "deleteMentor";
            DeleteMentorId = mentorRow.Length > 0 ? int.TryParse(mentorRow[0], out var id) ? id : (int?)null : null;
            DeleteMentorName = mentorRow.Length > 2 ? mentorRow[2] : "";

            if (DeleteMentorId == null || string.IsNullOrWhiteSpace(DeleteMentorName))
                return;

            ShowDeleteMentorModal = true;
            StateHasChanged();
        }

        private async Task OnClearDefaultChat(DefaultChatItem chat)
        {
            DeleteMentorModalMode = "deleteDefaultChat";
            DeleteDefaultChatId = chat.ServiceId;
            if (DeleteDefaultChatId == null)
                return;
            ShowDeleteMentorModal = true;
            StateHasChanged();
        }

        private void CancelDeleteMentor()
        {
            ShowDeleteMentorModal = false;
            DeleteMentorId = null;
            DeleteMentorName = "";
            DeleteDefaultChatId = null;
            StateHasChanged();
        }

        private async void ConfirmDeleteMentor()
        {
            ShowDeleteMentorModal = false;

            if (DeleteMentorModalMode == "deleteMentor")
            {
                if (!DeleteMentorId.HasValue)
                    return;

                var request = new HttpRequestMessage(HttpMethod.Delete, APIUrl.Endpoint("mentors/delete"))
                {
                    Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(DeleteMentorId.Value), System.Text.Encoding.UTF8, "application/json")
                };
                var response = await Http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    CancelDeleteMentor();
                    ShowDeleteMentorSuccessAlert(DeleteMentorModalMode);
                    await LoadData();
                }
                else await AlertService.ShowErrorAsync("Gagal menghapus mentor. Silakan coba lagi.");
            }
            else if (DeleteMentorModalMode == "deleteDefaultChat")
            {
                if (!DeleteDefaultChatId.HasValue)
                    return;
                var request = new HttpRequestMessage(HttpMethod.Delete, APIUrl.Endpoint("chat/default-chats/delete"))
                {
                    Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(DeleteDefaultChatId.Value), System.Text.Encoding.UTF8, "application/json")
                };
                var response = await Http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    CancelDeleteMentor();
                    ShowDeleteMentorSuccessAlert(DeleteMentorModalMode);
                    await LoadDefaultChats();
                    await LoadContacts();
                }
                else await AlertService.ShowErrorAsync("Gagal menghapus Default Chat. Silakan coba lagi.");
            }
        }

        private string LockLocationSuccessMessage = "";
        private bool GetStudentLockStatus(int studentId)
        {
            var student = StudentPKLs.FirstOrDefault(s => s.id == studentId);
            return student != null && student.isLock == "Yes";
        }

        private async void OnLockSliderChanged(int studentId, bool currentStatus)
        {
            // Kirim status yang baru (toggle)
            var newStatus = !currentStatus;
            var payload = new { studentId = studentId, status = newStatus ? 1 : 0 };
            var response = await Http.PutAsJsonAsync(APIUrl.Endpoint("assign/student-lock"), payload);
            if (response.IsSuccessStatusCode)
            {
                // Update data di frontend
                var student = Students.FirstOrDefault(s => s.id == studentId);
                if (student != null)
                    student.isLock = newStatus;

                var studentPKL = StudentPKLs.FirstOrDefault(s => s.id == studentId);
                if (studentPKL != null)
                    studentPKL.isLock = newStatus ? "Yes" : "No";

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                LockLocationSuccessMessage = doc.RootElement.GetProperty("message").GetString() ?? "";
                await AlertService.ShowSuccessAsync(LockLocationSuccessMessage);
                await LoadData();
                StateHasChanged();
            }
            else await AlertService.ShowErrorAsync("Gagal mengubah status lock siswa.");
        }

        private void OnDeleteClassClicked(string[] classRow)
        {
            DeleteMode = "class";
            DeleteClassId = classRow.Length > 0 ? int.TryParse(classRow[0], out var id) ? id : (int?)null : null;
            DeleteClassName = classRow.Length > 1 ? classRow[1] : "";
            ShowDeleteClassModal = true;
            StateHasChanged();
        }

        private void OnDeleteCompanyClicked(string[] companyRow)
        {
            DeleteMode = "company";
            DeleteCompanyId = companyRow.Length > 0 ? int.TryParse(companyRow[0], out var id) ? id : (int?)null : null;
            DeleteCompanyName = companyRow.Length > 1 ? companyRow[1] : "";
            ShowDeleteClassModal = true;
            StateHasChanged();
        }

        private void CancelDeleteClass()
        {
            ShowDeleteClassModal = false;
            DeleteClassId = null;
            DeleteClassName = "";
            DeleteCompanyId = null;
            DeleteCompanyName = "";
            DeleteMode = "class";
            StateHasChanged();
        }

        private async void ConfirmDeleteClass()
        {
            ShowDeleteClassModal = false;

            if (DeleteMode == "company")
            {
                if (!DeleteCompanyId.HasValue)
                    return;

                var request = new HttpRequestMessage(HttpMethod.Delete, APIUrl.Endpoint("data/companies/delete"))
                {
                    Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(DeleteCompanyId.Value), System.Text.Encoding.UTF8, "application/json")
                };
                var response = await Http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    DeleteCompanyId = null;
                    DeleteCompanyName = "";
                    ShowDeleteClassSuccessAlert(DeleteMode);
                    await LoadData();
                }
                else await AlertService.ShowErrorAsync("Gagal menghapus company. Silakan coba lagi.");
            }
            else // delete class
            {
                if (!DeleteClassId.HasValue)
                    return;

                var request = new HttpRequestMessage(HttpMethod.Delete, APIUrl.Endpoint($"classes/{DeleteClassId}"));
                var response = await Http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    DeleteClassId = null;
                    DeleteClassName = "";
                    ShowDeleteClassSuccessAlert(DeleteMode);
                    await LoadData();
                }
                else await AlertService.ShowErrorAsync("Gagal menghapus kelas. Silakan coba lagi.");
            }
        }

        private async void OnTabDropdownChanged(ChangeEventArgs e)
        {
            var selectedTab = e.Value?.ToString() ?? VisibleTabs.FirstOrDefault() ?? "";
            await ChangeTab(selectedTab);
        }

        private List<DefaultChatItem> DefaultChats = new();
        private bool IsLoadingDefaultChats = false;

        private async Task LoadDefaultChats()
        {
            IsLoadingDefaultChats = true;
            DefaultChats = await Http.GetFromJsonAsync<List<DefaultChatItem>>(APIUrl.Endpoint("chat/default-chats")) ?? new();
            IsLoadingDefaultChats = false;
            StateHasChanged();
        }

        private async Task LoadContacts()
        {
            IsLoadingDefaultChats = true;
            var response = await Http.GetFromJsonAsync<List<ChatContactItem>>(APIUrl.Endpoint("waha/contacts/all?session=default")) ?? new();

            ChatContacts = response
                .Where(c =>
                    !string.IsNullOrWhiteSpace(c.name) && (c.statusMute != null || c.isGroup)
                ).ToList();

            IsLoadingDefaultChats = false;
            StateHasChanged();
        }

        private List<DefaultChatDetail> DefaultChatDetails = new();
        private async Task LoadDefaultChatDetails()
        {
            if (DefaultChats == null || DefaultChats.Count == 0)
                return;

            IsLoadingDefaultChats = true;
            StateHasChanged();

            try
            {
                // Ambil semua contactId unik dari DefaultChats
                var contactIds = DefaultChats
                    .SelectMany(dc => dc.ContactId)
                    .Distinct()
                    .ToList();

                var tasks = new List<Task<List<DefaultChatDetail>?>>();

                // Jalankan request paralel (lebih cepat)
                foreach (var contactId in contactIds)
                {
                    var task = Http.GetFromJsonAsync<List<DefaultChatDetail>>(
                        APIUrl.Endpoint($"chat/detail-default-chat?contactId={contactId}")
                    );
                    tasks.Add(task!);
                }

                var results = await Task.WhenAll(tasks);
                DefaultChatDetails = results
                .Where(r => r != null)
                .SelectMany(r => r!)
                .ToList();
            }
            catch (Exception ex) { }
            finally
            {
                IsLoadingDefaultChats = false;
                StateHasChanged();
            }
        }

        private async void OnEditDefaultChat(DefaultChatItem chat)
        {
            DefaultChatModalMode = "edit";
            EditingDefaultChat = chat;
            ShowDefaultChatSettingsModal = true;
            SelectedServiceId = chat.ServiceId;
            ContactSearchText = "";

            ChatServices = await Http.GetFromJsonAsync<List<ChatServiceItem>>(APIUrl.Endpoint("chat/chat-services")) ?? new();
            SelectedContacts = ChatContacts.Where(c => chat.ContactId.Contains(c.id)).ToList();
            StateHasChanged();
        }

        private bool ShowDefaultChatSettingsModal = false;
        private List<ChatServiceItem> ChatServices = new();
        private int? SelectedServiceId = null;
        private List<ChatContactItem> ChatContacts = new();
        private List<ChatContactItem> SelectedContacts = new();
        private string ContactSearchText = "";
        private string DefaultChatModalMode = "add";
        private DefaultChatItem? EditingDefaultChat = null;
        private string DefaultChatSuccessText = "Default Chat berhasil dibuat";
        private string WahaStoppedModalMode = "Waha";
        private string? DefaultChatServiceName = null;

        private async void OpenDefaultChatSettingsModal()
        {
            DefaultChatModalMode = "add";
            EditingDefaultChat = null;
            ShowDefaultChatSettingsModal = true;
            SelectedServiceId = null;
            SelectedContacts.Clear();
            ContactSearchText = "";

            ChatServices = await Http.GetFromJsonAsync<List<ChatServiceItem>>(APIUrl.Endpoint("chat/chat-services")) ?? new();
            StateHasChanged();
        }

        private void CancelDefaultChatSettings()
        {
            ShowDefaultChatSettingsModal = false;
            SelectedServiceId = null;
            SelectedContacts.Clear();
            ContactSearchText = "";
        }

        private async void ConfirmDefaultChatSettings()
        {
            if (SelectedServiceId.HasValue && SelectedContacts.Count > 0)
            {
                var payload = new
                {
                    chatServiceid = SelectedServiceId.Value,
                    chatContactid = SelectedContacts.Select(c => c.id).ToList(),
                    contactName = SelectedContacts.Select(c => c.name).ToList()
                };

                HttpResponseMessage res;
                if (DefaultChatModalMode == "add")
                {
                    res = await Http.PostAsJsonAsync(APIUrl.Endpoint("chat/default-chats/add"), payload);
                    DefaultChatSuccessText = "Default Chat berhasil dibuat.";
                }
                else // edit
                {
                    res = await Http.PutAsJsonAsync(APIUrl.Endpoint($"chat/default-chats/{EditingDefaultChat?.Id}"), payload);
                    DefaultChatSuccessText = "Default Chat berhasil diubah.";
                }

                if (res.IsSuccessStatusCode)
                {
                    ShowDefaultChatSettingsModal = false;
                    SelectedServiceId = null;
                    SelectedContacts.Clear();
                    ContactSearchText = "";
                    SuccessModalText = DefaultChatSuccessText;
                    await AlertService.ShowSuccessAsync(SuccessModalText);
                    await LoadDefaultChats();
                    await LoadDefaultChatDetails();
                    await LoadContacts();
                }
                else if (res.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var errorText = await res.Content.ReadAsStringAsync();
                    if (errorText.Contains("DefaultChat dengan service yang sama sudah ada.", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowDefaultChatSettingsModal = false;
                        WahaStoppedModalMode = "DefaultChat";
                        DefaultChatServiceName = ChatServices.FirstOrDefault(s => s.id == SelectedServiceId)?.service_name ?? "";
                        ShowWahaStoppedAlert();
                        StateHasChanged();
                        return;
                    }
                    await AlertService.ShowErrorAsync("Gagal menyimpan Default Chat. Silakan coba lagi.");
                }
            }
        }

        // --- Contact Multi-Select Logic ---
        private void AddContact(ChatContactItem contact)
        {
            if (!SelectedContacts.Any(c => c.id == contact.id))
                SelectedContacts.Add(contact);
        }

        private void RemoveContact(ChatContactItem contact)
        {
            SelectedContacts.RemoveAll(c => c.id == contact.id);
        }

        private List<ChatContactItem> FilteredContacts =>
            string.IsNullOrWhiteSpace(ContactSearchText)
                ? ChatContacts.Where(c => !SelectedContacts.Any(s => s.id == c.id)).ToList()
                : ChatContacts.Where(c =>
                    !SelectedContacts.Any(s => s.id == c.id) &&
                    (c.name?.Contains(ContactSearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (c.id?.Contains(ContactSearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();

        private bool ShowContactDropdown = false;
        private void ToggleContactDropdown()
        {
            ShowContactDropdown = !ShowContactDropdown;
        }

        private async Task OnContactSearchChanged(ChangeEventArgs e)
        {
            ContactSearchText = e.Value?.ToString() ?? "";
            ShowContactDropdown = true;
            StateHasChanged();
        }

        [JSInvokable]
        public void CloseContactDropdown()
        {
            ShowContactDropdown = false;
            StateHasChanged();
        }

        private bool ShowSendReminderModal = false;
        private List<string> ReminderChatNamesList = new();
        private List<string> ReminderChatIdsList = new();
        private int CurrentReminderIndex = 0;
        private bool IsSendingReminder = false;
        private string CurrentReminderChatName => ReminderChatNamesList.ElementAtOrDefault(CurrentReminderIndex) ?? "";
        private string CurrentReminderChatId => ReminderChatIdsList.ElementAtOrDefault(CurrentReminderIndex) ?? "";
        private Dictionary<string, string> ReminderMessageCache = new();
        private int? SendingTestDefaultChatId = null;
        private string? SelectedReminderServiceName = null;
        private List<string> ReminderServiceNames = new();

        private async void OnSendReminderClicked()
        {
            ShowSendReminderModal = true;
            ReminderServiceNames = DefaultChats.Select(x => x.ServiceName).Distinct().ToList();
            SelectedReminderServiceName = ReminderServiceNames.FirstOrDefault();
            await UpdateReminderContactsAndMessages();
        }

        private async Task UpdateReminderContactsAndMessages()
        {
            ReminderChatNamesList.Clear();
            ReminderChatIdsList.Clear();
            ReminderMessageCache.Clear();
            CurrentReminderIndex = 0;

            if (string.IsNullOrEmpty(SelectedReminderServiceName))
                return;

            var selectedChats = DefaultChats.Where(x => x.ServiceName == SelectedReminderServiceName).ToList();
            ReminderChatNamesList = selectedChats.SelectMany(x => x.ContactName).Distinct().ToList();
            ReminderChatIdsList = selectedChats.SelectMany(x => x.ContactId).Distinct().ToList();

            foreach (var chatId in ReminderChatIdsList)
            {
                var fullMsg = GetDefaultMessage(chatId, SelectedReminderServiceName);
                var commonInner = ExtractCommonInner(fullMsg);
                ReminderMessageCache[chatId] = commonInner ?? "";
            }
            UpdateReminderMessage();
            StateHasChanged();
        }

        private async Task OnReminderServiceChanged()
        {
            await UpdateReminderContactsAndMessages();
        }

        private void PrevReminderChat()
        {
            if (CurrentReminderIndex > 0)
            {
                CurrentReminderIndex--;
                UpdateReminderMessage();
            }
        }

        private void NextReminderChat()
        {
            if (CurrentReminderIndex < ReminderChatNamesList.Count - 1)
            {
                CurrentReminderIndex++;
                UpdateReminderMessage();
            }
        }

        private void CancelSendReminder()
        {
            ShowSendReminderModal = false;
            ReminderChatNamesList.Clear();
            ReminderChatIdsList.Clear();
            ReminderMessage = "";
            CurrentReminderIndex = 0;
            IsSendingReminder = false;
        }

        private async void SendReminderAsync()
        {
            if (ReminderChatIdsList.Count == 0 || string.IsNullOrEmpty(SelectedReminderServiceName))
                return;

            IsSendingReminder = true;
            try
            {
                var tasks = ReminderChatIdsList.Select(async chatId =>
                {
                    var fullMsg = GetDefaultMessage(chatId, SelectedReminderServiceName);
                    if (ReminderMessageCache.TryGetValue(chatId, out var cachedCommon))
                        fullMsg = ReplaceCommonInner(fullMsg, cachedCommon);

                    var messageToSend = ToDisplayMessage(fullMsg);
                    var payload = new
                    {
                        chatId = chatId,
                        reply_to = (string)null,
                        text = messageToSend,
                        linkPreview = true,
                        linkPreviewHighQuality = false,
                        session = "default"
                    };
                    await Http.PostAsJsonAsync(APIUrl.Endpoint("waha/sendText"), payload);
                });

                await Task.WhenAll(tasks);
                await AlertService.ShowSuccessAsync("Pesan berhasil terkirim.");
                StateHasChanged();
            }
            catch (Exception ex)
            {
                await AlertService.ShowErrorAsync($"Terjadi error saat mengirim pesan:\n{ex.Message}");
            }
            finally
            {
                IsSendingReminder = false;
                ShowSendReminderModal = false;
                ReminderChatNamesList.Clear();
                ReminderChatIdsList.Clear();
                ReminderMessageCache.Clear();
                ReminderMessage = "";
                CurrentReminderIndex = 0;
                StateHasChanged();
            }
        }

        private async Task SendTestTemplateAsync(DefaultChatItem chat)
        {
            var chatIds = chat?.ContactId?.Distinct().ToList() ?? new List<string>();

            if (chatIds.Count == 0)
            {
                await AlertService.ShowErrorAsync("Tidak ada chat ID yang ditemukan.");
                return;
            }

            SendingTestDefaultChatId = chat.Id;
            StateHasChanged();

            try
            {
                var tasks = chatIds.Select(async chatId =>
                {
                    var payload = new { ChatId = chatId };
                    await Http.PostAsJsonAsync(APIUrl.Endpoint("chat/default-chats/test-send"), payload);
                });

                await Task.WhenAll(tasks);
                await AlertService.ShowSuccessAsync("Pesan berhasil terkirim.");
                StateHasChanged();
            }
            catch (Exception ex)
            {
                await AlertService.ShowErrorAsync($"Terjadi error saat mengirim pesan test:\n{ex.Message}");
            }
            finally
            {
                SendingTestDefaultChatId = null;
                StateHasChanged();
            }
        }

        private void OnReminderMessageChanged(ChangeEventArgs e)
        {
            var newRawMessage = e.Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(CurrentReminderChatId))
                return;

            var newCommonInner = ExtractCommonInner(newRawMessage);
            if (string.IsNullOrEmpty(newCommonInner))
                return;
            ReminderMessageCache[CurrentReminderChatId] = newCommonInner;

            foreach (var chatId in ReminderChatIdsList)
            {
                if (string.IsNullOrEmpty(chatId) || chatId == CurrentReminderChatId)
                    continue;

                var oldFullMsg = GetDefaultMessage(chatId);
                var updatedFullMsg = ReplaceCommonInner(oldFullMsg, newCommonInner);

                ReminderMessageCache[chatId] = newCommonInner;
            }
            StateHasChanged();
        }

        private void OnDisplayInputChanged(string displayValue)
        {
            ReminderMessage = displayValue;
            OnReminderMessageChanged(new ChangeEventArgs { Value = ReminderMessageRaw });
        }

        private string ExtractCommonInner(string message)
        {
            var startTag = "[";
            var endTag = "]";
            var start = message.IndexOf(startTag);
            var end = message.IndexOf(endTag);

            if (start >= 0 && end > start)
                return message.Substring(start + startTag.Length, end - (start + startTag.Length));
            return "";
        }

        private string ReplaceCommonInner(string message, string newInner)
        {
            var startTag = "[";
            var endTag = "]";
            var start = message.IndexOf(startTag);
            var end = message.IndexOf(endTag);

            if (start >= 0 && end > start)
            {
                var before = message.Substring(0, start + startTag.Length);
                var after = message.Substring(end);
                return before + newInner + after;
            }
            return $"{startTag}{newInner}{endTag}\n{message}";
        }

        private string GetDefaultMessage(string chatId, string? serviceName = null)
        {
            var detail = DefaultChatDetails
                .FirstOrDefault(d => d.ContactId == chatId && (serviceName == null || d.ServiceName == serviceName));
            return detail?.Template?.Content ?? "";
        }

        private string ToDisplayMessage(string raw) =>
            raw.Replace("[", "").Replace("]", "").Replace("\\n", "\n");

        private string ToRawMessage(string display)
        {
            if (!display.Contains("["))
                return $"[{display}]";
            return display;
        }

        private string _reminderMessageRaw = "";
        private string _reminderMessage = "";

        private string ReminderMessageRaw
        {
            get => _reminderMessageRaw;
            set
            {
                _reminderMessageRaw = value;
                _reminderMessage = ToDisplayMessage(value);
            }
        }

        private string ReminderMessage
        {
            get => _reminderMessage;
            set
            {
                var raw = ToRawMessage(value);
                _reminderMessage = value;
                _reminderMessageRaw = raw;
                ReminderMessageCache[CurrentReminderChatId] = raw;
            }
        }

        private void UpdateReminderMessage()
        {
            var chatId = CurrentReminderChatId;
            var fullMsg = GetDefaultMessage(chatId, SelectedReminderServiceName);

            if (ReminderMessageCache.TryGetValue(chatId, out var cachedCommon))
                fullMsg = ReplaceCommonInner(fullMsg, cachedCommon);
            ReminderMessageRaw = fullMsg;
        }

        private bool ShowCompanyModal = false;
        private string ShowCompanyModalMode = "add";
        private int? EditCompanyId = null;

        private string CoordinateInput = "";
        private bool IsCompanyValid =>
        ShowCompanyModalMode == "edit"
            ? !string.IsNullOrWhiteSpace(NewCompany?.Name)
            : !string.IsNullOrWhiteSpace(NewCompany?.Name)
                && !string.IsNullOrWhiteSpace(NewCompany?.Address)
                && !string.IsNullOrWhiteSpace(NewCompany?.Lat)
                && !string.IsNullOrWhiteSpace(NewCompany?.Long);

        private CompanyModel NewCompany = new();
        private const string LocationIQKey = "pk.e2145fd6b15e111a0fddb4586b415ed0";

        [Inject] private IJSRuntime JSRuntime { get; set; }
        private DotNetObjectReference<Participant>? _dotNetRef;
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

        private async Task OnEditCompanyClicked(string[] companyRow)
        {
            if (companyRow == null || companyRow.Length == 0) return;
            if (!int.TryParse(companyRow[0], out var companyId)) return;

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

                NewCompany = new CompanyModel
                {
                    Name = result.company.name ?? string.Empty
                };

                ShowCompanyModalMode = "edit";
                EditCompanyId = result.company.id;
                ShowCompanyModal = true;

                StateHasChanged();
                await Task.Yield();
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
            if (ShowCompanyModalMode == "add")
            {
                var payload = new
                {
                    name = NewCompany.Name,
                    address = NewCompany.Address,
                    lat = NewCompany.Lat,
                    @long = NewCompany.Long
                };
                var response = await Http.PostAsJsonAsync(APIUrl.Endpoint("data/companies/add"), payload);

                if (response.IsSuccessStatusCode)
                {
                    ShowCompanyModal = false;
                    await AlertService.ShowSuccessAsync("Perusahaan baru berhasil ditambahkan.");
                    await LoadData();
                }
            }
            else
            {
                if (!EditCompanyId.HasValue)
                {
                    await AlertService.ShowErrorAsync("Company id tidak ditemukan.");
                    return;
                }
                var payload = new
                {
                    name = NewCompany.Name
                };

                var response = await Http.PutAsJsonAsync(APIUrl.Endpoint($"data/companies/{EditCompanyId}"), payload);
                if (response.IsSuccessStatusCode)
                {
                    CancelAddCompany();
                    await AlertService.ShowSuccessAsync("Data perusahaan berhasil diubah.");
                    await LoadData();
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

        private bool ShowResetLocationModal = false;
        private int? ResetLocationStudentId = null;
        private string ResetLocationStudentName = "";

        private void OnShowResetLocationModal(string[] item)
        {
            ResetLocationStudentId = int.Parse(item[0]);
            ResetLocationStudentName = item[2];
            ShowResetLocationModal = true;
            StateHasChanged();
        }

        private void CancelResetLocation()
        {
            ShowResetLocationModal = false;
            ResetLocationStudentId = null;
            ResetLocationStudentName = "";
            StateHasChanged();
        }

        private async void ConfirmResetLocation()
        {
            if (!ResetLocationStudentId.HasValue)
                return;

            ShowResetLocationModal = false;
            var request = new HttpRequestMessage(HttpMethod.Delete, APIUrl.Endpoint("data/location/reset"))
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(ResetLocationStudentId.Value), System.Text.Encoding.UTF8, "application/json")
            };
            var response = await Http.SendAsync(request);

            ResetLocationStudentId = null;
            ResetLocationStudentName = "";

            if (response.IsSuccessStatusCode)
            {
                await AlertService.ShowSuccessAsync("Data Lokasi Presensi milik siswa tersebut berhasil dihapus.");
                await LoadData();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await AlertService.ShowInfoAsync("Siswa tersebut tidak memiliki data Lokasi Presensi yang tersimpan.");
            }
            else
            {
                await AlertService.ShowErrorAsync("Gagal mereset lokasi presensi siswa.");
            }
        }

        private List<int> GetCurrentStudentIdsForDetail()
        {
            if (CurrentTab == "Siswa TKJ")
                return GetPagedSortedData().Select(item => int.Parse(item[0])).ToList();
            return new List<int>();
        }

        private int GetCurrentStudentDetailIndex()
        {
            var ids = GetCurrentStudentIdsForDetail();
            if (SelectedStudentDetail == null)
                return -1;
            return ids.IndexOf(SelectedStudentDetail.id);
        }

        private void ShowPrevStudent()
        {
            var ids = GetCurrentStudentIdsForDetail();
            var idx = GetCurrentStudentDetailIndex();
            if (idx > 0)
            {
                var prevId = ids[idx - 1];
                OnShowStudentDetail(prevId);
            }
        }

        private bool IsPrevStudentDisabled
        {
            get
            {
                var idx = GetCurrentStudentDetailIndex();
                return idx <= 0;
            }
        }

        private void ShowNextStudent()
        {
            var ids = GetCurrentStudentIdsForDetail();
            var idx = GetCurrentStudentDetailIndex();
            if (idx >= 0 && idx < ids.Count - 1)
            {
                var nextId = ids[idx + 1];
                OnShowStudentDetail(nextId);
            }
        }

        private bool IsNextStudentDisabled
        {
            get
            {
                var ids = GetCurrentStudentIdsForDetail();
                var idx = GetCurrentStudentDetailIndex();
                return idx == -1 || idx >= ids.Count - 1;
            }
        }

        private void GoToLocation(string[] item)
        {
            CompanyState.CompanyId = int.Parse(item[0]);
            Navigation.NavigateTo("/participant/location");
        }
    }
}