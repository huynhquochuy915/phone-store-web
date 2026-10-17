using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Models.ViewModels;

namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    public class SanPhamController : Controller
    {
        private const int SoDongMoiTrang = 12;
        private const string SoSanhSessionKey = "SanPhamSoSanh";
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();

        [HttpGet]
        public ActionResult Index(string tuKhoa, int? loaiId, int? thuongHieuId,
            decimal? giaTu, decimal? giaDen, byte? ramGB, int? dungLuongGB,
            bool conHang = false, string sapXep = null, int trang = 1)
        {
            if (trang < 1) trang = 1;
            var sapXepHopLe = new[] { "gia_tang", "gia_giam", "banchay", "danhgia", "xemnhieu" };
            if (!sapXepHopLe.Contains(sapXep)) sapXep = null;
            if (giaTu < 0) giaTu = null;
            if (giaDen < 0) giaDen = null;

            var ketQua = db.sp_LocSanPham(tuKhoa, loaiId, thuongHieuId, giaTu, giaDen,
                ramGB, dungLuongGB, conHang, sapXep, trang, SoDongMoiTrang).ToList();
            var tong = ketQua.FirstOrDefault() == null ? 0 : ketQua[0].TongSoDong.GetValueOrDefault();
            var vm = new DanhSachSanPhamVM
            {
                SanPhams = ketQua.Select(x => new SanPhamCardVM
                {
                    Id = x.Id, LoaiId = x.LoaiId, TenSanPham = x.TenSanPham,
                    TenThuongHieu = x.TenThuongHieu, AnhDaiDien = x.AnhDaiDien,
                    GiaThapNhat = x.GiaThapNhat, GiaCaoNhat = x.GiaCaoNhat,
                    TongTon = x.TongTon, SaoTrungBinh = x.SaoTrungBinh,
                    SoDanhGia = x.SoDanhGia, DaBan = x.DaBan
                }).ToList(),
                Loais = db.LoaiSanPhams.Where(x => x.HienThi).OrderBy(x => x.TenLoai).ToList(),
                ThuongHieus = db.SanPhams.Where(x => x.HienThi && (!loaiId.HasValue || x.LoaiId == loaiId.Value))
                    .Select(x => x.ThuongHieu).Distinct().OrderBy(x => x.TenThuongHieu).ToList(),
                TuKhoa = tuKhoa, LoaiId = loaiId, ThuongHieuId = thuongHieuId,
                GiaTu = giaTu, GiaDen = giaDen, RamGB = ramGB, DungLuongGB = dungLuongGB,
                ConHang = conHang, SapXep = sapXep, Trang = trang,
                SoTrang = Math.Max(1, (int)Math.Ceiling(tong / (double)SoDongMoiTrang))
            };
            return View(vm);
        }

        [HttpGet]
        public JsonResult GoiY(string tuKhoa)
        {
            if (String.IsNullOrWhiteSpace(tuKhoa) || tuKhoa.Trim().Length < 2)
                return Json(new object[0], JsonRequestBehavior.AllowGet);

            var ketQua = db.sp_GoiYTimKiem(tuKhoa.Trim(), 8).ToList().Select(x => new
            {
                id = x.Id,
                ten = x.TenSanPham,
                thuongHieu = x.TenThuongHieu,
                anh = x.AnhDaiDien,
                gia = x.GiaThapNhat
            });
            return Json(ketQua, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult ChiTiet(int id)
        {
            var sp = db.SanPhams
                .Include(x => x.BienTheSanPhams)
                .Include(x => x.HinhAnhSanPhams)
                .FirstOrDefault(x => x.Id == id && x.HienThi);
            if (sp == null) return HttpNotFound("Không tìm thấy sản phẩm.");

            db.Database.ExecuteSqlCommand("UPDATE SanPham SET LuotXem = LuotXem + 1 WHERE Id = @p0", id);
            var userId = Session["UserId"] as int?;
            var daMua = userId.HasValue && db.vw_NguoiDaMuaSanPham.Any(x => x.SanPhamId == id && x.NguoiDungId == userId.Value);
            var daDanhGia = userId.HasValue && db.DanhGias.Any(x => x.SanPhamId == id && x.NguoiDungId == userId.Value);

            var lienQuanIds = db.SanPhamLienQuans.Where(x => x.SanPhamId == id)
                .Select(x => x.SanPhamLienQuanId).Take(5).ToList();
            if (!lienQuanIds.Any())
                lienQuanIds = db.SanPhams.Where(x => x.HienThi && x.Id != id && x.ThuongHieuId == sp.ThuongHieuId)
                    .OrderByDescending(x => x.NoiBat).Select(x => x.Id).Take(5).ToList();

            var vm = new SanPhamChiTietVM
            {
                SanPham = sp,
                HinhAnhs = sp.HinhAnhSanPhams.OrderByDescending(x => x.LaAnhChinh).ThenBy(x => x.ThuTu).ToList(),
                DanhGias = db.DanhGias.Include(x => x.NguoiDung).Where(x => x.SanPhamId == id && x.DaDuyet)
                    .OrderByDescending(x => x.NgayDanhGia).Take(20).ToList(),
                CoTheDanhGia = daMua && !daDanhGia,
                DaDanhGia = daDanhGia,
                KyHanTraGop = db.GoiTraGops.Where(x => x.HienThi).OrderBy(x => x.SoThang).Select(x => x.SoThang).ToList(),
                SanPhamLienQuan = db.vw_SanPhamHienThi.Where(x => lienQuanIds.Contains(x.Id)).ToList().Select(x => new SanPhamCardVM
                {
                    Id = x.Id, LoaiId = x.LoaiId, TenSanPham = x.TenSanPham,
                    TenThuongHieu = x.TenThuongHieu, AnhDaiDien = x.AnhDaiDien,
                    GiaThapNhat = x.GiaThapNhat, GiaCaoNhat = x.GiaCaoNhat,
                    TongTon = x.TongTon, SaoTrungBinh = x.SaoTrungBinh,
                    SoDanhGia = x.SoDanhGia, DaBan = x.DaBan
                }).ToList()
            };
            return View(vm);
        }

        [HttpGet]
        public JsonResult TinhTraGop(int bienTheId, byte soThang, byte traTruocPT)
        {
            try
            {
                var result = db.sp_TinhTraGop(bienTheId, soThang, traTruocPT).FirstOrDefault();
                if (result == null) return Json(new { ok = false, message = "Không tính được khoản trả góp." }, JsonRequestBehavior.AllowGet);
                return Json(new { ok = true, result.GiaBan, result.SoThang, result.TraTruoc, result.SoTienTraGop,
                    result.PhiChuyenDoi, result.TraMoiThang, result.TongPhaiTra }, JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new { ok = false, message = "Kỳ hạn hoặc mức trả trước không hợp lệ." }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult YeuThichIds()
        {
            var userId = Session["UserId"] as int?;
            var ids = userId.HasValue
                ? db.YeuThiches.Where(x => x.NguoiDungId == userId.Value).Select(x => x.SanPhamId).ToList()
                : new List<int>();
            return Json(ids, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ChuyenYeuThich(int sanPhamId)
        {
            var userId = Session["UserId"] as int?;
            if (!userId.HasValue)
            {
                return Json(new
                {
                    ok = false,
                    requiresLogin = true,
                    loginUrl = Url.Action("Login", "Account", new { returnUrl = Request.RawUrl })
                });
            }

            if (!db.SanPhams.Any(x => x.Id == sanPhamId && x.HienThi))
                return Json(new { ok = false, message = "Sản phẩm không tồn tại hoặc đã ngừng bán." });

            var saved = db.YeuThiches.FirstOrDefault(x => x.NguoiDungId == userId.Value && x.SanPhamId == sanPhamId);
            var daYeuThich = saved == null;
            if (daYeuThich)
            {
                db.YeuThiches.Add(new YeuThich
                {
                    NguoiDungId = userId.Value,
                    SanPhamId = sanPhamId,
                    NgayThem = DateTime.Now
                });
            }
            else
            {
                db.YeuThiches.Remove(saved);
            }
            db.SaveChanges();

            var count = db.YeuThiches.Count(x => x.NguoiDungId == userId.Value);
            return Json(new
            {
                ok = true,
                saved = daYeuThich,
                count,
                message = daYeuThich ? "Đã thêm vào danh sách yêu thích." : "Đã bỏ khỏi danh sách yêu thích."
            });
        }

        [HttpGet]
        public ActionResult YeuThich()
        {
            var userId = Session["UserId"] as int?;
            if (!userId.HasValue)
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("YeuThich") });

            var rows = (from favorite in db.YeuThiches
                        join product in db.vw_SanPhamHienThi on favorite.SanPhamId equals product.Id
                        where favorite.NguoiDungId == userId.Value
                        orderby favorite.NgayThem descending
                        select product).ToList();
            var vm = rows.Select(x => new SanPhamCardVM
            {
                Id = x.Id, LoaiId = x.LoaiId, TenSanPham = x.TenSanPham,
                TenThuongHieu = x.TenThuongHieu, AnhDaiDien = x.AnhDaiDien,
                GiaThapNhat = x.GiaThapNhat, GiaCaoNhat = x.GiaCaoNhat,
                TongTon = x.TongTon, SaoTrungBinh = x.SaoTrungBinh,
                SoDanhGia = x.SoDanhGia, DaBan = x.DaBan, DaYeuThich = true
            }).ToList();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DanhGia(DanhGiaVM model)
        {
            var userId = Session["UserId"] as int?;
            if (!userId.HasValue)
            {
                TempData["Err"] = "Vui lòng đăng nhập để đánh giá sản phẩm.";
                return RedirectToAction("Login", "Account");
            }
            if (!ModelState.IsValid)
            {
                TempData["Err"] = "Thông tin đánh giá chưa hợp lệ.";
                return RedirectToAction("ChiTiet", new { id = model.SanPhamId });
            }

            try
            {
                db.sp_ThemDanhGia(model.SanPhamId, userId.Value, model.SoSao, model.NoiDung);
                TempData["Msg"] = "Đã gửi đánh giá. Đánh giá sẽ hiển thị sau khi được duyệt.";
            }
            catch (Exception ex)
            {
                var message = ex.GetBaseException().Message;
                TempData["Err"] = message.Contains("50040") || message.Contains("nhận hàng")
                    ? "Chỉ khách đã nhận hàng mới được đánh giá sản phẩm này."
                    : (message.Contains("50041") || message.Contains("đánh giá sản phẩm này")
                        ? "Bạn đã đánh giá sản phẩm này rồi."
                        : "Không gửi được đánh giá. Vui lòng thử lại.");
            }
            return RedirectToAction("ChiTiet", new { id = model.SanPhamId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ThemSoSanh(int id)
        {
            var ids = LayDanhSachSoSanh();
            if (!db.SanPhams.Any(x => x.Id == id && x.HienThi))
                TempData["Err"] = "Sản phẩm không tồn tại hoặc đã ngừng bán.";
            else if (!ids.Contains(id) && ids.Count >= 4)
                TempData["Err"] = "Bạn chỉ có thể so sánh tối đa 4 sản phẩm.";
            else
            {
                if (!ids.Contains(id)) ids.Add(id);
                Session[SoSanhSessionKey] = ids;
                TempData["Msg"] = "Đã thêm sản phẩm vào danh sách so sánh.";
            }
            return RedirectToAction("SoSanh");
        }

        [HttpGet]
        public ActionResult SoSanh()
        {
            var ids = LayDanhSachSoSanh();
            var vm = new SoSanhVM
            {
                SanPhams = db.SanPhams.Include(x => x.BienTheSanPhams)
                    .Where(x => ids.Contains(x.Id) && x.HienThi).ToList()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult XoaSoSanh(int id)
        {
            var ids = LayDanhSachSoSanh();
            ids.Remove(id);
            Session[SoSanhSessionKey] = ids;
            return RedirectToAction("SoSanh");
        }

        private List<int> LayDanhSachSoSanh()
        {
            return Session[SoSanhSessionKey] as List<int> ?? new List<int>();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
