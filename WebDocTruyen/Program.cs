using Microsoft.EntityFrameworkCore;
using System;
using WebDocTruyen.Models;

// Npgsql: cho phép DateTime.Now (Kind=Local) ghi vào cột timestamp
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Khóa bí mật (vd Supabase:Key) để trong appsettings.Local.json - file này không đưa lên git.
// Khi deploy (Render...) đặt biến môi trường Supabase__Key thay cho file này.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Render (và các host Docker) báo cổng cần mở qua biến môi trường PORT
var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(renderPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
}

// Các store tĩnh (quốc gia, thích truyện, thích bình luận) dùng chung DB Supabase.
// Lỗi kết nối lúc khởi động (vd chưa đặt mật khẩu database) không được làm sập cả web.
try
{
    StoreDb.Init(builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty);
}
catch (Exception ex)
{
    Console.Error.WriteLine("[StoreDb] Không kết nối được database lúc khởi động: " + ex.Message +
        " -> Kiểm tra biến môi trường ConnectionStrings__DefaultConnection (phải có Password=...).");
}

// 1. Đổi sang AddControllersWithViews() để hỗ trợ View (.cshtml) và bỏ qua vòng lặp cycle JSON
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Hỗ trợ CORS cho Mobile App (Expo SDK / React Native)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Đăng ký AppDbContext kết nối PostgreSQL (Supabase)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Đăng ký HttpContextAccessor và Session
builder.Services.AddHttpContextAccessor();

// Lưu ảnh lên Supabase Storage (dùng chung với app mobile)
builder.Services.AddHttpClient();
builder.Services.AddScoped<WebDocTruyen.Services.SupabaseStorageService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sắp xếp lại thứ tự Middleware chuẩn xác của ASP.NET Core
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Bật CORS cho Mobile App kết nối API
app.UseCors("AllowAll");

// UseSession bắt buộc phải nằm sau UseRouting và trước UseAuthorization
app.UseSession();
app.UseAuthorization();

// Định tuyến dành riêng cho khu vực Admin (Đặt lên trước route mặc định)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

// Định tuyến về Home/Index khi mở web
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();