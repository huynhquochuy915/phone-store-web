using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Areas.Admin.Controllers
{
    public class SanPhamController : AdminBaseController
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();
        private const int PageSize = 15;
        private static readonly string[] DuoiAnh = { ".jpg", ".jpeg", ".png", ".webp" };

        // ===== DANH SÁCH =====
        public ActionResult Index(string q, int page = 1)
        {
            var query = db.SanPhams.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var tk = q.Trim();
                query = query.Where(s => s.TenSanPham.Contains(tk));
            }
            int tong = query.Count();
            int soTrang = Math.Max(1, (int)Math.Ceiling(tong / (double)PageSize));
            page = Math.Max(1, Math.Min(page, soTrang));

            var ds = query.OrderByDescending(s => s.Id)
                .Skip((page - 1) * PageSize).Take(PageSize)
                .Select(s => new SanPhamDongVM
                {
                    Id = s.Id,
                    Ten = s.TenSanPham,
                    Anh = s.AnhDaiDien,
                    Loai = s.LoaiSanPham.TenLoai,
                    ThuongHieu = s.ThuongHieu.TenThuongHieu,
                    HienThi = s.HienThi,
                    SoBienThe = s.BienTheSanPhams.Count(),
                    TongTon = s.BienTheSanPhams.Sum(b => (int?)b.SoLuongTon) ?? 0
                }).ToList();

            ViewBag.Q = q; ViewBag.Page = page; ViewBag.SoTrang = soTrang;
            return View(ds);
        }

        // ===== THÊM SẢN PHẨM =====
        [HttpGet]
        public ActionResult Tao()
        {
            NapDanhSach();
            return View(new TaoSanPhamVM());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Tao(TaoSanPhamVM m, HttpPostedFileBase anh)
        {
            string sku = (m.SKU ?? "").Trim().ToUpper();

            if (m.GiaBan <= 0)
                ModelState.AddModelError("GiaBan", "Giá bán phải lớn hơn 0.");
            if (m.GiaGoc < m.GiaBan)
                ModelState.AddModelError("GiaGoc", "Giá gốc phải lớn hơn hoặc bằng giá bán.");
            if (sku != "" && db.BienTheSanPhams.Any(b => b.SKU == sku))
                ModelState.AddModelError("SKU", "SKU này đã tồn tại.");

            bool coAnh = anh != null && anh.ContentLength > 0;
            string duoi = coAnh ? Path.GetExtension(anh.FileName).ToLowerInvariant() : null;
            if (coAnh && !DuoiAnh.Contains(duoi))
                ModelState.AddModelError("", "Ảnh chỉ nhận .jpg, .jpeg, .png, .webp.");
            else if (coAnh && anh.ContentLength > 2 * 1024 * 1024)
                ModelState.AddModelError("", "Ảnh tối đa 2MB.");

            if (!ModelState.IsValid) { NapDanhSach(); return View(m); }

            // Lưu ảnh
            string fileLuu = null, duongDan = null;
            if (coAnh)
            {
                fileLuu = Guid.NewGuid().ToString("N").Substring(0, 12) + duoi;
                var thuMuc = Server.MapPath("~/Content/images/products");
                Directory.CreateDirectory(thuMuc);
                anh.SaveAs(Path.Combine(thuMuc, fileLuu));
                duongDan = "/Content/images/products/" + fileLuu;
            }

            var sp = new SanPham
            {
                LoaiId = m.LoaiId,
                ThuongHieuId = m.ThuongHieuId,
                TenSanPham = m.TenSanPham.Trim(),
                MoTa = m.MoTa,
                ManHinh = m.ManHinh,
                ChipXuLy = m.ChipXuLy,
                RamGB = m.RamGB,
                RAM = m.RamGB.HasValue ? m.RamGB + "GB" : null,
                CameraSau = m.CameraSau,
                CameraTruoc = m.CameraTruoc,
                Pin = m.Pin,
                HeDieuHanh = m.HeDieuHanh,
                TrongLuong = m.TrongLuong,
                BaoHanhThang = m.BaoHanhThang,
                AnhDaiDien = duongDan,
                NoiBat = m.NoiBat,
                HienThi = true,
                LuotXem = 0,
                NgayTao = DateTime.Now
            };

            var bt = new BienTheSanPham
            {
                MauSac = m.MauSac.Trim(),
                DungLuongGB = m.DungLuongGB,
                DungLuong = !m.DungLuongGB.HasValue ? null
                            : (m.DungLuongGB >= 1024 ? (m.DungLuongGB / 1024) + "TB" : m.DungLuongGB + "GB"),
                SKU = sku,
                GiaGoc = m.GiaGoc,
                GiaBan = m.GiaBan,
                SoLuongTon = m.SoLuongBanDau,
                NguongCanhBao = m.NguongCanhBao,
                HienThi = true
            };
            if (m.SoLuongBanDau > 0)
                bt.LichSuTonKhoes.Add(new LichSuTonKho
                {
                    SoLuongThayDoi = m.SoLuongBanDau,
                    LyDo = "Nhập kho: Tạo sản phẩm mới",
                    NgayTao = DateTime.Now
                });
            sp.BienTheSanPhams.Add(bt);

            if (duongDan != null)
                sp.HinhAnhSanPhams.Add(new HinhAnhSanPham { DuongDan = duongDan, LaAnhChinh = true, ThuTu = 1 });

            try
            {
                db.SanPhams.Add(sp);
                db.SaveChanges();   // một lần lưu = một transaction
            }
            catch (Exception)
            {
                if (fileLuu != null)   // lỗi DB thì xóa ảnh vừa lưu
                {
                    var p = Path.Combine(Server.MapPath("~/Content/images/products"), fileLuu);
                    if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
                }
                ModelState.AddModelError("", "Không lưu được sản phẩm. Vui lòng kiểm tra lại dữ liệu.");
                NapDanhSach();
                return View(m);
            }

            TempData["Msg"] = "Đã thêm sản phẩm \"" + sp.TenSanPham + "\".";
            return RedirectToAction("Index");
        }

        // ===== ẨN / HIỆN (xóa mềm) =====
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DoiTrangThai(int id)
        {
            var sp = db.SanPhams.Find(id);
            if (sp == null) return HttpNotFound();
            sp.HienThi = !sp.HienThi;
            db.SaveChanges();
            TempData["Msg"] = (sp.HienThi ? "Đã hiện " : "Đã ẩn ") + sp.TenSanPham + ".";
            return RedirectToAction("Index");
        }

        private void NapDanhSach()
        {
            ViewBag.Loais = new SelectList(db.LoaiSanPhams.Where(x => x.HienThi).OrderBy(x => x.TenLoai).ToList(), "Id", "TenLoai");
            ViewBag.ThuongHieus = new SelectList(db.ThuongHieux.OrderBy(x => x.TenThuongHieu).ToList(), "Id", "TenThuongHieu");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    public class SanPhamDongVM
    {
        public int Id { get; set; }
        public string Ten { get; set; }
        public string Anh { get; set; }
        public string Loai { get; set; }
        public string ThuongHieu { get; set; }
        public bool HienThi { get; set; }
        public int SoBienThe { get; set; }
        public int TongTon { get; set; }
    }

    public class TaoSanPhamVM
    {
        [Required(ErrorMessage = "Chọn loại sản phẩm")] public int LoaiId { get; set; }
        [Required(ErrorMessage = "Chọn thương hiệu")] public int ThuongHieuId { get; set; }
        [Required(ErrorMessage = "Nhập tên sản phẩm"), StringLength(200)] public string TenSanPham { get; set; }
        public string MoTa { get; set; }
        [StringLength(150)] public string ManHinh { get; set; }
        [StringLength(100)] public string ChipXuLy { get; set; }
        [Range(1, 64, ErrorMessage = "RAM từ 1 đến 64 GB")] public byte? RamGB { get; set; }
        [StringLength(200)] public string CameraSau { get; set; }
        [StringLength(100)] public string CameraTruoc { get; set; }
        [StringLength(100)] public string Pin { get; set; }
        [StringLength(50)] public string HeDieuHanh { get; set; }
        [StringLength(30)] public string TrongLuong { get; set; }
        [Range(0, 120, ErrorMessage = "Bảo hành từ 0 đến 120 tháng")] public int BaoHanhThang { get; set; } = 12;
        public bool NoiBat { get; set; }

        // Biến thể đầu tiên
        [Required(ErrorMessage = "Nhập màu sắc"), StringLength(50)] public string MauSac { get; set; }
        [Range(1, 4096, ErrorMessage = "Dung lượng không hợp lệ")] public int? DungLuongGB { get; set; }
        [Required(ErrorMessage = "Nhập SKU"), StringLength(50)] public string SKU { get; set; }
        [Range(0, 999999999)] public decimal GiaGoc { get; set; }
        [Range(0, 999999999)] public decimal GiaBan { get; set; }
        [Range(0, 100000, ErrorMessage = "Số lượng từ 0 đến 100.000")] public int SoLuongBanDau { get; set; }
        [Range(0, 1000)] public int NguongCanhBao { get; set; } = 5;
    }
}