using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebDocTruyen.Models;

namespace WebDocTruyen.Controllers
{
    [Route("api/GoiDoc")]
    [Route("api/GoiDocApi")]
    [ApiController]
    public class GoiDocApiController : ControllerBase
    {
        private const string CHO = "Chờ thanh toán";
        private const string OK = "Thành công";
        private const string HET_HAN = "Đã hủy";

        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public GoiDocApiController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        private bool CoCauHinhNganHang =>
            !string.IsNullOrWhiteSpace(_config["Payment:BankId"]) &&
            !string.IsNullOrWhiteSpace(_config["Payment:AccountNo"]);

        private int HetHanPhut => int.TryParse(_config["Payment:HetHanPhut"], out var m) && m > 0 ? m : 15;

        // Kích hoạt gói sau khi đã thanh toán (giống DangKyGoiController, idempotent)
        private async Task<bool> KichHoatAsync(LichSuGiaoDich gd)
        {
            if (gd.TrangThai == OK) return true;
            if (gd.TrangThai != CHO) return false;

            var goi = await _context.GoiDocs.FindAsync(gd.MaMụcTieu);
            if (goi == null) return false;

            var now = DateTime.Now;
            var conHan = await _context.NguoiDungGoiDocs
                .Where(x => x.MaNguoiDung == gd.MaNguoiDung && x.TrangThai == "Active" && x.NgayKetThuc > now)
                .OrderByDescending(x => x.NgayKetThuc)
                .FirstOrDefaultAsync();

            var batDau = conHan?.NgayKetThuc ?? now;
            _context.NguoiDungGoiDocs.Add(new NguoiDungGoiDoc
            {
                MaNguoiDung = gd.MaNguoiDung,
                MaGoiDoc = goi.MaGoiDoc,
                NgayBatDau = batDau,
                NgayKetThuc = batDau.AddDays(goi.SoNgayHieuLuc),
                TrangThai = "Active"
            });
            gd.TrangThai = OK;
            await _context.SaveChangesAsync();
            return true;
        }

        public class TaoThanhToanRequest
        {
            public int MaNguoiDung { get; set; }
            public int MaGoiDoc { get; set; }
            public string? PhuongThuc { get; set; } = "VNPAY";
        }

        // POST: api/GoiDoc/TaoThanhToan - tạo giao dịch chờ + dữ liệu mã QR (cùng quy trình với web /DangKyGoi/TaoThanhToan)
        [HttpPost("TaoThanhToan")]
        public async Task<IActionResult> TaoThanhToan([FromBody] TaoThanhToanRequest req)
        {
            var user = req.MaNguoiDung > 0 ? await _context.NguoiDungs.FindAsync(req.MaNguoiDung) : null;
            if (user == null || user.IsKhoa)
                return Unauthorized(new { success = false, message = "Vui lòng đăng nhập để đăng ký gói." });

            var goi = await _context.GoiDocs.FindAsync(req.MaGoiDoc);
            if (goi == null || goi.IsHoatDong != true)
                return NotFound(new { success = false, message = "Gói đọc không tồn tại hoặc đã ngừng bán." });

            var cu = await _context.LichSuGiaoDiches
                .Where(g => g.MaNguoiDung == user.MaNguoiDung && g.LoaiGiaoDich == "GoiDoc" && g.TrangThai == CHO)
                .ToListAsync();
            foreach (var c in cu) c.TrangThai = HET_HAN;

            var gd = new LichSuGiaoDich
            {
                MaNguoiDung = user.MaNguoiDung,
                LoaiGiaoDich = "GoiDoc",
                MaMụcTieu = goi.MaGoiDoc,
                SoTien = goi.GiaTien,
                PhuongThucThanhToan = (req.PhuongThuc?.Trim().ToLower()) == "paypal" ? "PayPal" : "VNPAY",
                TrangThai = CHO,
                NgayTao = DateTime.Now
            };
            _context.LichSuGiaoDiches.Add(gd);
            await _context.SaveChangesAsync();

            string noiDung = $"GOIDOC{gd.MaGiaoDich}";
            string? qrImage = null;
            if (CoCauHinhNganHang)
            {
                qrImage = $"https://img.vietqr.io/image/{_config["Payment:BankId"]}-{_config["Payment:AccountNo"]}-compact2.png" +
                          $"?amount={(long)gd.SoTien}&addInfo={Uri.EscapeDataString(noiDung)}" +
                          $"&accountName={Uri.EscapeDataString(_config["Payment:AccountName"] ?? "")}";
            }

            return Ok(new
            {
                success = true,
                maGiaoDich = gd.MaGiaoDich,
                tenGoi = goi.TenGoi,
                soTien = gd.SoTien,
                soTienFormatted = gd.SoTien.ToString("N0") + " đ",
                phuongThuc = gd.PhuongThucThanhToan,
                isPayPal = false,
                noiDung,
                qrImage,
                demo = !CoCauHinhNganHang,
                taiKhoan = CoCauHinhNganHang ? _config["Payment:AccountNo"] : null,
                tenTaiKhoan = CoCauHinhNganHang ? _config["Payment:AccountName"] : null,
                hetHanGiay = HetHanPhut * 60
            });
        }

