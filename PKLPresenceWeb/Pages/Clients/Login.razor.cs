using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using PKLPresenceWeb.Model;
using System.Net.Http.Json;

namespace PKLPresenceWeb.Pages.Clients
{
    public partial class Login : ComponentBase
    {
        private LoginDTO loginDTO = new()
        {
            username = string.Empty,
            password = string.Empty
        };

        private bool showPassword = false;
        private string? errorMessage;
        private string PasswordInputType => showPassword ? "text" : "password";

        private bool isLoggingIn = false;

        private void TogglePasswordVisibility()
        {
            showPassword = !showPassword;
        }

        private Task PreventDefault(MouseEventArgs e)
        {
            return Task.CompletedTask;
        }

        private async Task LoginUser()
        {
            isLoggingIn = true;
            StateHasChanged();

            try
            {
                errorMessage = null;

                var request = new HttpRequestMessage(HttpMethod.Post, APIUrl.Endpoint("authentication/login"))
                {
                    Content = JsonContent.Create(loginDTO)
                };
                var response = await Http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    if (AuthStateProvider is SimpleAuthStateProvider simpleAuth)
                        await simpleAuth.NotifyAuthenticationStateChanged();

                    var responseMe = await Http.GetAsync(APIUrl.Endpoint("me"));

                    var json = await responseMe.Content.ReadAsStringAsync();
                    await JS.InvokeVoidAsync("localStorage.setItem", "meResponse", json);

                    Navigation.NavigateTo("/home", true);
                }
                else
                {
                    errorMessage = "Login gagal. Periksa username dan password Anda.";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Terjadi kesalahan: " + ex.Message;
            }
            finally
            {
                isLoggingIn = false;
                StateHasChanged();
            }
        }

        private async Task OnInputKeyUp(KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && !isLoggingIn) await LoginUser();
        }
    }
}