using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong
{
    public class MvcApplication : HttpApplication
    {
        private static System.Threading.Timer _timer;

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // tự hủy đơn QR quá hạn, 5 phút/lần
            _timer = new System.Threading.Timer(_ =>
            {
                try
                {
                    using (var db = new QL_DienThoaiEntities())
                        db.Database.ExecuteSqlCommand("EXEC sp_HuyDonQRQuaHan");
                }
                catch { }
            }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        }
    }
}