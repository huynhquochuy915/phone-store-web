using System;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Helpers;
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
            var back = RedirectToAction("Index", new { tt = tt });

            var yc = db.Database.SqlQuery<YeuCauThuCu>("SELECT * FROM YeuCauThuCu WHERE Id = @id", new SqlParameter("@id", id)).FirstOrDefault();
            if (yc == null) return HttpNotFound();

            if (!ThuCuRules.TrangThai.Contains(trangThai)) { TempData["Err"] = "Trạng thái không hợp lệ."; return back; }
            if (yc.TrangThai == "HoanTat" || yc.TrangThai == "Huy")
            { TempData["Err"] = "Yêu cầu " + yc.MaYeuCau + " đã đóng, không sửa được nữa."; return back; }

            bool canGia = trangThai == "DaDinhGia" || trangThai == "HoanTat";
            if (canGia)
            {
                if (!giaChot.HasValue || giaChot.Value <= 0)
                { TempData["Err"] = "Cần nhập giá chốt trước khi chuyển sang \"" + ThuCuRules.TenTrangThai(trangThai) + "\"."; return back; }

                var max = db.Database.SqlQuery<decimal>("SELECT GiaToiDa FROM BangGiaThuCu WHERE Id = @b", new SqlParameter("@b", yc.BangGiaId)).FirstOrDefault();
                if (max > 0 && giaChot.Value > max)
                { TempData["Err"] = "Giá chốt " + giaChot.Value.ToString("N0") + "đ vượt giá thu tối đa của dòng máy này (" + max.ToString("N0") + "đ)."; return back; }
            }
            decimal? giaLuu = canGia ? giaChot : yc.GiaChot;   // chưa định giá thì giữ nguyên

            db.Database.ExecuteSqlCommand(
                "UPDATE YeuCauThuCu SET TrangThai = @tt, GiaChot = @gia, NgayCapNhat = GETDATE() WHERE Id = @id",
                new SqlParameter("@tt", trangThai),
                new SqlParameter("@gia", (object)giaLuu ?? DBNull.Value),
                new SqlParameter("@id", id));

            // báo cho khách khi có kết quả quan trọng
            bool doiTT = yc.TrangThai != trangThai;
            bool doiGia = canGia && yc.GiaChot != giaLuu;
            bool coMail = false;
            if ((doiTT || doiGia) && !string.IsNullOrWhiteSpace(yc.Email) && (canGia || trangThai == "Huy"))
                coMail = GuiMail(yc, trangThai, giaLuu);

            TempData["Msg"] = "Đã cập nhật yêu cầu " + yc.MaYeuCau + (coMail ? " và gửi email báo khách." : ".");
            return back;
        }

        private bool GuiMail(YeuCauThuCu yc, string trangThai, decimal? gia)
        {
            string shop = CauHinh.Get("TenCuaHang", "PhoneStore");
            string link = Url.Action("TraCuu", "ThuCu", new { area = "", ma = yc.MaYeuCau }, Request.Url.Scheme);
            string ten = HttpUtility.HtmlEncode(yc.HoTen);
            string may = HttpUtility.HtmlEncode(yc.TenMay);
            string tienGia = gia.HasValue ? gia.Value.ToString("N0") + "đ" : "";
            string noiDung, tieuDe;

            if (trangThai == "DaDinhGia")
            {
                tieuDe = "Giá thu chính thức cho " + yc.TenMay;
                noiDung = "<p>Sau khi kiểm tra, " + shop + " đề xuất giá thu <b>" + may + "</b> là <b style=\"color:#d70018\">" + tienGia + "</b> (giá ước tính ban đầu: " + yc.GiaUocTinh.ToString("N0") + "đ).</p>"
                        + "<p>Bạn có thể đồng ý hoặc từ chối khi đến cửa hàng, không mất phí.</p>";
            }
            else if (trangThai == "HoanTat")
            {
                tieuDe = "Hoàn tất thu cũ " + yc.MaYeuCau;
                noiDung = "<p>Giao dịch thu cũ <b>" + may + "</b> đã hoàn tất với số tiền <b style=\"color:#d70018\">" + tienGia + "</b>.</p>"
                        + (string.IsNullOrEmpty(yc.MayMuonMua) ? "" : "<p>Số tiền này được dùng để lên đời <b>" + HttpUtility.HtmlEncode(yc.MayMuonMua) + "</b>.</p>")
                        + "<p>Cảm ơn bạn đã tin tưởng " + shop + ".</p>";
            }
            else
            {
                tieuDe = "Yêu cầu thu cũ " + yc.MaYeuCau + " đã đóng";
                noiDung = "<p>Yêu cầu thu cũ <b>" + may + "</b> đã được đóng. Nếu cần hỗ trợ thêm, vui lòng liên hệ hotline <b>" + CauHinh.Get("Hotline") + "</b>.</p>";
            }

            string body = "<p>Xin chào <b>" + ten + "</b>,</p>" + noiDung
                        + "<p><a href=\"" + link + "\">Xem trạng thái yêu cầu</a> (nhập mã " + yc.MaYeuCau + " và số điện thoại đã đăng ký).</p>";
            string loi;
            return EmailHelper.Send(yc.Email, tieuDe + " - " + shop, body, out loi);
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}