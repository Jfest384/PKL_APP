using Hangfire;
using PKL_HangfireWorker.Services;

File.AppendAllText("service-start.log", $"{DateTime.Now}: Worker starting...\n");

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "PKL Hangfire Worker";
    })
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;
        services.AddHangfire(cfg =>
        {
            var conn = config.GetConnectionString("DefaultConnection");
            cfg.UseSqlServerStorage(conn);
        });
        services.AddHangfireServer();

        services.AddHttpClient("waha", client =>
        {
            var baseUrl = config["HttpClients:waha"];
            client.BaseAddress = new Uri(baseUrl);
        });

        services.AddScoped<WhatsAppJobService>();
    })
    .Build();

using (var scope = builder.Services.CreateScope())
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

try
{
    File.AppendAllText("service-start.log", $"{DateTime.Now}: Running...\n");
    builder.Run();
}
catch (Exception ex)
{
    File.AppendAllText("service-error.log", $"{DateTime.Now}: {ex}\n");
    throw;
}