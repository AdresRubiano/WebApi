using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WebApi.Custom;
using WebApi.Models;
using WebApi.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// Configuraci�n de DbContext con SQL Server
builder.Services.AddDbContext<DbMasterContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("CadenaSQL"));
});

// Servicios personalizados
builder.Services.AddSingleton<Utilidades>();
builder.Services.AddScoped<IS3Service, S3Service>();




builder.Services.AddAuthentication(config =>
{
    config.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    config.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    config.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(config =>
{
    config.RequireHttpsMetadata = false;
    config.SaveToken = true;
    config.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey= true,
        ValidateIssuer= false,
        ValidateAudience= false,
        ValidateLifetime=true,
        ClockSkew = TimeSpan.Zero,
        IssuerSigningKey = new SymmetricSecurityKey
        (Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});
builder.Services.AddCors(option =>
{
    option.AddPolicy("NewPolicy", app =>
    {
        app.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });



});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Habilitar archivos estáticos para la interfaz personalizada
app.UseStaticFiles();

// Mantener Swagger solo para generar el JSON de OpenAPI (sin UI)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    // Swagger UI removido - usando interfaz personalizada
}

app.UseCors("NewPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Mapear ruta raíz a la interfaz personalizada
app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html";
    var filePath = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html");
    if (File.Exists(filePath))
    {
        await context.Response.SendFileAsync(filePath);
    }
    else
    {
        await context.Response.WriteAsync("<h1>API Blog - Interfaz no encontrada</h1>");
    }
});

app.Run();
