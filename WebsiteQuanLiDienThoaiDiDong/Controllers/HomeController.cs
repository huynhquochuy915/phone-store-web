using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Helpers;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Models.ViewModels;
using WebsiteQuanLiDienThoaiDiDong.Services;
namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    public class HomeController : Controller
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();

        public ActionResult Index()
        {
            // lấy thêm GiaGoc của biến thể rẻ nhất để tính % giảm
            const string cot = "SELECT TOP 10 v.*, (SELECT TOP 1 b.GiaGoc FROM BienTheSanPham b " +
                               "WHERE b.SanPhamId = v.Id AND b.HienThi = 1 ORDER BY b.GiaBan) AS GiaGoc " +
                               "FROM vw_SanPhamHienThi v ";
            int hangCu; int.TryParse(CauHinh.Get("LoaiId_HangCu", "0"), out hangCu);
            var vm = new TrangChuVM
            {
                NoiBat = db.Database.SqlQuery<SanPhamCardVM>(
                    cot + "WHERE v.NoiBat = 1 ORDER BY v.DaBan DESC, v.LuotXem DESC").ToList(),
                BanChay = db.Database.SqlQuery<SanPhamCardVM>(
                    cot + "ORDER BY v.DaBan DESC, v.LuotXem DESC").ToList(),
                DienThoai = db.Database.SqlQuery<SanPhamCardVM>(
                     cot + "WHERE v.LoaiId = 1 ORDER BY v.DaBan DESC, v.LuotXem DESC").ToList(),
                PhuKien = db.Database.SqlQuery<SanPhamCardVM>(
                     cot + "WHERE v.LoaiId <> 1 AND v.LoaiId <> " + hangCu + " ORDER BY v.DaBan DESC, v.LuotXem DESC").ToList(),
                MayCu = hangCu == 0 ? new List<SanPhamCardVM>() : db.Database.SqlQuery<SanPhamCardVM>(
                     cot + "WHERE v.LoaiId = " + hangCu + " ORDER BY v.DaBan DESC, v.LuotXem DESC").ToList(),
                Tin = NewsService.GetNews(4),
                Video = NewsService.GetVideos(3)
            };
            return View(vm);
        }

        // Menu 2 cấp: Loại sản phẩm -> Thương hiệu (lấy từ DB)
        [ChildActionOnly]
        public ActionResult Menu()
        {
            var loais = db.LoaiSanPhams.Where(x => x.HienThi).OrderBy(x => x.Id).ToList();
            var vm = new List<MenuLoaiVM>();
            foreach (var l in loais)
            {
                var hangs = db.SanPhams.Where(s => s.HienThi && s.LoaiId == l.Id)
                    .Select(s => s.ThuongHieu).Distinct().OrderBy(t => t.TenThuongHieu).ToList();
                vm.Add(new MenuLoaiVM
                {
                    Id = l.Id,
                    LoaiId = l.Id,
                    Ten = l.TenLoai,
                    ThuongHieus = hangs.Select(t => new MenuThuongHieuVM { Id = t.Id, Ten = t.TenThuongHieu }).ToList()
                });
            }
            return PartialView(vm);
        }
        // danh sách thông báo cho khung tài khoản (gọi bằng fetch sau khi trang tải)
        [HttpGet]
        public JsonResult ThongBao()
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            if (!AuthHelper.IsLoggedIn) return Json(new { ok = false }, JsonRequestBehavior.AllowGet);

            int uid = AuthHelper.UserId.Value;
            var items = new List<ThongBaoItem>();

            // 1. đơn hàng
            foreach (var d in db.DonHangs.Where(x => x.NguoiDungId == uid).OrderByDescending(x => x.NgayDat).Take(10).ToList())
            {
                string text, icon = "bi-cart-check";
                string link = Url.Action("Index", "DonHang", new { area = "" });
                switch (d.TrangThai)
                {
                    case "DaXacNhan": text = "Đơn hàng " + d.MaDonHang + " đã được xác nhận và đang được chuẩn bị."; break;
                    case "DangGiao": text = "Đơn hàng " + d.MaDonHang + " đang trên đường giao đến bạn."; icon = "bi-truck"; break;
                    case "HoanTat": text = "Đơn hàng " + d.MaDonHang + " đã giao thành công. Cảm ơn bạn đã mua hàng!"; icon = "bi-bag-check"; break;
                    case "DaHuy": text = "Đơn hàng " + d.MaDonHang + " đã được hủy."; icon = "bi-bag-x"; break;
                    default: text = "Cảm ơn bạn đã đặt hàng. Đơn hàng " + d.MaDonHang + " đang chờ xác nhận."; break;
                }
                if (d.PhuongThucThanhToan == "QR" && d.TrangThaiThanhToan == "ChuaThanhToan" && d.TrangThai == "ChoXacNhan")
                {
                    text = "Đơn hàng " + d.MaDonHang + " đang chờ bạn thanh toán bằng mã QR.";
                    icon = "bi-qr-code";
                    link = Url.Action("ThanhCong", "DatHang", new { area = "", id = d.Id });
                }
                items.Add(new ThongBaoItem
                {
                    Loai = "order",
                    Ma = d.MaDonHang,
                    Text = text,
                    Icon = icon,
                    Url = link,
                    Sig = "o:" + d.Id + ":" + d.TrangThai + ":" + d.TrangThaiThanhToan,
                    Ts = d.NgayDat.ToString("yyyy-MM-dd'T'HH:mm:ss")
                });
            }

            // 2. yêu cầu thu cũ (bỏ qua nếu chưa chạy ThuCu.sql)
            try
            {
                var u = db.NguoiDungs.Find(uid);
                string mail = u == null ? null : u.Email;
                var ds = db.Database.SqlQuery<YeuCauThuCu>(
                    "SELECT TOP 5 * FROM YeuCauThuCu WHERE NguoiDungId = @u OR (@e IS NOT NULL AND Email = @e) ORDER BY Id DESC",
                    new SqlParameter("@u", uid), new SqlParameter("@e", (object)mail ?? DBNull.Value)).ToList();
                foreach (var r in ds)
                {
                    string text;
                    switch (r.TrangThai)
                    {
                        case "DaLienHe": text = "Nhân viên đã liên hệ với bạn về yêu cầu thu cũ " + r.MaYeuCau + "."; break;
                        case "DaDinhGia": text = "Yêu cầu thu cũ " + r.MaYeuCau + " đã được định giá: " + (r.GiaChot ?? r.GiaUocTinh).ToString("N0") + "đ."; break;
                        case "HoanTat": text = "Hoàn tất thu cũ " + r.MaYeuCau + ", giá chốt " + (r.GiaChot ?? r.GiaUocTinh).ToString("N0") + "đ."; break;
                        case "Huy": text = "Yêu cầu thu cũ " + r.MaYeuCau + " đã được đóng."; break;
                        default: text = "Đã nhận yêu cầu thu cũ " + r.MaYeuCau + " (" + r.TenMay + "). Nhân viên sẽ liên hệ với bạn."; break;
                    }
                    items.Add(new ThongBaoItem
                    {
                        Loai = "trade",
                        Ma = r.MaYeuCau,
                        Text = text,
                        Icon = "bi-arrow-repeat",
                        Url = Url.Action("TraCuu", "ThuCu", new { area = "", ma = r.MaYeuCau, sdt = r.SoDienThoai }),
                        Sig = "t:" + r.Id + ":" + r.TrangThai,
                        Ts = (r.NgayCapNhat ?? r.NgayTao).ToString("yyyy-MM-dd'T'HH:mm:ss")
                    });
                }
            }
            catch { }

            return Json(new { ok = true, items = items.OrderByDescending(x => x.Ts).Take(15).ToList() }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
    public class ThongBaoItem
    {
        public string Loai { get; set; }
        public string Sig { get; set; }
        public string Ma { get; set; }
        public string Text { get; set; }
        public string Icon { get; set; }
        public string Url { get; set; }
        public string Ts { get; set; }
    }
}