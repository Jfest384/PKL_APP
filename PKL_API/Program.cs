using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PKL_API;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<PklContext>(opt =>
{
    opt.UseSqlServer("Data Source=localhost\\SQLEXPRESS;Initial Catalog=PKL_APP;Integrated Security=True;Trust Server Certificate=True");
});

builder.Services.AddAuthentication("Bearer").AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("70158f9055af67cb94c0175b73624a1b198135aeab541cc06c05da81452009a8"))
    };
});

builder.Services.AddSwaggerGen(opt =>
{
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Scheme = "Bearer",
        //BearerFormat = "JWT",
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
                "http://localhost:5125")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserAccessHelper>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PKL API V1");
    });
}

app.UseHttpsRedirection();

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

app.UseCors("AllowAllClients");

//app.UseCors("AllowNgrok");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
