using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Areas.Admin.Controllers
{
    public class KhoController : AdminBaseController
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();
        private const int PageSize = 15;

        private static readonly string[] LyDoNhap =
            { "Nhập hàng từ nhà cung cấp", "Khách trả hàng", "Kiểm kê - tăng", "Khác" };
        private static readonly string[] LyDoXuat =
            { "Hàng lỗi / hư hỏng", "Mất mát / kiểm kê - giảm", "Xuất trả nhà cung cấp", "Xuất bảo hành / đổi hàng", "Khác" };

        // ===== 1. TỒN KHO =====
        public ActionResult Index(string q, string tt, int page = 1)
        {
            var vm = new KhoIndexVM
            {
                TuKhoa = q,
                TrangThai = tt,
                TongBienThe = db.BienTheSanPhams.Count(),
                TongTon = db.BienTheSanPhams.Sum(b => (int?)b.SoLuongTon) ?? 0,
                HetHang = db.BienTheSanPhams.Count(b => b.SoLuongTon <= 0),
                SapHet = db.BienTheSanPhams.Count(b => b.SoLuongTon > 0 && b.SoLuongTon <= b.NguongCanhBao)
            };

            var query = db.BienTheSanPhams.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var tk = q.Trim();
                query = query.Where(b => b.SKU.Contains(tk) || b.SanPham.TenSanPham.Contains(tk));
            }
            if (tt == "het") query = query.Where(b => b.SoLuongTon <= 0);
            else if (tt == "sap") query = query.Where(b => b.SoLuongTon > 0 && b.SoLuongTon <= b.NguongCanhBao);
            else if (tt == "con") query = query.Where(b => b.SoLuongTon > b.NguongCanhBao);

            vm.TongSoDong = query.Count();
            vm.TongSoTrang = (int)Math.Ceiling(vm.TongSoDong / (double)PageSize);
            vm.TrangHienTai = Math.Max(1, Math.Min(page, Math.Max(1, vm.TongSoTrang)));

            vm.Dong = query
                .OrderBy(b => b.SoLuongTon - b.NguongCanhBao)      // thiếu hàng nhất lên đầu
                .ThenBy(b => b.SanPham.TenSanPham)
                .Skip((vm.TrangHienTai - 1) * PageSize).Take(PageSize)
                .Select(b => new KhoDongVM
                {
                    BienTheId = b.Id,
                    TenSanPham = b.SanPham.TenSanPham,
                    Anh = b.SanPham.AnhDaiDien,
                    DungLuong = b.DungLuong,
                    MauSac = b.MauSac,
                    SKU = b.SKU,
                    SoLuongTon = b.SoLuongTon,
                    NguongCanhBao = b.NguongCanhBao
                }).ToList();

            return View(vm);
        }

        // ===== 2. PHIẾU NHẬP / XUẤT =====
        [HttpGet]
        public ActionResult PhieuKho(int bienTheId, string loai = "Nhap")
        {
            var vm = TaoPhieu(bienTheId, loai);
            if (vm == null) return HttpNotFound();
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult PhieuKho(int bienTheId, string loai, int? soLuong, string lyDo, string ghiChu)
        {
            var vm = TaoPhieu(bienTheId, loai);
            if (vm == null) return HttpNotFound();
            vm.SoLuong = soLuong; vm.LyDo = lyDo; vm.GhiChu = ghiChu;

            int sl = soLuong ?? 0;
            if (sl < 1 || sl > 100000)
                ModelState.AddModelError("", "Số lượng phải từ 1 đến 100.000.");
            if (string.IsNullOrWhiteSpace(lyDo) || !vm.DanhSachLyDo.Contains(lyDo))
                ModelState.AddModelError("", "Vui lòng chọn lý do.");
            if (vm.Loai == "Xuat" && sl > vm.TonHienTai)
                ModelState.AddModelError("", "Không thể xuất " + sl + " vì kho chỉ còn " + vm.TonHienTai + ".");
            if (!ModelState.IsValid) return View(vm);

            int delta = vm.Loai == "Nhap" ? sl : -sl;
            string lyDoDayDu = (vm.Loai == "Nhap" ? "Nhập kho: " : "Xuất kho: ") + lyDo;
            if (!string.IsNullOrWhiteSpace(ghiChu)) lyDoDayDu += " - " + ghiChu.Trim();
            if (lyDoDayDu.Length > 100) lyDoDayDu = lyDoDayDu.Substring(0, 100);   // cột LyDo tối đa 100 ký tự

            using (var tx = db.Database.BeginTransaction())
            {
                // Cập nhật nguyên tử: không bao giờ để tồn kho âm, kể cả khi vừa có đơn hàng mới
                int n = db.Database.ExecuteSqlCommand(
                    "UPDATE BienTheSanPham SET SoLuongTon = SoLuongTon + {0} WHERE Id = {1} AND SoLuongTon + {2} >= 0",
                    delta, bienTheId, delta);

                if (n == 0)
                {
                    var moi = TaoPhieu(bienTheId, loai);
                    moi.SoLuong = soLuong; moi.LyDo = lyDo; moi.GhiChu = ghiChu;
                    ModelState.AddModelError("", "Không đủ tồn kho để xuất (có thể vừa có đơn hàng mới). Vui lòng thử lại.");
                    return View(moi);            // thoát khỏi using => tự rollback
                }

                db.LichSuTonKhoes.Add(new LichSuTonKho
                {
                    BienTheId = bienTheId,
                    SoLuongThayDoi = delta,
                    LyDo = lyDoDayDu,
                    NgayTao = DateTime.Now
                });
                db.SaveChanges();
                tx.Commit();
            }

            TempData["Msg"] = string.Format("Đã {0} {1} x {2}. Tồn kho mới: {3}.",
                vm.Loai == "Nhap" ? "nhập" : "xuất", sl, vm.TenSanPham, vm.TonHienTai + delta);
            return RedirectToAction("Index");
        }

        private PhieuKhoVM TaoPhieu(int bienTheId, string loai)
        {
            var vm = db.BienTheSanPhams.Where(b => b.Id == bienTheId)
                .Select(b => new PhieuKhoVM
                {
                    BienTheId = b.Id,
                    TenSanPham = b.SanPham.TenSanPham,
                    Anh = b.SanPham.AnhDaiDien,
                    DungLuong = b.DungLuong,
                    MauSac = b.MauSac,
                    SKU = b.SKU,
                    TonHienTai = b.SoLuongTon,
                    NguongCanhBao = b.NguongCanhBao
                }).FirstOrDefault();
            if (vm == null) return null;
            vm.Loai = loai == "Xuat" ? "Xuat" : "Nhap";
            vm.DanhSachLyDo = vm.Loai == "Nhap" ? LyDoNhap : LyDoXuat;
            return vm;
        }

        // ===== 3. LỊCH SỬ XUẤT NHẬP =====
        public ActionResult LichSu(int? bienTheId, string loai, DateTime? tuNgay, DateTime? denNgay, int page = 1)
        {
            var query = db.LichSuTonKhoes.AsQueryable();
            if (bienTheId.HasValue) query = query.Where(h => h.BienTheId == bienTheId.Value);
            if (loai == "nhap") query = query.Where(h => h.SoLuongThayDoi > 0);
            else if (loai == "xuat") query = query.Where(h => h.SoLuongThayDoi < 0);
            if (tuNgay.HasValue) { var t = tuNgay.Value.Date; query = query.Where(h => h.NgayTao >= t); }
            if (denNgay.HasValue) { var d = denNgay.Value.Date.AddDays(1); query = query.Where(h => h.NgayTao < d); }

            var vm = new LichSuKhoVM
            {
                BienTheId = bienTheId,
                Loai = loai,
                TuNgay = tuNgay.HasValue ? tuNgay.Value.ToString("yyyy-MM-dd") : "",
                DenNgay = denNgay.HasValue ? denNgay.Value.ToString("yyyy-MM-dd") : "",
                TongNhap = query.Where(h => h.SoLuongThayDoi > 0).Sum(h => (int?)h.SoLuongThayDoi) ?? 0,
                TongXuat = -(query.Where(h => h.SoLuongThayDoi < 0).Sum(h => (int?)h.SoLuongThayDoi) ?? 0)
            };

            if (bienTheId.HasValue)
            {
                var bt = db.BienTheSanPhams.Where(b => b.Id == bienTheId.Value)
                    .Select(b => new { b.SKU, b.SanPham.TenSanPham, b.DungLuong, b.MauSac }).FirstOrDefault();
                if (bt != null)
                    vm.TenLoc = bt.TenSanPham + " " +
                        string.Join(" · ", new[] { bt.DungLuong, bt.MauSac }.Where(s => !string.IsNullOrWhiteSpace(s))) +
                        " (" + bt.SKU + ")";
            }

            vm.TongSoDong = query.Count();
            vm.TongSoTrang = (int)Math.Ceiling(vm.TongSoDong / (double)PageSize);
            vm.TrangHienTai = Math.Max(1, Math.Min(page, Math.Max(1, vm.TongSoTrang)));

            vm.Dong = query
                .OrderByDescending(h => h.NgayTao).ThenByDescending(h => h.Id)
                .Skip((vm.TrangHienTai - 1) * PageSize).Take(PageSize)
                .Select(h => new LichSuDongVM
                {
                    NgayTao = h.NgayTao,
                    SKU = h.BienTheSanPham.SKU,
                    TenSanPham = h.BienTheSanPham.SanPham.TenSanPham,
                    DungLuong = h.BienTheSanPham.DungLuong,
                    MauSac = h.BienTheSanPham.MauSac,
                    SoLuongThayDoi = h.SoLuongThayDoi,
                    LyDo = h.LyDo,
                    MaDonHang = h.DonHang.MaDonHang
                }).ToList();

            return View(vm);
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }

    // ================= VIEW MODELS =================
    public class KhoDongVM
    {
        public int BienTheId { get; set; }
        public string TenSanPham { get; set; }
        public string Anh { get; set; }
        public string DungLuong { get; set; }
        public string MauSac { get; set; }
        public string SKU { get; set; }
        public int SoLuongTon { get; set; }
        public int NguongCanhBao { get; set; }
    }
    public class KhoIndexVM
    {
        public string TuKhoa { get; set; }
        public string TrangThai { get; set; }
        public int TongBienThe { get; set; }
        public int TongTon { get; set; }
        public int SapHet { get; set; }
        public int HetHang { get; set; }
        public int TongSoDong { get; set; }
        public int TrangHienTai { get; set; }
        public int TongSoTrang { get; set; }
        public List<KhoDongVM> Dong { get; set; }
    }
    public class PhieuKhoVM
    {
        public int BienTheId { get; set; }
        public string Loai { get; set; }                 // "Nhap" | "Xuat"
        public string TenSanPham { get; set; }
        public string Anh { get; set; }
        public string DungLuong { get; set; }
        public string MauSac { get; set; }
        public string SKU { get; set; }
        public int TonHienTai { get; set; }
        public int NguongCanhBao { get; set; }
        public int? SoLuong { get; set; }
        public string LyDo { get; set; }
        public string GhiChu { get; set; }
        public string[] DanhSachLyDo { get; set; }
    }
    public class LichSuDongVM
    {
        public DateTime NgayTao { get; set; }
        public string SKU { get; set; }
        public string TenSanPham { get; set; }
        public string DungLuong { get; set; }
        public string MauSac { get; set; }
        public int SoLuongThayDoi { get; set; }
        public string LyDo { get; set; }
        public string MaDonHang { get; set; }
    }
    public class LichSuKhoVM
    {
        public int? BienTheId { get; set; }
        public string TenLoc { get; set; }
        public string Loai { get; set; }
        public string TuNgay { get; set; }
        public string DenNgay { get; set; }
        public int TongNhap { get; set; }
        public int TongXuat { get; set; }
        public int TongSoDong { get; set; }
        public int TrangHienTai { get; set; }
        public int TongSoTrang { get; set; }
        public List<LichSuDongVM> Dong { get; set; }
    }
}