using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Services;

namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    public class GioHangController : Controller
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();
        private readonly CartService cart;
        public GioHangController() { cart = new CartService(db); }

        public ActionResult Index() { return View(cart.GetCartVM()); }

        [ChildActionOnly]
        public ActionResult Badge() { return PartialView(cart.CountItems()); }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Them(int bienTheId, int soLuong = 1, bool muaNgay = false, string returnUrl = null)
        {
            var loi = cart.AddItem(bienTheId, soLuong);

            if (Request.IsAjaxRequest())
                return Json(new { ok = loi == null, message = loi ?? "Đã thêm vào giỏ hàng", count = cart.CountItems() });

            if (loi != null) { TempData["Err"] = loi; return QuayLai(returnUrl); }
            if (muaNgay) return RedirectToAction("Index", "DatHang");   // chưa đăng nhập sẽ bị chuyển sang đăng nhập
            TempData["Msg"] = "Đã thêm vào giỏ hàng";
            return QuayLai(returnUrl);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CapNhat(int lineId, int soLuong)
        {
            var loi = cart.UpdateQty(lineId, soLuong);
            if (loi != null) TempData["Err"] = loi;
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Xoa(int lineId)
        {
            cart.RemoveItem(lineId);
            TempData["Msg"] = "Đã xóa sản phẩm khỏi giỏ hàng";
            return RedirectToAction("Index");
        }

        private ActionResult QuayLai(string returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            if (Request.UrlReferrer != null) return Redirect(Request.UrlReferrer.ToString());
            return RedirectToAction("Index", "Home");
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}