        // GET: api/GoiDoc/TrangThai/{id}?userId=... - app polling trạng thái thanh toán
        [HttpGet("TrangThai/{id:int}")]
        public async Task<IActionResult> TrangThai(int id, [FromQuery] int userId)
        {
            var gd = await _context.LichSuGiaoDiches
                .FirstOrDefaultAsync(g => g.MaGiaoDich == id && g.MaNguoiDung == userId && g.LoaiGiaoDich == "GoiDoc");
            if (gd == null) return NotFound(new { success = false, message = "Không tìm thấy giao dịch." });

            if (gd.TrangThai == CHO && (DateTime.Now - (gd.NgayTao ?? DateTime.Now)).TotalMinutes > HetHanPhut)
            {
                gd.TrangThai = HET_HAN;
                await _context.SaveChangesAsync();
            }

            return Ok(new { trangThai = gd.TrangThai, daThanhToan = gd.TrangThai == OK, hetHan = gd.TrangThai == HET_HAN });
        }

        public class XacNhanRequest
        {
            public int MaNguoiDung { get; set; }
            public int MaGiaoDich { get; set; }
        }

        // POST: api/GoiDoc/XacNhan - xác nhận đã chuyển khoản (chế độ demo, giống /DangKyGoi/KichHoatTest của web)
        [HttpPost("XacNhan")]
        public async Task<IActionResult> XacNhan([FromBody] XacNhanRequest req)
        {
            var gd = await _context.LichSuGiaoDiches
                .FirstOrDefaultAsync(g => g.MaGiaoDich == req.MaGiaoDich && g.MaNguoiDung == req.MaNguoiDung && g.LoaiGiaoDich == "GoiDoc");
            if (gd == null) return NotFound(new { success = false, message = "Không tìm thấy giao dịch." });

            if (gd.TrangThai == CHO && (DateTime.Now - (gd.NgayTao ?? DateTime.Now)).TotalMinutes > HetHanPhut)
            {
                gd.TrangThai = HET_HAN;
                await _context.SaveChangesAsync();
            }

            bool ok = await KichHoatAsync(gd);
            return Ok(new { success = ok, message = ok ? "Thành công" : "Giao dịch đã hết hạn hoặc không hợp lệ." });
        }

        // 1. GET: api/GoiDoc (Lấy danh sách các gói đọc VIP đang hoạt động)
        [HttpGet]
        public async Task<IActionResult> GetGoiDocs()
        {
            var dsGoiDoc = await _context.GoiDocs
                .Where(g => g.IsHoatDong == true)
                .OrderBy(g => g.GiaTien)
                .Select(g => new
                {
                    maGoiDoc = g.MaGoiDoc,
                    tenGoi = g.TenGoi,
                    soNgayHieuLuc = g.SoNgayHieuLuc,
                    giaTien = g.GiaTien,
                    moTa = g.MoTa
                })
                .ToListAsync();

            return Ok(dsGoiDoc);
        }

