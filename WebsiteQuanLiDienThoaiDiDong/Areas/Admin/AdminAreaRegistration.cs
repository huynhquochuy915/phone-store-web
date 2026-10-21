using System.Web.Mvc;

namespace WebsiteQuanLiDienThoaiDiDong.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName { get { return "Admin"; } }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "Admin_default",
                "Admin/{controller}/{action}/{id}",
                new { controller = "ThongKe", action = "Index", id = UrlParameter.Optional },
                new[] { "WebsiteQuanLiDienThoaiDiDong.Areas.Admin.Controllers" });
        }
    }
}