using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Helpers;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Services;

namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    public class ThuCuController : Controller
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();

        private List<MayThuCu> DsMay()
        {
            return db.Database.SqlQuery<MayThuCu>(
                "SELECT Id, HangMay, TenMay, DungLuongGB, GiaToiDa FROM BangGiaThuCu WHERE HienThi = 1 ORDER BY HangMay, TenMay, DungLuongGB").ToList();
        }

        private List<MayMoi> DsMoi()
        {
            return db.SanPhams.Where(s => s.HienThi && s.LoaiId == 1)
                .Select(s => new { s.Id, s.TenSanPham, Gia = s.BienTheSanPhams.Where(b => b.HienThi).Min(b => (decimal?)b.GiaBan) })
                .ToList().Where(x => x.Gia.HasValue)
                .Select(x => new MayMoi { Id = x.Id, Ten = x.TenSanPham, Gia = x.Gia.Value })
                .OrderBy(x => x.Ten).ToList();
        }

        private ThuCuVM Build(ThuCuForm f)
        {
            return new ThuCuVM { Form = f, May = DsMay(), MayMoi = DsMoi() };
        }

        [HttpGet]
        public ActionResult Index(int? moi)
        {
            var f = new ThuCuForm { MayMoiId = moi };
            if (AuthHelper.IsLoggedIn)
            {
                var u = db.NguoiDungs.Find(AuthHelper.UserId.Value);
                if (u != null) { f.HoTen = u.HoTen; f.Email = u.Email; f.SoDienThoai = u.SoDienThoai; }
            }
            return View(Build(f));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Index(ThuCuForm f)
        {
            var may = f.MayId.HasValue ? DsMay().FirstOrDefault(m => m.Id == f.MayId.Value) : null;
            if (may == null) ModelState.AddModelError("MayId", "Vui lòng chọn máy cần thu");

            decimal? gia = may == null ? (decimal?)null : ThuCuRules.Tinh(may.GiaToiDa, f.NgoaiHinh, f.ManHinh, f.Pin, f.ChucNang);
            if (may != null && gia == null) ModelState.AddModelError("", "Tình trạng máy chưa hợp lệ, vui lòng chọn lại.");

            MayMoi moi = f.MayMoiId.HasValue ? DsMoi().FirstOrDefault(x => x.Id == f.MayMoiId.Value) : null;

            if (!ModelState.IsValid) return View(Build(f));

            string ma = "TC" + DateTime.Now.ToString("yyMMddHHmmss");
            string sdt = f.SoDienThoai.Replace(" ", "");
            db.Database.ExecuteSqlCommand(
                @"INSERT INTO YeuCauThuCu (MaYeuCau, NguoiDungId, HoTen, SoDienThoai, Email, BangGiaId, TenMay,
                    NgoaiHinh, ManHinh, Pin, ChucNang, GiaUocTinh, MayMuonMua, GhiChu)
                  VALUES (@ma, @uid, @ten, @sdt, @mail, @bg, @may, @nh, @mh, @pin, @cn, @gia, @moi, @gc)",
                new SqlParameter("@ma", ma),
                new SqlParameter("@uid", AuthHelper.IsLoggedIn ? (object)AuthHelper.UserId.Value : DBNull.Value),
                new SqlParameter("@ten", f.HoTen.Trim()),
                new SqlParameter("@sdt", sdt),
                new SqlParameter("@mail", string.IsNullOrWhiteSpace(f.Email) ? (object)DBNull.Value : f.Email.Trim()),
                new SqlParameter("@bg", may.Id),
                new SqlParameter("@may", may.Nhan),
                new SqlParameter("@nh", f.NgoaiHinh),
                new SqlParameter("@mh", f.ManHinh),
                new SqlParameter("@pin", f.Pin),
                new SqlParameter("@cn", f.ChucNang),
                new SqlParameter("@gia", gia.Value),
                new SqlParameter("@moi", moi == null ? (object)DBNull.Value : moi.Ten),
                new SqlParameter("@gc", string.IsNullOrWhiteSpace(f.GhiChu) ? (object)DBNull.Value : f.GhiChu.Trim()));

            // gửi mail xác nhận, lỗi mail không làm hỏng yêu cầu
            if (!string.IsNullOrWhiteSpace(f.Email))
            {
                string loi;
                string shop = CauHinh.Get("TenCuaHang", "PhoneStore");
                EmailHelper.Send(f.Email.Trim(), "Đã nhận yêu cầu thu cũ " + ma + " - " + shop,
                    "<p>Xin chào <b>" + System.Web.HttpUtility.HtmlEncode(f.HoTen) + "</b>,</p>" +
                    "<p>" + shop + " đã nhận yêu cầu thu cũ <b>" + System.Web.HttpUtility.HtmlEncode(may.Nhan) + "</b> (mã <b>" + ma + "</b>).</p>" +
                    "<p>Giá ước tính: <b>" + gia.Value.ToString("N0") + "đ</b>. Đây chỉ là giá tham khảo, giá chính thức sẽ được chốt sau khi nhân viên kiểm tra máy thực tế.</p>" +
                    "<p>Nhân viên sẽ liên hệ với bạn qua số điện thoại đã đăng ký.</p>", out loi);
            }

            return RedirectToAction("ThanhCong", new { ma = ma });
        }

        public ActionResult ThanhCong(string ma)
        {
            var yc = db.Database.SqlQuery<YeuCauThuCu>("SELECT TOP 1 * FROM YeuCauThuCu WHERE MaYeuCau = @ma",
                new SqlParameter("@ma", ma ?? "")).FirstOrDefault();
            if (yc == null) return HttpNotFound();
            return View(yc);
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}