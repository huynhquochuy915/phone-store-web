using System.Web;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Helpers;

namespace WebsiteQuanLiDienThoaiDiDong.Areas.Admin.Controllers
{
    public abstract class AdminBaseController : Controller
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (!AuthHelper.IsLoggedIn)
            {
                filterContext.Result = new RedirectResult("~/Account/Login?returnUrl=" + HttpUtility.UrlEncode(Request.RawUrl));
                return;
            }
            if (!AuthHelper.IsStaff)
            {
                filterContext.Result = new HttpStatusCodeResult(403, "Không có quyền truy cập");
                return;
            }
            base.OnActionExecuting(filterContext);
        }
    }
}