using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.SessionState;
using WebsiteQuanLiDienThoaiDiDong.Helpers;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Services;

namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    // API cho khung chat "Liên hệ" ở góc phải dưới của website (phía khách hàng).
    // ReadOnly: Session chỉ đọc để các request chat (gọi liên tục) không khoá Session làm chậm các trang khác.
    [SessionState(SessionStateBehavior.ReadOnly)]
    public class ChatController : Controller
    {
        private const string CookieName = "chat_tk";
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();

        // Khách chưa đăng nhập được nhận ra bằng một mã ngẫu nhiên lưu trong cookie
        private Guid? DocToken()
        {
            var c = Request.Cookies[CookieName];
            Guid g;
            return c != null && Guid.TryParse(c.Value, out g) ? g : (Guid?)null;
        }

        private void GhiToken(Guid g)
        {
            Response.Cookies.Add(new HttpCookie(CookieName, g.ToString("N"))
            {
                HttpOnly = true,
                Secure = Request.IsSecureConnection,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = DateTime.Now.AddDays(90)
            });
        }

        private static object Dto(ChatTinNhanVM m)
        {
            return new
            {
                id = m.Id,
                me = m.NguoiGui == ChatService.Khach,
                ten = m.NguoiGui == ChatService.NhanVien ? m.TenNV : null,
                nd = m.NoiDung,
                ts = ChatService.Ts(m.NgayGui)
            };
        }

        // Trình duyệt hỏi tin mới (2,5 giây/lần khi khung chat mở, 12 giây/lần khi đóng để cập nhật số chưa đọc)
        [HttpGet]
        public ActionResult Poll(int after = 0, int open = 0)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            if (AuthHelper.IsStaff) return Json(new { ok = false }, JsonRequestBehavior.AllowGet);

            try
            {
                var svc = new ChatService(db);
                var p = svc.TimPhien(AuthHelper.UserId, DocToken());
                if (p == null)
                    return Json(new { ok = true, started = false, unread = 0, messages = new object[0] }, JsonRequestBehavior.AllowGet);

                if (open != 1)
                    return Json(new { ok = true, started = true, unread = svc.DemChuaDocCuaKhach(p.Id), messages = new object[0] },
                        JsonRequestBehavior.AllowGet);

                var ds = svc.LayTinNhan(p.Id, after);
                if (ds.Count > 0) svc.DanhDauDaDoc(p.Id, ChatService.NhanVien, ds.Max(x => x.Id));
                return Json(new { ok = true, started = true, unread = 0, messages = ds.Select(Dto).ToList() },
                    JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Loi(ex);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Send(string noiDung, string hoTen, string sdt)
        {
            if (AuthHelper.IsStaff)
                return Json(new { ok = false, err = "Tài khoản nhân viên trả lời khách tại trang quản trị (Quản trị > Tin nhắn)." });

            string nd = ChatService.LamSach(noiDung, ChatService.MaxLen);
            if (nd.Length == 0) return Json(new { ok = false, err = "Vui lòng nhập nội dung tin nhắn." });

            try
            {
                var svc = new ChatService(db);
                int? uid = AuthHelper.UserId;
                Guid? token = DocToken();

                var p = svc.TimPhien(uid, token);
                if (p == null)
                {
                    string ten = uid.HasValue ? ChatService.LamSach(AuthHelper.HoTen, 60) : ChatService.LamSach(hoTen, 60);
                    if (ten.Length == 0)
                    {
                        if (uid.HasValue) ten = "Khách hàng";
                        else return Json(new { ok = false, needName = true, err = "Vui lòng cho mình biết tên của bạn nhé." });
                    }
                    if (ChatService.VuotGioiHanTaoPhien(Request))
                        return Json(new { ok = false, err = "Bạn thao tác quá nhanh, vui lòng thử lại sau." });

                    Guid? tk = null;
                    if (!uid.HasValue) { tk = token ?? Guid.NewGuid(); GhiToken(tk.Value); }
                    p = svc.TaoPhien(uid, tk, ten, ChatService.LamSachSdt(sdt));
                }

                if (svc.SoTinKhachGanDay(p.Id) >= 15)
                    return Json(new { ok = false, err = "Bạn gửi hơi nhanh, vui lòng chờ một chút rồi gửi tiếp." });

                int id = svc.GuiTin(p.Id, ChatService.Khach, null, nd);
                return Json(new { ok = true, msg = new { id = id, me = true, ten = (string)null, nd = nd, ts = ChatService.Ts(DateTime.Now) } });
            }
            catch (Exception ex)
            {
                return Loi(ex);
            }
        }

        private ActionResult Loi(Exception ex)
        {
            System.Diagnostics.Trace.TraceError("Chat: " + ex);
            // chỉ hiện chi tiết lỗi khi đang chạy debug (Web.config: compilation debug="true")
            string chiTiet = HttpContext.IsDebuggingEnabled ? ex.GetBaseException().Message : null;
            return Json(new { ok = false, err = "Chat tạm thời không khả dụng, vui lòng thử lại sau.", detail = chiTiet },
                JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
