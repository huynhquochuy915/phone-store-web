using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Services;

namespace WebsiteQuanLiDienThoaiDiDong.Helpers
{
    public static class AuthHelper
    {
        private static System.Web.SessionState.HttpSessionState S { get { return HttpContext.Current.Session; } }
        public static int? UserId { get { return S["UserId"] as int?; } }
        public static string HoTen { get { return S["HoTen"] as string; } }
        public static string VaiTro { get { return S["VaiTro"] as string; } }
        public static bool IsLoggedIn { get { return UserId.HasValue; } }
        public static bool IsStaff { get { return VaiTro == "Admin" || VaiTro == "NhanVien"; } }

        // TV2 gọi sau khi kiểm tra mật khẩu đúng. user phải được Include("VaiTro")
        public static void SignIn(NguoiDung user, QL_DienThoaiEntities db)
        {
            S["UserId"] = user.Id;
            S["HoTen"] = user.HoTen;
            S["VaiTro"] = user.VaiTro.TenVaiTro;
            new CartService(db).MergeGuestCart(user.Id);   // gộp giỏ hàng khách vào giỏ tài khoản
        }

        public static void SignOut()
        {
            S.Clear();
            S.Abandon();
        }
    }

    public class RequireLoginAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext ctx)
        {
            if (!AuthHelper.IsLoggedIn)
                ctx.Result = new RedirectResult("~/TaiKhoan/DangNhap?returnUrl=" +
                    HttpUtility.UrlEncode(ctx.HttpContext.Request.RawUrl));
        }
    }

    public class RequireRoleAttribute : ActionFilterAttribute
    {
        private readonly string[] _roles;
        public RequireRoleAttribute(params string[] roles) { _roles = roles; }

        public override void OnActionExecuting(ActionExecutingContext ctx)
        {
            if (!AuthHelper.IsLoggedIn)
                ctx.Result = new RedirectResult("~/TaiKhoan/DangNhap?returnUrl=" +
                    HttpUtility.UrlEncode(ctx.HttpContext.Request.RawUrl));
            else if (!_roles.Contains(AuthHelper.VaiTro))
                ctx.Result = new HttpStatusCodeResult(403, "Bạn không có quyền truy cập");
        }
    }
}