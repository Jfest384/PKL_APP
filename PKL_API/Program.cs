using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PKL_API;
using PKL_API.Helpers;
using Syncfusion.Licensing;
using System.Globalization;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<PklContext>(opt =>
{
    opt.UseSqlServer("Data Source=localhost\\SQLEXPRESS;Initial Catalog=PKL_APP_TEST;Integrated Security=True;Trust Server Certificate=True");
});

builder.Services.AddHangfire(config =>
{
    config.UseSqlServerStorage("Data Source=localhost\\SQLEXPRESS;Initial Catalog=PKL_APP_TEST;Integrated Security=True;Trust Server Certificate=True");
});

builder.Services.AddAuthentication("Bearer").AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("70158f9055af67cb94c0175b73624a1b198135aeab541cc06c05da81452009a8"))
    };
});

builder.Services.AddSwaggerGen(opt =>
{
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Scheme = "Bearer",
        Type = SecuritySchemeType.Http
    });

    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });

    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PKL API",
        Version = "v1"
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllClients", policy =>
    {
        policy.WithOrigins(
                "https://localhost:7235",
                "https://presensi.smksabdev.my.id",
                "https://presensi-test.smksabdev.my.id",
                "https://39e5d25c0d0b.ngrok-free.app",
                "http://localhost:5125",
                "http://138.138.138.193:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<UserAccessHelper>();
builder.Services.AddScoped<ChatTemplateService>();
builder.Services.AddScoped<WhatsAppJobService>();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("waha", client =>
{
    client.BaseAddress = new Uri("http://138.138.138.193:3000/api/");
});

var cultureInfo = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JEaF1cWWhBYVJzWmFZfVtgdVVMZVxbRHJPIiBoS35Rc0VrWXdccnFVRmRUVkx+VEFd");

var app = builder.Build();
app.UsePathBase("/api");

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "PKL API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseRouting();
app.UseHttpsRedirection();
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
app.UseCors("AllowAllClients");
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api/swagger"))
    {
        await next();
        return;
    }

    var user = context.User;
    if (!user.Identity?.IsAuthenticated ?? true)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Unauthorized: please login.");
        return;
    }

    if (!user.IsInRole("Admin") && !user.IsInRole("Kepala Jurusan"))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Forbidden: Access denied.");
        return;
    }

    await next();
});

app.MapGet("/waha/{**path}", async (HttpContext context, HttpClient http) =>
{
    var path = context.Request.Path.Value?.Replace("/waha/", "");
    var targetUrl = $"http://138.138.138.193:3000/api/{path}";
    var response = await http.GetAsync(targetUrl);
    var content = await response.Content.ReadAsStringAsync();

    context.Response.StatusCode = (int)response.StatusCode;
    await context.Response.WriteAsync(content);
});

app.MapPost("/waha/{**path}", async (HttpContext context, HttpClient http) =>
{
    var path = context.Request.Path.Value?.Replace("/waha/", "");
    var targetUrl = $"http://138.138.138.193:3000/api/{path}";

    using var content = new StreamContent(context.Request.Body);
    foreach (var header in context.Request.Headers)
    {
        if (!content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
        {

        }
    }

    var response = await http.PostAsync(targetUrl, content);
    context.Response.StatusCode = (int)response.StatusCode;
    await context.Response.WriteAsync(await response.Content.ReadAsStringAsync());
});

app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new RoleBasedAuthorizationFilter("Admin", "Kepala Jurusan") }
});
app.MapControllers();
app.Run();