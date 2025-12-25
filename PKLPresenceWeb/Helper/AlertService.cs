using Microsoft.JSInterop;

namespace PKLPresenceWeb.Helper
{
    public class AlertService
    {
        private readonly IJSRuntime _js;
        public AlertService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task ShowSuccessAsync(string message)
        {
            await _js.InvokeVoidAsync("Swal.fire", new
            {
                title = "Success!",
                text = message,
                icon = "success",
                timer = 3000,
                showConfirmButton = false,
                width = "90%",
                customClass = new { popup = "my-swal-popup" }
            });
        }

        public async Task ShowErrorAsync(string message)
        {
            await _js.InvokeVoidAsync("Swal.fire", new
            {
                title = "Error!",
                text = message,
                icon = "error",
                timer = 3000,
                showConfirmButton = false,
                width = "90%",
                customClass = new { popup = "my-swal-popup" }
            });
        }

        public async Task ShowWarningAsync(string message)
        {
            await _js.InvokeVoidAsync("Swal.fire", new
            {
                title = "Warning!",
                text = message,
                icon = "warning",
                timer = 3500,
                showConfirmButton = false,
                width = "90%",
                customClass = new { popup = "my-swal-popup" }
            });
        }

        public async Task ShowInfoAsync(string message)
        {
            await _js.InvokeVoidAsync("Swal.fire", new
            {
                title = "Info!",
                text = message,
                icon = "info",
                timer = 3000,
                showConfirmButton = false,
                width = "90%",
                customClass = new { popup = "my-swal-popup" }
            });
        }
    }
}
