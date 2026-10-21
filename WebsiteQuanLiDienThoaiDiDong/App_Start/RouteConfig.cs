using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace WebsiteQuanLiDienThoaiDiDong
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Giữ các URL cũ mà header đang sử dụng, nhưng xử lý bằng AccountController.
            routes.MapRoute(
                name: "TaiKhoanDangNhap",
                url: "TaiKhoan/DangNhap",
                defaults: new { controller = "Account", action = "Login" },
                namespaces: new[] { "WebsiteQuanLiDienThoaiDiDong.Controllers" }
            );
            routes.MapRoute(
                name: "TaiKhoanDangKy",
                url: "TaiKhoan/DangKy",
                defaults: new { controller = "Account", action = "Register" },
                namespaces: new[] { "WebsiteQuanLiDienThoaiDiDong.Controllers" }
            );
            routes.MapRoute(
                name: "TaiKhoanDangXuat",
                url: "TaiKhoan/DangXuat",
                defaults: new { controller = "Account", action = "Logout" },
                namespaces: new[] { "WebsiteQuanLiDienThoaiDiDong.Controllers" }
            );

            routes.MapRoute(
    name: "Default",
    url: "{controller}/{action}/{id}",
    defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional },
    namespaces: new[] { "WebsiteQuanLiDienThoaiDiDong.Controllers" }
);
        }
    }
}
