using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Helpers;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Areas.Admin.Controllers
{
    public class DonHangController : AdminBaseController
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();
        private const int PageSize = 15;
        private static readonly string[] TrangThaiHopLe = { "ChoXacNhan", "DaXacNhan", "DangGiao", "HoanTat", "DaHuy" };

        // ---------- nhãn hiển thị ----------
        public static string NhanThanhToan(string t)
        {
            switch (t)
            {
                case "ChuaThanhToan": return "Chưa thanh toán";
                case "DaThanhToan": return "Đã thanh toán";
                case "HoanTien": return "Cần hoàn tiền";
                default: return t;
            }
        }
        public static string MauThanhToan(string t)
        {
            switch (t)
            {
                case "DaThanhToan": return "green";
                case "HoanTien": return "red";
                default: return "amber";
            }
        }
        public static string NhanPhuongThuc(string p) { return p == "QR" ? "Chuyển khoản QR" : "Thanh toán khi nhận (COD)"; }

        // ===== 1. DANH SÁCH =====
        public ActionResult Index(string q, string tt, string ttt, string pt, DateTime? tuNgay, DateTime? denNgay, int page = 1)
        {
            var goc = db.DonHangs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                goc = goc.Where(d => d.MaDonHang.Contains(k) || d.TenNguoiNhan.Contains(k) || d.SoDienThoai.Contains(k));
            }
            if (!string.IsNullOrEmpty(ttt)) goc = goc.Where(d => d.TrangThaiThanhToan == ttt);
            if (!string.IsNullOrEmpty(pt)) goc = goc.Where(d => d.PhuongThucThanhToan == pt);
            if (tuNgay.HasValue) { var t = tuNgay.Value.Date; goc = goc.Where(d => d.NgayDat >= t); }
            if (denNgay.HasValue) { var e = denNgay.Value.Date.AddDays(1); goc = goc.Where(d => d.NgayDat < e); }

            var vm = new DonHangIndexVM
            {
                TuKhoa = q,
                TrangThai = tt,
                ThanhToan = ttt,
                PhuongThuc = pt,
                TuNgay = tuNgay.HasValue ? tuNgay.Value.ToString("yyyy-MM-dd") : "",
                DenNgay = denNgay.HasValue ? denNgay.Value.ToString("yyyy-MM-dd") : ""
            };

            // số đơn trên mỗi tab (áp dụng các bộ lọc khác, trừ tab trạng thái)
            var dem = goc.GroupBy(d => d.TrangThai).Select(g => new { g.Key, N = g.Count() }).ToList();
            vm.Dem = dem.ToDictionary(x => x.Key, x => x.N);
            vm.Dem[""] = dem.Sum(x => x.N);

            var query = string.IsNullOrEmpty(tt) ? goc : goc.Where(d => d.TrangThai == tt);
            vm.TongSoDong = query.Count();
            vm.TongSoTrang = (int)Math.Ceiling(vm.TongSoDong / (double)PageSize);
            vm.TrangHienTai = Math.Max(1, Math.Min(page, Math.Max(1, vm.TongSoTrang)));

            vm.Dong = query
                .OrderByDescending(d => d.NgayDat).ThenByDescending(d => d.Id)
                .Skip((vm.TrangHienTai - 1) * PageSize).Take(PageSize)
                .Select(d => new DonHangDongVM
                {
                    Id = d.Id,
                    MaDonHang = d.MaDonHang,
                    TenNguoiNhan = d.TenNguoiNhan,
                    SoDienThoai = d.SoDienThoai,
                    NgayDat = d.NgayDat,
                    PhuongThuc = d.PhuongThucThanhToan,
                    TrangThaiThanhToan = d.TrangThaiThanhToan,
                    TrangThai = d.TrangThai,
                    Tong = d.TongTienHang + d.PhiShip - d.TienGiam
                }).ToList();

            return View(vm);
        }

        // ===== 2. CHI TIẾT =====
        public ActionResult ChiTiet(int id)
        {
            var vm = TaiChiTiet(id);
            if (vm == null) return HttpNotFound();
            return View(vm);
        }

        private DonHangChiTietVM TaiChiTiet(int id)
        {
            var vm = db.DonHangs.Where(d => d.Id == id).Select(d => new DonHangChiTietVM
            {
                Id = d.Id,
                MaDonHang = d.MaDonHang,
                TaiKhoan = d.NguoiDung.HoTen,
                TenNguoiNhan = d.TenNguoiNhan,
                SoDienThoai = d.SoDienThoai,
                Email = d.Email,
                DiaChiGiao = d.DiaChiGiao,
                TinhThanh = d.TinhThanh,
                GhiChu = d.GhiChu,
                MaKhuyenMai = d.KhuyenMai.MaCode,
                TongTienHang = d.TongTienHang,
                PhiShip = d.PhiShip,
                TienGiam = d.TienGiam,
                PhuongThuc = d.PhuongThucThanhToan,
                TrangThaiThanhToan = d.TrangThaiThanhToan,
                TrangThai = d.TrangThai,
                NgayDat = d.NgayDat,
                NgayThanhToan = d.NgayThanhToan,
                NgayGiaoXong = d.NgayGiaoXong
            }).FirstOrDefault();
            if (vm == null) return null;

            // sản phẩm + IMEI
            vm.Items = db.ChiTietDonHangs.Where(c => c.DonHangId == id)
                .Select(c => new DongHangVM
                {
                    Id = c.Id,
                    Ten = c.TenSanPham,
                    SoLuong = c.SoLuong,
                    DonGia = c.DonGia,
                    LaDienThoai = c.BienTheSanPham.SanPham.LoaiId == 1
                }).ToList();
            var ctIds = vm.Items.Select(x => x.Id).ToList();
            var imeis = db.ImeiSanPhams.Where(i => ctIds.Contains(i.ChiTietDonHangId))
                .Select(i => new { i.ChiTietDonHangId, i.Imei }).ToList();
            foreach (var it in vm.Items)
                it.Imeis = imeis.Where(x => x.ChiTietDonHangId == it.Id).Select(x => x.Imei.Trim()).ToList();

            // timeline + giao dịch (kèm tên người thao tác)
            var ls = db.DonHangLichSus.Where(h => h.DonHangId == id).OrderByDescending(h => h.NgayCapNhat).ThenByDescending(h => h.Id).ToList();
            var gd = db.ThanhToanGiaoDiches.Where(g => g.DonHangId == id).OrderByDescending(g => g.NgayThanhToan).ToList();
            var nvIds = ls.Where(x => x.NguoiCapNhatId.HasValue).Select(x => x.NguoiCapNhatId.Value)
                .Concat(gd.Where(x => x.NguoiXacNhanId.HasValue).Select(x => x.NguoiXacNhanId.Value)).Distinct().ToList();
            var ten = db.NguoiDungs.Where(n => nvIds.Contains(n.Id)).ToDictionary(n => n.Id, n => n.HoTen);
            Func<int?, string> tenNv = i => (i.HasValue && ten.ContainsKey(i.Value)) ? ten[i.Value] : "";

            vm.LichSu = ls.Select(h => new LichSuTrangThaiVM
            {
                TrangThai = h.TrangThai,
                GhiChu = h.GhiChu,
                Ngay = h.NgayCapNhat,
                NguoiCapNhat = tenNv(h.NguoiCapNhatId)
            }).ToList();
            vm.GiaoDich = gd.Select(g => new GiaoDichVM
            {
                PhuongThuc = g.PhuongThuc,
                SoTien = g.SoTien,
                MaGiaoDich = g.MaGiaoDich,
                Ngay = g.NgayThanhToan,
                NguoiXacNhan = tenNv(g.NguoiXacNhanId)
            }).ToList();

            return vm;
        }

        // ===== 3. ĐỔI TRẠNG THÁI (sp_CapNhatTrangThaiDon) =====
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CapNhatTrangThai(int id, string trangThaiMoi, string ghiChu)
        {
            if (!TrangThaiHopLe.Contains(trangThaiMoi))
            {
                TempData["Err"] = "Trạng thái không hợp lệ.";
                return RedirectToAction("ChiTiet", new { id });
            }
            ghiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();
            if (ghiChu != null && ghiChu.Length > 200) ghiChu = ghiChu.Substring(0, 200);

            try
            {
                db.Database.ExecuteSqlCommand(
                    "EXEC sp_CapNhatTrangThaiDon @DonHangId, @TrangThaiMoi, @NguoiCapNhatId, @GhiChu",
                    new SqlParameter("@DonHangId", id),
                    new SqlParameter("@TrangThaiMoi", trangThaiMoi),
                    new SqlParameter("@NguoiCapNhatId", AuthHelper.UserId.Value),
                    new SqlParameter("@GhiChu", (object)ghiChu ?? DBNull.Value));
                TempData["Msg"] = "Đã chuyển đơn sang trạng thái: " + ThongKeController.NhanTrangThai(trangThaiMoi) + ".";
            }
            catch (Exception ex) { TempData["Err"] = LoiSql(ex); }
            return RedirectToAction("ChiTiet", new { id });
        }

        // ===== 4. XÁC NHẬN ĐÃ NHẬN TIỀN QR (sp_XacNhanThanhToan) =====
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult XacNhanThanhToan(int id, string maGiaoDich)
        {
            maGiaoDich = string.IsNullOrWhiteSpace(maGiaoDich) ? null : maGiaoDich.Trim();
            if (maGiaoDich != null && maGiaoDich.Length > 50)
            {
                TempData["Err"] = "Mã giao dịch tối đa 50 ký tự.";
                return RedirectToAction("ChiTiet", new { id });
            }
            try
            {
                db.Database.ExecuteSqlCommand(
                    "EXEC sp_XacNhanThanhToan @DonHangId, @NguoiXacNhanId, @MaGiaoDich",
                    new SqlParameter("@DonHangId", id),
                    new SqlParameter("@NguoiXacNhanId", AuthHelper.UserId.Value),
                    new SqlParameter("@MaGiaoDich", (object)maGiaoDich ?? DBNull.Value));
                TempData["Msg"] = "Đã xác nhận nhận tiền cho đơn hàng.";
            }
            catch (Exception ex) { TempData["Err"] = LoiSql(ex); }
            return RedirectToAction("ChiTiet", new { id });
        }

        // ===== 5. GÁN IMEI (sp_GanImei) =====
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult GanImei(int chiTietId, string imei)
        {
            var donId = db.ChiTietDonHangs.Where(c => c.Id == chiTietId).Select(c => (int?)c.DonHangId).FirstOrDefault();
            if (donId == null) return HttpNotFound();

            imei = (imei ?? "").Trim();
            if (!Regex.IsMatch(imei, @"^\d{15}$"))
            {
                TempData["Err"] = "IMEI phải gồm đúng 15 chữ số.";
                return RedirectToAction("ChiTiet", new { id = donId.Value });
            }
            try
            {
                db.Database.ExecuteSqlCommand(
                    "EXEC sp_GanImei @ChiTietDonHangId, @Imei",
                    new SqlParameter("@ChiTietDonHangId", chiTietId),
                    new SqlParameter("@Imei", imei));
                TempData["Msg"] = "Đã gán IMEI " + imei + ".";
            }
            catch (Exception ex) { TempData["Err"] = LoiSql(ex); }
            return RedirectToAction("ChiTiet", new { id = donId.Value });
        }

        // lỗi nghiệp vụ trong SQL dùng THROW 5xxxx nên hiển thị được thẳng cho nhân viên
        private static string LoiSql(Exception ex)
        {
            var sql = ex as SqlException ?? ex.InnerException as SqlException;
            if (sql != null && sql.Number >= 50000) return sql.Message;
            if (sql != null && (sql.Number == 2627 || sql.Number == 2601)) return "IMEI này đã được gán cho một máy khác.";
            return "Thao tác thất bại, vui lòng thử lại.";
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }

    // ================= VIEW MODELS =================
    public class DonHangDongVM
    {
        public int Id { get; set; }
        public string MaDonHang { get; set; }
        public string TenNguoiNhan { get; set; }
        public string SoDienThoai { get; set; }
        public DateTime NgayDat { get; set; }
        public string PhuongThuc { get; set; }
        public string TrangThaiThanhToan { get; set; }
        public string TrangThai { get; set; }
        public decimal Tong { get; set; }
        public string TenTrangThai { get { return ThongKeController.NhanTrangThai(TrangThai); } }
        public string MauTrangThai { get { return ThongKeController.MauTrangThai(TrangThai); } }
        public string TenThanhToan { get { return DonHangController.NhanThanhToan(TrangThaiThanhToan); } }
        public string MauThanhToan { get { return DonHangController.MauThanhToan(TrangThaiThanhToan); } }
    }
    public class DonHangIndexVM
    {
        public string TuKhoa { get; set; }
        public string TrangThai { get; set; }
        public string ThanhToan { get; set; }
        public string PhuongThuc { get; set; }
        public string TuNgay { get; set; }
        public string DenNgay { get; set; }
        public Dictionary<string, int> Dem { get; set; }
        public int TongSoDong { get; set; }
        public int TrangHienTai { get; set; }
        public int TongSoTrang { get; set; }
        public List<DonHangDongVM> Dong { get; set; }
    }
    public class DongHangVM
    {
        public int Id { get; set; }
        public string Ten { get; set; }
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public bool LaDienThoai { get; set; }
        public List<string> Imeis { get; set; }
    }
    public class LichSuTrangThaiVM
    {
        public string TrangThai { get; set; }
        public string GhiChu { get; set; }
        public DateTime Ngay { get; set; }
        public string NguoiCapNhat { get; set; }
        public string Ten { get { return ThongKeController.NhanTrangThai(TrangThai); } }
    }
    public class GiaoDichVM
    {
        public string PhuongThuc { get; set; }
        public decimal SoTien { get; set; }
        public string MaGiaoDich { get; set; }
        public DateTime Ngay { get; set; }
        public string NguoiXacNhan { get; set; }
    }
    public class HanhDongVM
    {
        public string Ma { get; set; }
        public string Ten { get; set; }
        public string Icon { get; set; }
        public string Css { get; set; }
    }
    public class DonHangChiTietVM
    {
        public int Id { get; set; }
        public string MaDonHang { get; set; }
        public string TaiKhoan { get; set; }
        public string TenNguoiNhan { get; set; }
        public string SoDienThoai { get; set; }
        public string Email { get; set; }
        public string DiaChiGiao { get; set; }
        public string TinhThanh { get; set; }
        public string GhiChu { get; set; }
        public string MaKhuyenMai { get; set; }
        public decimal TongTienHang { get; set; }
        public decimal PhiShip { get; set; }
        public decimal TienGiam { get; set; }
        public decimal TongCong { get { return TongTienHang + PhiShip - TienGiam; } }
        public string PhuongThuc { get; set; }
        public string TrangThaiThanhToan { get; set; }
        public string TrangThai { get; set; }
        public DateTime NgayDat { get; set; }
        public DateTime? NgayThanhToan { get; set; }
        public DateTime? NgayGiaoXong { get; set; }
        public List<DongHangVM> Items { get; set; }
        public List<LichSuTrangThaiVM> LichSu { get; set; }
        public List<GiaoDichVM> GiaoDich { get; set; }

        public string TenTrangThai { get { return ThongKeController.NhanTrangThai(TrangThai); } }
        public string MauTrangThai { get { return ThongKeController.MauTrangThai(TrangThai); } }
        public string TenThanhToan { get { return DonHangController.NhanThanhToan(TrangThaiThanhToan); } }
        public string MauThanhToan { get { return DonHangController.MauThanhToan(TrangThaiThanhToan); } }
        public string TenPhuongThuc { get { return DonHangController.NhanPhuongThuc(PhuongThuc); } }

        // các bước tiếp theo được phép (khớp đúng luồng trong sp_CapNhatTrangThaiDon)
        public List<HanhDongVM> HanhDong
        {
            get
            {
                var ds = new List<HanhDongVM>();
                var huy = new HanhDongVM { Ma = "DaHuy", Ten = "Hủy đơn", Icon = "bi-x-circle", Css = "btn-outline-danger" };
                switch (TrangThai)
                {
                    case "ChoXacNhan":
                        ds.Add(new HanhDongVM { Ma = "DaXacNhan", Ten = "Xác nhận đơn", Icon = "bi-check2-circle", Css = "btn-success" });
                        ds.Add(huy); break;
                    case "DaXacNhan":
                        ds.Add(new HanhDongVM { Ma = "DangGiao", Ten = "Bắt đầu giao hàng", Icon = "bi-truck", Css = "btn-primary" });
                        ds.Add(huy); break;
                    case "DangGiao":
                        ds.Add(new HanhDongVM { Ma = "HoanTat", Ten = "Hoàn tất (đã giao thành công)", Icon = "bi-bag-check", Css = "btn-success" });
                        break;
                }
                return ds;
            }
        }
    }
}