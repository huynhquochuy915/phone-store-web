using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Models.ViewModels;
using WebsiteQuanLiDienThoaiDiDong.Services;
using System.Data.SqlClient;
namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    public class GioHangController : Controller
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();
        private readonly CartService cart;
        public GioHangController() { cart = new CartService(db); }

        //public ActionResult Index() { return View(cart.GetCartVM()); }
        public ActionResult Index()
        {
            var vm = cart.GetCartVM().GanTrangThaiChon(db);
            vm.GoiY = LayGoiY(vm);
            return View(vm);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Chon(int? lineId, bool chon)
        {
            var ids = cart.GetCartVM().Items.Select(i => i.LineId).ToList();   // chỉ động vào giỏ của chính người này
            if (lineId.HasValue)
            {
                if (!ids.Contains(lineId.Value)) return Json(new { ok = false });
                ids = new List<int> { lineId.Value };
            }
            if (ids.Count > 0)
                db.Database.ExecuteSqlCommand(
                    "UPDATE GioHangChiTiet SET DuocChon = @c WHERE Id IN (" + string.Join(",", ids) + ")",
                    new SqlParameter("@c", chon));
            return Json(new { ok = true });
        }

        private void ChiChonBienThe(int bienTheId)
        {
            var items = cart.GetCartVM().Items;
            var dong = items.FirstOrDefault(i => i.BienTheId == bienTheId);
            if (dong == null) return;
            db.Database.ExecuteSqlCommand(
                "UPDATE GioHangChiTiet SET DuocChon = CASE WHEN Id = @id THEN 1 ELSE 0 END " +
                "WHERE Id IN (" + string.Join(",", items.Select(i => i.LineId)) + ")",
                new SqlParameter("@id", dong.LineId));
        }
        private List<SanPhamCardVM> LayGoiY(CartVM vm)
        {
            var ids = vm.Items.Select(i => i.SanPhamId).Distinct().ToList();
            if (ids.Count == 0) return new List<SanPhamCardVM>();
            var ds = string.Join(",", ids);   // ids là int lấy từ DB nên nối chuỗi an toàn
            return db.Database.SqlQuery<SanPhamCardVM>(
                "SELECT TOP 4 v.*, (SELECT TOP 1 b.GiaGoc FROM BienTheSanPham b " +
                "WHERE b.SanPhamId = v.Id AND b.HienThi = 1 ORDER BY b.GiaBan) AS GiaGoc " +
                "FROM vw_SanPhamHienThi v " +
                "WHERE v.TongTon > 0 AND v.Id NOT IN (" + ds + ") " +
                "AND v.Id IN (SELECT SanPhamLienQuanId FROM SanPhamLienQuan " +
                "             WHERE LoaiLienQuan = 'PhuKien' AND SanPhamId IN (" + ds + ")) " +
                "ORDER BY v.DaBan DESC, v.LuotXem DESC").ToList();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ThemSanPham(int sanPhamId)
        {
            var bt = db.BienTheSanPhams
                .Where(b => b.SanPhamId == sanPhamId && b.HienThi && b.SoLuongTon > 0 && b.SanPham.HienThi)
                .OrderBy(b => b.GiaBan).FirstOrDefault();
            if (bt == null) { TempData["Err"] = "Sản phẩm này hiện đã hết hàng"; return RedirectToAction("Index"); }

            var loi = cart.AddItem(bt.Id, 1);
            if (loi != null) TempData["Err"] = loi; else TempData["Msg"] = "Đã thêm vào giỏ hàng";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CapNhat(int lineId, int soLuong)
        {
            var loi = cart.UpdateQty(lineId, soLuong);
            if (Request.IsAjaxRequest())
                return Json(new { ok = loi == null, message = loi, count = cart.CountItems() });
            if (loi != null) TempData["Err"] = loi;
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Xoa(int lineId)
        {
            cart.RemoveItem(lineId);
            if (Request.IsAjaxRequest())
                return Json(new { ok = true, count = cart.CountItems() });
            TempData["Msg"] = "Đã xóa sản phẩm khỏi giỏ hàng";
            return RedirectToAction("Index");
        }
        [ChildActionOnly]
        public ActionResult Badge() { return PartialView(cart.CountItems()); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Them(int bienTheId, int soLuong = 1, bool muaNgay = false, string returnUrl = null)
        {
            var loi = cart.AddItem(bienTheId, soLuong);

            if (Request.IsAjaxRequest())
                return Json(new { ok = loi == null, message = loi ?? "Đã thêm vào giỏ hàng", count = cart.CountItems() });

            if (loi != null) { TempData["Err"] = loi; return QuayLai(returnUrl); }
            if (muaNgay) { ChiChonBienThe(bienTheId); return RedirectToAction("Index", "DatHang"); }
            TempData["Msg"] = "Đã thêm vào giỏ hàng";
            return QuayLai(returnUrl);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult MuaNgay(int sanPhamId)
        {
            var bt = db.BienTheSanPhams
                .Where(b => b.SanPhamId == sanPhamId && b.HienThi && b.SoLuongTon > 0 && b.SanPham.HienThi)
                .OrderBy(b => b.GiaBan)
                .FirstOrDefault();

            if (bt == null)
            {
                TempData["Err"] = "Sản phẩm này hiện đã hết hàng";
                return QuayLai(null);
            }

            var loi = cart.AddItem(bt.Id, 1);
            if (loi != null) { TempData["Err"] = loi; return QuayLai(null); }

            return RedirectToAction("Index", "DatHang");   // chưa đăng nhập sẽ bị chuyển sang đăng nhập
        }

        //[HttpPost, ValidateAntiForgeryToken]
        //public ActionResult CapNhat(int lineId, int soLuong)
        //{
        //    var loi = cart.UpdateQty(lineId, soLuong);
        //    if (loi != null) TempData["Err"] = loi;
        //    return RedirectToAction("Index");
        //}

        //[HttpPost, ValidateAntiForgeryToken]
        //public ActionResult Xoa(int lineId)
        //{
        //    cart.RemoveItem(lineId);
        //    TempData["Msg"] = "Đã xóa sản phẩm khỏi giỏ hàng";
        //    return RedirectToAction("Index");
        //}

        private ActionResult QuayLai(string returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            if (Request.UrlReferrer != null) return Redirect(Request.UrlReferrer.ToString());
            return RedirectToAction("Index", "Home");
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}