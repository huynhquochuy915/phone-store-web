using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Helpers;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Models.ViewModels;
using WebsiteQuanLiDienThoaiDiDong.Services;

namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    [RequireLogin]
    public class DatHangController : Controller
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();
        private readonly CartService cart;
        public DatHangController() { cart = new CartService(db); }

        // Bước xác nhận: nhập thông tin + chọn thanh toán + mã khuyến mãi
        public ActionResult Index()
        {
            var gio = cart.GetCartVM();
            if (gio.Items.Count == 0) return RedirectToAction("Index", "GioHang");
            if (gio.CoLoi) { TempData["Err"] = "Có sản phẩm vượt quá số lượng tồn kho, vui lòng chỉnh lại giỏ hàng"; return RedirectToAction("Index", "GioHang"); }

            int uid = AuthHelper.UserId.Value;
            var user = db.NguoiDungs.Find(uid);
            var dc = db.DiaChiGiaoHangs.FirstOrDefault(d => d.NguoiDungId == uid && d.MacDinh);
            var vm = new CheckoutVM
            {
                TenNguoiNhan = dc != null ? dc.TenNguoiNhan : user.HoTen,
                SoDienThoai = dc != null ? dc.SoDienThoai : user.SoDienThoai,
                Email = user.Email,
                TinhThanh = dc != null ? dc.TinhThanh : null,
                DiaChiGiao = dc == null ? null : string.Join(", ",
                    new[] { dc.DiaChiChiTiet, dc.PhuongXa, dc.QuanHuyen }.Where(s => !string.IsNullOrWhiteSpace(s)))
            };
            Nap(vm, gio);
            return View(vm);
        }

        // Lưu đơn: gọi sp_DatHang (trừ tồn kho, áp mã, tạo đơn, timeline trong 1 transaction)
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Index(CheckoutVM vm)
        {
            var gio = cart.GetCartVM();
            if (gio.Items.Count == 0) return RedirectToAction("Index", "GioHang");
            if (vm.PhuongThucThanhToan != "COD" && vm.PhuongThucThanhToan != "QR")
                ModelState.AddModelError("PhuongThucThanhToan", "Vui lòng chọn phương thức thanh toán");
            if (!ModelState.IsValid) { Nap(vm, gio); return View(vm); }

            try
            {
                var ma = string.IsNullOrWhiteSpace(vm.MaKhuyenMai) ? null : vm.MaKhuyenMai.Trim();
                var kq = db.Database.SqlQuery<KetQuaDatHang>(
                    "EXEC sp_DatHang @NguoiDungId, @TenNguoiNhan, @SoDienThoai, @Email, @DiaChiGiao, @TinhThanh, @PhuongThucThanhToan, @MaKhuyenMai, @GhiChu",
                    new SqlParameter("@NguoiDungId", AuthHelper.UserId.Value),
                    new SqlParameter("@TenNguoiNhan", vm.TenNguoiNhan),
                    new SqlParameter("@SoDienThoai", vm.SoDienThoai),
                    new SqlParameter("@Email", vm.Email),
                    new SqlParameter("@DiaChiGiao", vm.DiaChiGiao),
                    new SqlParameter("@TinhThanh", vm.TinhThanh),
                    new SqlParameter("@PhuongThucThanhToan", vm.PhuongThucThanhToan),
                    new SqlParameter("@MaKhuyenMai", (object)ma ?? DBNull.Value),
                    new SqlParameter("@GhiChu", (object)vm.GhiChu ?? DBNull.Value)
                ).Single();

                // TODO (TV5): gửi email xác nhận đơn tại đây
                return RedirectToAction("ThanhCong", new { id = kq.DonHangId });
            }
            catch (Exception ex)
            {
                // lỗi nghiệp vụ trong sp_DatHang dùng THROW 5000x nên hiển thị được thẳng cho khách
                var sql = ex as SqlException ?? ex.InnerException as SqlException;
                ModelState.AddModelError("", sql != null && sql.Number >= 50000 ? sql.Message : "Đặt hàng thất bại, vui lòng thử lại");
                Nap(vm, cart.GetCartVM());
                return View(vm);
            }
        }

        public ActionResult ThanhCong(int id)
        {
            int uid = AuthHelper.UserId.Value;
            var dh = db.DonHangs.Include("ChiTietDonHangs").FirstOrDefault(d => d.Id == id && d.NguoiDungId == uid);
            if (dh == null) return HttpNotFound();

            decimal tong = dh.TongTienHang + dh.PhiShip - dh.TienGiam;
            ViewBag.Tong = tong;
            if (dh.PhuongThucThanhToan == "QR" && dh.TrangThaiThanhToan == "ChuaThanhToan" && dh.TrangThai == "ChoXacNhan")
            {
                ViewBag.QrUrl = VietQrHelper.BuildUrl(tong, dh.MaDonHang);
                int phut = int.Parse(CauHinh.Get("QR_HetHan_Phut", "30"));
                ViewBag.ConLaiGiay = Math.Max(0, (int)(dh.NgayDat.AddMinutes(phut) - DateTime.Now).TotalSeconds);
            }
            return View(dh);
        }

        // AJAX: tính lại phí ship + tiền giảm khi đổi tỉnh/thành hoặc nhập mã
        [HttpPost]
        public JsonResult TinhTien(string tinhThanh, string maKhuyenMai)
        {
            var gio = cart.GetCartVM();
            decimal phi = TinhPhiShip(tinhThanh);
            bool coMa = !string.IsNullOrWhiteSpace(maKhuyenMai);
            decimal giam = coMa ? TinhGiam(maKhuyenMai, gio.TongTien) : 0;
            return Json(new
            {
                phiShip = phi,
                tienGiam = giam,
                tong = gio.TongTien + phi - giam,
                message = !coMa ? "" : (giam == 0 ? "Mã không hợp lệ hoặc chưa đủ điều kiện" : "Đã áp dụng mã giảm giá")
            });
        }

        private void Nap(CheckoutVM vm, CartVM gio)
        {
            vm.Cart = gio;
            vm.DanhSachTinh = db.PhiVanChuyens.Where(p => p.TinhThanh != "Khác")
                                .OrderBy(p => p.TinhThanh).Select(p => p.TinhThanh).ToList();
            vm.PhiShip = TinhPhiShip(vm.TinhThanh);
            vm.TienGiam = string.IsNullOrWhiteSpace(vm.MaKhuyenMai) ? 0 : TinhGiam(vm.MaKhuyenMai, gio.TongTien);
        }

        private decimal TinhPhiShip(string tinh)
        {
            var phi = db.PhiVanChuyens.Where(p => p.TinhThanh == tinh).Select(p => (decimal?)p.PhiShip).FirstOrDefault();
            if (phi == null) phi = db.PhiVanChuyens.Where(p => p.TinhThanh == "Khác").Select(p => (decimal?)p.PhiShip).FirstOrDefault();
            return phi ?? 0;
        }

        private decimal TinhGiam(string ma, decimal tong)
        {
            return db.Database.SqlQuery<decimal>("SELECT dbo.fn_TinhTienGiam(@p0, @p1)", ma.Trim(), tong).FirstOrDefault();
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}