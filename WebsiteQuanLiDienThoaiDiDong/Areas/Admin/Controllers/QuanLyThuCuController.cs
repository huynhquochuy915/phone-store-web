using System;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Services;

namespace WebsiteQuanLiDienThoaiDiDong.Areas.Admin.Controllers
{
    public class QuanLyThuCuController : AdminBaseController
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();

        public ActionResult Index(string tt)
        {
            if (!string.IsNullOrEmpty(tt) && !ThuCuRules.TrangThai.Contains(tt)) tt = null;
            var ds = string.IsNullOrEmpty(tt)
                ? db.Database.SqlQuery<YeuCauThuCu>("SELECT TOP 200 * FROM YeuCauThuCu ORDER BY Id DESC").ToList()
                : db.Database.SqlQuery<YeuCauThuCu>("SELECT TOP 200 * FROM YeuCauThuCu WHERE TrangThai = @tt ORDER BY Id DESC",
                    new SqlParameter("@tt", tt)).ToList();
            ViewBag.Dem = db.Database.SqlQuery<TrangThaiDem>("SELECT TrangThai, COUNT(*) AS SoLuong FROM YeuCauThuCu GROUP BY TrangThai").ToList();
            ViewBag.TT = tt;
            return View(ds);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CapNhat(int id, string trangThai, decimal? giaChot, string tt)
        {
            if (!ThuCuRules.TrangThai.Contains(trangThai))
            {
                TempData["Err"] = "Trạng thái không hợp lệ";
                return RedirectToAction("Index", new { tt = tt });
            }
            db.Database.ExecuteSqlCommand(
                "UPDATE YeuCauThuCu SET TrangThai = @tt, GiaChot = @gia, NgayCapNhat = GETDATE() WHERE Id = @id",
                new SqlParameter("@tt", trangThai),
                new SqlParameter("@gia", (object)giaChot ?? DBNull.Value),
                new SqlParameter("@id", id));
            TempData["Msg"] = "Đã cập nhật yêu cầu.";
            return RedirectToAction("Index", new { tt = tt });
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}