using Hangfire;
using PKL_HangfireWorker.Services;

File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "service-start.log"),
    $"{DateTime.Now}: Worker starting...\n");

var builder = Host.CreateApplicationBuilder(args);

// 🔹 Pastikan appsettings.json terbaca walau service berjalan dari System32
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
    optional: false,
    reloadOnChange: true
);

// 🔹 Registrasikan agar bisa jalan sebagai Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "PKL Hangfire Worker";
});

// 🔹 Tambahkan Hangfire
builder.Services.AddHangfire(config =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    config.UseSqlServerStorage(conn);
});
builder.Services.AddHangfireServer();

// 🔹 Tambahkan HttpClient
builder.Services.AddHttpClient("waha", client =>
{
    var baseUrl = builder.Configuration["HttpClients:waha"];
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddScoped<WhatsAppJobService>();

var app = builder.Build();

try
{
    using (var scope = app.Services.CreateScope())
    {
        var jobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        var jakarta = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        // 🕒 Jadwal otomatis
        jobManager.AddOrUpdate<WhatsAppJobService>(
            "wa-job-pagi",
            job => job.ExecuteAsync(),
            "*/1 * * * *",
            jakarta
        );

        jobManager.AddOrUpdate<WhatsAppJobService>(
            "wa-job-sore",
            job => job.ExecuteAsync(),
            "0 16 * * 1-5",
            jakarta
        );
    }

    File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "service-start.log"),
        $"{DateTime.Now}: Hangfire jobs registered. Running...\n");

    // 🟢 Jalankan aplikasi (blok utama service)
    app.Run();
}
catch (Exception ex)
{
    File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "service-error.log"),
        $"{DateTime.Now}: {ex}\n");
    throw;
}