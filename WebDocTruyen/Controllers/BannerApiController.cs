using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDocTruyen.Models;

namespace WebDocTruyen.Controllers
{
    // API cho app mobile: banner trang chủ (cùng dữ liệu với trang quản lý banner của admin)
    [Route("api/Banner")]
    [ApiController]
    public class BannerApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BannerApiController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Banner
        [HttpGet]
        public async Task<IActionResult> GetBanners()
        {
            var ds = await _context.Banners
                .AsNoTracking()
                .Where(b => b.HoatDong)
                .OrderBy(b => b.ThuTu)
                .ThenByDescending(b => b.MaBanner)
                .Select(b => new
                {
                    maBanner = b.MaBanner,
                    tieuDe = b.TieuDe,
                    duongDanAnh = b.DuongDanAnh,
                    lienKet = b.LienKet,
                    thuTu = b.ThuTu
                })
                .ToListAsync();

            // Giống trang chủ web: chưa có banner nào đang bật thì dùng 3 ảnh mặc định /Images/banner1-3.jpg
            if (ds.Count == 0)
            {
                var macDinh = new[]
                {
                    new { maBanner = -1, tieuDe = "Đại Tiệc Tiên Hiệp", duongDanAnh = "/Images/banner1.jpg", lienKet = (string?)null, thuTu = 1 },
                    new { maBanner = -2, tieuDe = "Đăng Ký Gói Đọc VIP", duongDanAnh = "/Images/banner2.jpg", lienKet = (string?)null, thuTu = 2 },
                    new { maBanner = -3, tieuDe = "Top Truyện Thịnh Hành", duongDanAnh = "/Images/banner3.jpg", lienKet = (string?)null, thuTu = 3 },
                };
                return Ok(macDinh);
            }

            return Ok(ds);
        }
    }
}
