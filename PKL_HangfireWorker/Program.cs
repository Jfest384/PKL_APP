using Hangfire;
using PKL_HangfireWorker.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddHangfire(config =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    config.UseSqlServerStorage(conn);
});

builder.Services.AddHangfireServer();

builder.Services.AddHttpClient("waha", client =>
{
    var baseUrl = builder.Configuration["HttpClients:waha"];
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddScoped<WhatsAppJobService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var jobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    var jakarta = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

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

Console.WriteLine("✅ Hangfire Worker berjalan...");

// tambahkan ini
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "PKL Hangfire Worker";
});

app.Run();