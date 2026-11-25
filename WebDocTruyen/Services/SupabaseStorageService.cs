using System.Net.Http.Headers;

namespace WebDocTruyen.Services
{
    /// <summary>
    /// Lưu ảnh (banner, ảnh bìa, ảnh đại diện) lên Supabase Storage để web và app mobile dùng chung một link ảnh.
    /// Nếu chưa cấu hình hoặc tải lên lỗi thì lưu vào wwwroot như trước để web không bị hỏng chức năng.
    /// Cấu hình trong appsettings.json, mục "Supabase": Url, Key (khuyên dùng secret key), Bucket.
    /// </summary>
    public class SupabaseStorageService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<SupabaseStorageService> _logger;

        private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
            [".gif"] = "image/gif",
        };

        public SupabaseStorageService(IHttpClientFactory httpFactory, IConfiguration config, IWebHostEnvironment env, ILogger<SupabaseStorageService> logger)
        {
            _httpFactory = httpFactory;
            _config = config;
            _env = env;
            _logger = logger;
        }

        private string? Url => _config["Supabase:Url"]?.TrimEnd('/');
        private string? Key => _config["Supabase:Key"];
        private string Bucket => string.IsNullOrWhiteSpace(_config["Supabase:Bucket"]) ? "images" : _config["Supabase:Bucket"]!;

        /// <summary>
        /// Lưu ảnh và trả về đường dẫn để ghi vào database:
        /// link đầy đủ trên Supabase (https://...) hoặc /{thuMucLocal}/ten-file nếu phải lưu trên server.
        /// </summary>
        /// <param name="file">File ảnh người dùng tải lên</param>
        /// <param name="folder">Thư mục trong kho ảnh, vd "banners", "covers", "avatars"</param>
        /// <param name="localFolder">Thư mục dự phòng trong wwwroot, vd "Images" hoặc "uploads/banners"</param>
        public async Task<string?> SaveImageAsync(IFormFile? file, string folder, string localFolder)
        {
            if (file == null || file.Length == 0) return null;

            var ext = Path.GetExtension(file.FileName);
            if (!ContentTypes.TryGetValue(ext, out var contentType))
            {
                ext = ".jpg";
                contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;
            }
            var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";

            if (!string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Key))
            {
                try
                {
                    var objectPath = $"{folder.Trim('/')}/{fileName}";
                    using var content = new StreamContent(file.OpenReadStream());
                    content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                    using var req = new HttpRequestMessage(HttpMethod.Post, $"{Url}/storage/v1/object/{Bucket}/{objectPath}") { Content = content };
                    req.Headers.Add("apikey", Key);
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);
                    req.Headers.Add("x-upsert", "true");

                    var client = _httpFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(60);
                    using var res = await client.SendAsync(req);
                    if (res.IsSuccessStatusCode)
                    {
                        return $"{Url}/storage/v1/object/public/{Bucket}/{objectPath}";
                    }
                    _logger.LogWarning("Tải ảnh lên Supabase thất bại ({Status}): {Body}", (int)res.StatusCode, await res.Content.ReadAsStringAsync());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không tải được ảnh lên Supabase, lưu tạm trên server.");
                }
            }

            // Dự phòng: lưu trong wwwroot như cách cũ
            var dir = Path.Combine(_env.WebRootPath, localFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(dir);
            await using (var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return "/" + localFolder.Trim('/') + "/" + fileName;
        }
    }
}
