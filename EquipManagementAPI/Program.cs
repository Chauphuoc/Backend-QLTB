using System.Text;
using EquipManagementAPI.Data;
using EquipManagementAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

// Cấu hình DbContext với SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultSqlConnection")));

// Cấu hình JWT từ appsettings
var jwtConfigSection = configuration.GetSection("JwtConfig");
builder.Services.Configure<JwtConfig>(jwtConfigSection);

string secretKey = configuration["JwtConfig:SecretKey"] ?? throw new InvalidOperationException("JWT Secret Key is not configured");
string issuer = configuration["JwtConfig:Issuer"] ?? throw new InvalidOperationException("JWT Issuer is not configured");
string audience = configuration["JwtConfig:Audience"] ?? throw new InvalidOperationException("JWT Audience is not configured");

// Cấu hình Authentication với JWT Bearer
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Bearer";
    options.DefaultChallengeScheme = "Bearer";
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Đăng ký các service tùy chỉnh
builder.Services.AddScoped<IDocEntryService, DocEntryService>();
builder.Services.AddScoped<IAssetService, AssetService>();

// Thêm controller, Swagger và CORS
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EquipManagementAPI",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập 'Bearer {token}' vào đây. Ví dụ: Bearer eyJhbGciOiJIUzI1NiIsInR..."
    });

    var securityRequirement = new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    };

    c.AddSecurityRequirement(securityRequirement);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

// Cấu hình static files cho thư mục lưu ảnh
// Cho phép truy cập file tĩnh trong thư mục wwwroot (nếu có)
app.UseStaticFiles();

// Cho phép truy cập thư mục "Images" trong root project
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "Images")),
    RequestPath = "/Images"
});
//string imagePath = configuration["ImageStoragePath"];
//if (!string.IsNullOrEmpty(imagePath))
//{
//    app.UseStaticFiles(new StaticFileOptions
//    {
//        FileProvider = new PhysicalFileProvider(imagePath),
//        RequestPath = "/Upload"
//    });
//}



app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "EquipManagementAPI v1");
    c.RoutePrefix = "swagger";
});

app.UseExceptionHandler("/error"); // bạn có thể thay đổi đường dẫn xử lý lỗi nếu muốn

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
