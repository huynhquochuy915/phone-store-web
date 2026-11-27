using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.SessionState;
using WebsiteQuanLiDienThoaiDiDong.Helpers;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Services;

namespace WebsiteQuanLiDienThoaiDiDong.Areas.Admin.Controllers
{
    // Trang trả lời tin nhắn khách hàng dành cho Admin / Nhân viên (AdminBaseController đã chặn người ngoài).
    // ReadOnly: Session chỉ đọc để các request cập nhật liên tục không khoá Session.
    [SessionState(SessionStateBehavior.ReadOnly)]
    public class ChatController : AdminBaseController
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();

        public ActionResult Index()
        {
            ViewBag.Title = "Tin nhắn khách hàng";
            return View();
        }

        // Danh sách hội thoại (tt = mo | dong, q = tìm theo tên / số điện thoại)
        [HttpGet]
        public ActionResult DanhSach(string tt, string q)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            try
            {
                var svc = new ChatService(db);
                var ds = svc.DanhSach(tt, q).Select(p => new
                {
                    id = p.Id,
                    ten = p.Ten,
                    sdt = p.Sdt,
                    tv = p.LaThanhVien,
                    tin = p.TinCuoi,
                    nv = p.NguoiGuiCuoi == ChatService.NhanVien,
                    ts = ChatService.Ts(p.NgayCapNhat),
                    chuaDoc = p.ChuaDoc
                }).ToList();
                return Json(new { ok = true, tong = svc.TongChuaDocCuaNhanVien(), items = ds }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Loi(ex); }
        }

        // Tin nhắn của một hội thoại. doc = 1: nhân viên đang xem thật sự nên đánh dấu đã đọc.
        [HttpGet]
        public ActionResult TinNhan(int id, int after = 0, int doc = 1)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            try
            {
                var svc = new ChatService(db);
                var p = svc.ThongTinPhien(id);
                if (p == null) return Json(new { ok = false, err = "Không tìm thấy cuộc trò chuyện." }, JsonRequestBehavior.AllowGet);

                var ds = svc.LayTinNhan(id, after);
                if (doc == 1 && ds.Count > 0) svc.DanhDauDaDoc(id, ChatService.Khach, ds.Max(x => x.Id));

                return Json(new
                {
                    ok = true,
                    phien = new { id = p.Id, ten = p.Ten, sdt = p.Sdt, email = p.Email, tv = p.LaThanhVien, dong = p.TrangThai == "Dong" },
                    messages = ds.Select(Dto).ToList()
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) { return Loi(ex); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Gui(int id, string noiDung)
        {
            string nd = ChatService.LamSach(noiDung, ChatService.MaxLen);
            if (nd.Length == 0) return Json(new { ok = false, err = "Vui lòng nhập nội dung." });
            try
            {
                var svc = new ChatService(db);
                if (svc.LayPhien(id) == null) return Json(new { ok = false, err = "Không tìm thấy cuộc trò chuyện." });

                int msgId = svc.GuiTin(id, ChatService.NhanVien, AuthHelper.UserId, nd);
                return Json(new
                {
                    ok = true,
                    msg = new { id = msgId, nv = true, ten = AuthHelper.HoTen, nd = nd, ts = ChatService.Ts(DateTime.Now) }
                });
            }
            catch (Exception ex) { return Loi(ex); }
        }

        // Đóng cuộc trò chuyện đã xử lý xong / mở lại
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DoiTrangThai(int id, bool dong)
        {
            try
            {
                var svc = new ChatService(db);
                if (svc.LayPhien(id) == null) return Json(new { ok = false, err = "Không tìm thấy cuộc trò chuyện." });
                svc.DatTrangThai(id, dong);
                return Json(new { ok = true });
            }
            catch (Exception ex) { return Loi(ex); }
        }

        // Số tin chưa đọc hiện trên menu bên trái
        [HttpGet]
        public ActionResult SoChuaDoc()
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            try { return Json(new { ok = true, n = new ChatService(db).TongChuaDocCuaNhanVien() }, JsonRequestBehavior.AllowGet); }
            catch (Exception) { return Json(new { ok = false, n = 0 }, JsonRequestBehavior.AllowGet); }
        }

        private static object Dto(ChatTinNhanVM m)
        {
            return new
            {
                id = m.Id,
                nv = m.NguoiGui == ChatService.NhanVien,
                ten = m.NguoiGui == ChatService.NhanVien ? m.TenNV : null,
                nd = m.NoiDung,
                ts = ChatService.Ts(m.NgayGui)
            };
        }

        private ActionResult Loi(Exception ex)
        {
            System.Diagnostics.Trace.TraceError("Admin chat: " + ex);
            string chiTiet = HttpContext.IsDebuggingEnabled ? ex.GetBaseException().Message : null;
            return Json(new { ok = false, err = "Không xử lý được yêu cầu.", detail = chiTiet }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