        // 2. GET: api/GoiDoc/UserVip?userId=5 (Kiểm tra gói VIP của người dùng trong CSDL)
        [HttpGet("UserVip")]
        public async Task<IActionResult> GetUserVip([FromQuery] int userId)
        {
            if (userId <= 0) return BadRequest(new { isVip = false, message = "UserId không hợp lệ." });

            var vip = await _context.NguoiDungGoiDocs
                .AsNoTracking()
                .Include(g => g.MaGoiDocNavigation)
                .Where(g => g.MaNguoiDung == userId && g.TrangThai == "Active" && g.NgayKetThuc > DateTime.Now)
                .OrderByDescending(g => g.NgayKetThuc)
                .FirstOrDefaultAsync();

            if (vip == null)
            {
                // Kiểm tra xem có phải tài khoản Admin (mặc định hưởng quyền VIP)
                var user = await _context.NguoiDungs.FindAsync(userId);
                if (user != null && user.MaVaiTro == 1)
                {
                    return Ok(new
                    {
                        isVip = true,
                        tenGoi = "Admin VIP Vĩnh Viễn",
                        ngayKetThuc = DateTime.Now.AddYears(10),
                        soNgayConLai = 3650
                    });
                }
                return Ok(new { isVip = false });
            }

            return Ok(new
            {
                isVip = true,
                tenGoi = vip.MaGoiDocNavigation?.TenGoi ?? "Gói VIP",
                ngayKetThuc = vip.NgayKetThuc,
                soNgayConLai = (int)Math.Max(0, (vip.NgayKetThuc - DateTime.Now).TotalDays)
            });
        }

        // 3. POST: api/GoiDoc/KichHoat (Kích hoạt gói VIP lưu trực tiếp vào SQL Server)
        public class KichHoatGoiRequest
        {
            public int MaNguoiDung { get; set; }
            public int MaGoiDoc { get; set; }
            public string? PhuongThuc { get; set; } = "VietQR";
        }

        [HttpPost("KichHoat")]
        public async Task<IActionResult> KichHoatGoi([FromBody] KichHoatGoiRequest req)
        {
            if (req.MaNguoiDung <= 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng đăng nhập để kích hoạt gói VIP." });
            }

            var goi = await _context.GoiDocs.FindAsync(req.MaGoiDoc);
            if (goi == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy gói đọc tương ứng." });
            }

            var now = DateTime.Now;
            var conHan = await _context.NguoiDungGoiDocs
                .Where(x => x.MaNguoiDung == req.MaNguoiDung && x.TrangThai == "Active" && x.NgayKetThuc > now)
                .OrderByDescending(x => x.NgayKetThuc)
                .FirstOrDefaultAsync();

            var batDau = conHan?.NgayKetThuc ?? now;
            var ketThuc = batDau.AddDays(goi.SoNgayHieuLuc);

            var ndgd = new NguoiDungGoiDoc
            {
                MaNguoiDung = req.MaNguoiDung,
                MaGoiDoc = goi.MaGoiDoc,
                NgayBatDau = batDau,
                NgayKetThuc = ketThuc,
                TrangThai = "Active"
            };
            _context.NguoiDungGoiDocs.Add(ndgd);

            var gd = new LichSuGiaoDich
            {
                MaNguoiDung = req.MaNguoiDung,
                LoaiGiaoDich = "GoiDoc",
                MaMụcTieu = goi.MaGoiDoc,
                SoTien = goi.GiaTien,
                PhuongThucThanhToan = string.IsNullOrEmpty(req.PhuongThuc) ? "VietQR" : req.PhuongThuc,
                TrangThai = "Thành công",
                NgayTao = now
            };
            _context.LichSuGiaoDiches.Add(gd);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Kích hoạt thành công {goi.TenGoi}!",
                isVip = true,
                tenGoiVip = goi.TenGoi,
                hanGoiVip = ketThuc.ToString("o")
            });
        }
    }
}
