using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    public class KhuyenMaiController : Controller
    {
        // GET: KhuyenMai
        private QL_DienThoaiEntities db = new QL_DienThoaiEntities();

        private bool LaAdmin()
        {
            return Session["VaiTroId"] != null
                && Convert.ToInt32(Session["VaiTroId"]) == 1;
        }

        private ActionResult KiemTraAdmin()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

            if (!LaAdmin())
                return new HttpStatusCodeResult(403);

            return null;
        }

        public ActionResult Index()
        {
            var kiemTra = KiemTraAdmin();
            if (kiemTra != null)
                return kiemTra;

            var danhSach = db.KhuyenMais
                .OrderByDescending(x => x.Id)
                .ToList();

            return View(danhSach);
        }

        // Tìm kiếm khuyến mãi
        public ActionResult TimKiem(string tuKhoa)
        {
            var kiemTra = KiemTraAdmin();
            if (kiemTra != null)
                return kiemTra;

            var danhSach = db.KhuyenMais.AsQueryable();

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                tuKhoa = tuKhoa.Trim();

                danhSach = danhSach.Where(x =>
                    x.MaCode.Contains(tuKhoa) ||
                    x.TenKhuyenMai.Contains(tuKhoa));
            }

            return View("Index", danhSach
                .OrderByDescending(x => x.Id)
                .ToList());
        }

        // Lọc trạng thái khuyến mãi

        public ActionResult Loc(string trangThai)
        {
            var kiemTra = KiemTraAdmin();
            if (kiemTra != null)
                return kiemTra;

            var danhSach = db.KhuyenMais.AsQueryable();

            if (trangThai == "DangApDung")
            {
                danhSach = danhSach.Where(x =>
                    x.TrangThai &&
                    x.NgayBatDau <= DateTime.Now &&
                    x.NgayKetThuc >= DateTime.Now &&
                    (x.SoLuong == null || x.DaDung < x.SoLuong));
            }

            else if (trangThai == "ChuaDenNgay")
            {
                danhSach = danhSach.Where(x =>
                    x.TrangThai &&
                    x.NgayBatDau > DateTime.Now);
            }

            else if (trangThai == "HetHan")
            {
                danhSach = danhSach.Where(x =>
                    x.NgayKetThuc < DateTime.Now);
            }

            else if (trangThai == "HetLuot")
            {
                danhSach = danhSach.Where(x =>
                    x.SoLuong != null &&
                    x.DaDung >= x.SoLuong);
            }

            else if (trangThai == "Ngung")
            {
                danhSach = danhSach.Where(x => !x.TrangThai);
            }

            ViewBag.TrangThaiDangChon = trangThai;

            return View("Index", danhSach
                .OrderByDescending(x => x.Id)
                .ToList());
        }

        // Bật, tắt khuyến mãi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DoiTrangThai(int id)
        {
            var kiemTra = KiemTraAdmin();
            if (kiemTra != null) 
                return kiemTra;

            var khuyenMai = db.KhuyenMais.Find(id);

            if (khuyenMai == null)
                return HttpNotFound();

            khuyenMai.TrangThai = !khuyenMai.TrangThai;
            db.SaveChanges();

            TempData["ThongBao"] = khuyenMai.TrangThai
                ? "Đã bật khuyến mãi."
                : "Đã tắt khuyến mãi.";

            return RedirectToAction("Index");
        }

        // Kiểm tra dữ liệu
        private void KiemTraDuLieu(KhuyenMai km)
        {
            if (string.IsNullOrWhiteSpace(km.MaCode))
            {
                ModelState.AddModelError("MaCode",
                    "Vui lòng nhập mã khuyến mãi.");
            }

            if (km.LoaiGiam != "PHANTRAM" &&
                km.LoaiGiam != "SOTIEN")
            {
                ModelState.AddModelError("LoaiGiam",
                    "Loại giảm không hợp lệ.");
            }

            if (km.GiaTriGiam <= 0)
            {
                ModelState.AddModelError("GiaTriGiam",
                    "Giá trị giảm phải lớn hơn 0.");
            }

            if (km.LoaiGiam == "PHANTRAM" &&
                km.GiaTriGiam > 100)
            {
                ModelState.AddModelError("GiaTriGiam",
                    "Mức giảm phần trăm không được vượt quá 100%.");
            }

            if (km.GiamToiDa.HasValue &&
                km.GiamToiDa.Value <= 0)
            {
                ModelState.AddModelError("GiamToiDa",
                    "Mức giảm tối đa phải lớn hơn 0.");
            }

            if (km.DonToiThieu < 0)
            {
                ModelState.AddModelError("DonToiThieu",
                    "Đơn tối thiểu không được âm.");
            }

            if (km.NgayKetThuc <= km.NgayBatDau)
            {
                ModelState.AddModelError("NgayKetThuc",
                    "Ngày kết thúc phải sau ngày bắt đầu.");
            }

            if (km.SoLuong.HasValue && km.SoLuong.Value <= 0)
            {
                ModelState.AddModelError("SoLuong",
                    "Số lượng phải lớn hơn 0 hoặc để trống nếu không giới hạn.");
            }

            if (km.GioiHanMoiNguoi.HasValue &&
                km.GioiHanMoiNguoi.Value <= 0)
            {
                ModelState.AddModelError("GioiHanMoiNguoi",
                    "Giới hạn mỗi người phải lớn hơn 0 hoặc để trống.");
            }
        }

        // Thêm khuyến mãi
        [HttpGet]
        public ActionResult Create()
        {
            var kiemTra = KiemTraAdmin();
            if (kiemTra != null) 
                return kiemTra;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(KhuyenMai khuyenMai)
        {
            var kiemTra = KiemTraAdmin();
            if (kiemTra != null) 
                return kiemTra;

            if (string.IsNullOrWhiteSpace(khuyenMai.MaCode))
            {
                ModelState.AddModelError("MaCode",
                    "Vui lòng nhập mã voucher.");
            }

            else
            {
                khuyenMai.MaCode = khuyenMai.MaCode.Trim().ToUpper();

                if (db.KhuyenMais.Any(x => x.MaCode == khuyenMai.MaCode))
                {
                    ModelState.AddModelError("MaCode",
                        "Mã khuyến mãi đã tồn tại.");
                }

                khuyenMai.MaCode = khuyenMai.MaCode.Trim().ToUpper();
            }

            if (string.IsNullOrWhiteSpace(khuyenMai.TenKhuyenMai))
            {
                ModelState.AddModelError("TenKhuyenMai",
                    "Vui lòng nhập tên khuyến mãi.");
            }

            else
            {
                khuyenMai.TenKhuyenMai = khuyenMai.TenKhuyenMai.Trim();
            }

            if (khuyenMai.LoaiGiam == "SOTIEN")
            {
                khuyenMai.GiamToiDa = null;
            }

            KiemTraDuLieu(khuyenMai);

            if (!ModelState.IsValid)
                return View(khuyenMai);

            khuyenMai.DaDung = 0;

            db.KhuyenMais.Add(khuyenMai);
            db.SaveChanges();

            TempData["ThongBao"] = "Thêm khuyến mãi thành công.";

            return RedirectToAction("Index");
        }

        // Sửa khuyến mãi
        [HttpGet]
        public ActionResult Edit(int id)
        {
            var kiemTra = KiemTraAdmin();
            if (kiemTra != null) 
                return kiemTra;

            var khuyenMai = db.KhuyenMais.Find(id);

            if (khuyenMai == null)
                return HttpNotFound();

            return View(khuyenMai);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(KhuyenMai khuyenMai)
        {
            var kiemTra = KiemTraAdmin();
            if (kiemTra != null) return kiemTra;

            var duLieuCu = db.KhuyenMais.Find(khuyenMai.Id);

            if (duLieuCu == null)
                return HttpNotFound();

            if (string.IsNullOrWhiteSpace(khuyenMai.MaCode))
            {
                ModelState.AddModelError("MaCode",
                    "Vui lòng nhập mã voucher.");
            }

            else
            {
                khuyenMai.MaCode = khuyenMai.MaCode.Trim().ToUpper();

                if (db.KhuyenMais.Any(x =>
                    x.MaCode == khuyenMai.MaCode &&
                    x.Id != khuyenMai.Id))
                {
                    ModelState.AddModelError("MaCode",
                        "Mã khuyến mãi đã tồn tại.");
                }
            }

            if (string.IsNullOrWhiteSpace(khuyenMai.TenKhuyenMai))
            {
                ModelState.AddModelError("TenKhuyenMai",
                    "Vui lòng nhập tên khuyến mãi.");
            }

            else
            {
                khuyenMai.TenKhuyenMai = khuyenMai.TenKhuyenMai.Trim();
            }

            KiemTraDuLieu(khuyenMai);

            if (khuyenMai.SoLuong.HasValue &&
                khuyenMai.SoLuong.Value < duLieuCu.DaDung)
            {
                ModelState.AddModelError("SoLuong",
                    "Tổng lượt sử dụng không được nhỏ hơn số lượt đã dùng.");
            }

            if (!ModelState.IsValid)
                return View(khuyenMai);

            duLieuCu.MaCode = khuyenMai.MaCode;
            duLieuCu.TenKhuyenMai = khuyenMai.TenKhuyenMai;
            duLieuCu.LoaiGiam = khuyenMai.LoaiGiam;
            duLieuCu.GiaTriGiam = khuyenMai.GiaTriGiam;
            duLieuCu.GiamToiDa = khuyenMai.GiamToiDa;
            duLieuCu.DonToiThieu = khuyenMai.DonToiThieu;
            duLieuCu.NgayBatDau = khuyenMai.NgayBatDau;
            duLieuCu.NgayKetThuc = khuyenMai.NgayKetThuc;
            duLieuCu.SoLuong = khuyenMai.SoLuong;
            duLieuCu.GioiHanMoiNguoi = khuyenMai.GioiHanMoiNguoi;
            duLieuCu.TrangThai = khuyenMai.TrangThai;

            db.SaveChanges();

            TempData["ThongBao"] = "Cập nhật khuyến mãi thành công.";

            return RedirectToAction("Index");
        }

        // Xóa khuyến mãi chưa được sử dụng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var kiemTra = KiemTraAdmin();
            if (kiemTra != null) 
                return kiemTra;

            var khuyenMai = db.KhuyenMais.Find(id);

            if (khuyenMai == null)
                return HttpNotFound();

            bool daCoLuotSuDung = db.KhuyenMaiSuDungs
                .Any(x => x.KhuyenMaiId == id);

            bool daGanDonHang = db.DonHangs
                .Any(x => x.KhuyenMaiId == id);

            if (khuyenMai.DaDung > 0 ||
                daCoLuotSuDung ||
                daGanDonHang)
            {
                TempData["ThongBao"] =
                    "Khuyến mãi đã được sử dụng hoặc gắn với đơn hàng. Hãy tắt thay vì xóa!";

                return RedirectToAction("Index");
            }

            db.KhuyenMais.Remove(khuyenMai);
            db.SaveChanges();

            TempData["ThongBao"] = "Đã xóa khuyến mãi.";

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }
}