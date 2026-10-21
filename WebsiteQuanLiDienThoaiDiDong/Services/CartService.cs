using System;
using System.Data.Entity;
using System.Linq;
using System.Web;
using WebsiteQuanLiDienThoaiDiDong.Helpers;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Models.ViewModels;

namespace WebsiteQuanLiDienThoaiDiDong.Services
{
    public class CartService
    {
        private const string CookieName = "cart_token";
        private readonly QL_DienThoaiEntities db;
        private GioHang _cart;
        private string _guestToken;

        public CartService(QL_DienThoaiEntities db) { this.db = db; }

        private string GetGuestToken(bool create)
        {
            if (_guestToken != null) return _guestToken;
            var ck = HttpContext.Current.Request.Cookies[CookieName];
            if (ck != null && !string.IsNullOrEmpty(ck.Value)) return _guestToken = ck.Value;
            if (!create) return null;
            _guestToken = Guid.NewGuid().ToString("N");
            HttpContext.Current.Response.Cookies.Add(
                new HttpCookie(CookieName, _guestToken) { HttpOnly = true, Expires = DateTime.Now.AddDays(30) });
            return _guestToken;
        }

        private GioHang TaoGio(int? userId, string token)
        {
            var g = new GioHang { NguoiDungId = userId, SessionToken = token, NgayCapNhat = DateTime.Now };
            db.GioHangs.Add(g);
            db.SaveChanges();
            return g;
        }

        public GioHang GetCart(bool create)
        {
            if (_cart != null) return _cart;
            var uid = AuthHelper.UserId;
            if (uid.HasValue)
            {
                int id = uid.Value;
                _cart = db.GioHangs.FirstOrDefault(g => g.NguoiDungId == id);
                if (_cart == null && create) _cart = TaoGio(id, null);
            }
            else
            {
                var token = GetGuestToken(create);
                if (token == null) return null;
                _cart = db.GioHangs.FirstOrDefault(g => g.SessionToken == token && g.NguoiDungId == null);
                if (_cart == null && create) _cart = TaoGio(null, token);
            }
            return _cart;
        }

        // Trả về null nếu thành công, ngược lại là thông báo lỗi
        public string AddItem(int bienTheId, int soLuong)
        {
            if (soLuong < 1) soLuong = 1;
            var bt = db.BienTheSanPhams.FirstOrDefault(b => b.Id == bienTheId && b.HienThi && b.SanPham.HienThi);
            if (bt == null) return "Sản phẩm không tồn tại hoặc đã ngừng bán";
            if (bt.SoLuongTon <= 0) return "Sản phẩm này đã hết hàng";

            var cart = GetCart(true);
            var line = db.GioHangChiTiets.FirstOrDefault(x => x.GioHangId == cart.Id && x.BienTheId == bienTheId);
            int moi = (line == null ? 0 : line.SoLuong) + soLuong;
            if (moi > bt.SoLuongTon) return "Kho chỉ còn " + bt.SoLuongTon + " sản phẩm";

            if (line == null)
                db.GioHangChiTiets.Add(new GioHangChiTiet { GioHangId = cart.Id, BienTheId = bienTheId, SoLuong = soLuong });
            else
                line.SoLuong = moi;
            cart.NgayCapNhat = DateTime.Now;
            db.SaveChanges();
            return null;
        }

        public string UpdateQty(int lineId, int soLuong)
        {
            var cart = GetCart(false);
            if (cart == null) return "Giỏ hàng trống";
            var line = db.GioHangChiTiets.Include("BienTheSanPham")
                         .FirstOrDefault(x => x.Id == lineId && x.GioHangId == cart.Id);
            if (line == null) return "Không tìm thấy sản phẩm trong giỏ";
            if (soLuong <= 0) { db.GioHangChiTiets.Remove(line); db.SaveChanges(); return null; }
            if (soLuong > line.BienTheSanPham.SoLuongTon) return "Kho chỉ còn " + line.BienTheSanPham.SoLuongTon + " sản phẩm";
            line.SoLuong = soLuong;
            db.SaveChanges();
            return null;
        }

        public void RemoveItem(int lineId)
        {
            var cart = GetCart(false);
            if (cart == null) return;
            var line = db.GioHangChiTiets.FirstOrDefault(x => x.Id == lineId && x.GioHangId == cart.Id);
            if (line == null) return;
            db.GioHangChiTiets.Remove(line);
            db.SaveChanges();
        }

        public CartVM GetCartVM()
        {
            var vm = new CartVM();
            var cart = GetCart(false);
            if (cart == null) return vm;
            vm.Items = (from c in db.GioHangChiTiets
                        where c.GioHangId == cart.Id
                        orderby c.Id
                        select new CartItemVM
                        {
                            LineId = c.Id,
                            BienTheId = c.BienTheId,
                            SanPhamId = c.BienTheSanPham.SanPhamId,
                            Ten = c.BienTheSanPham.SanPham.TenSanPham,
                            MauSac = c.BienTheSanPham.MauSac,
                            DungLuong = c.BienTheSanPham.DungLuong,
                            Anh = c.BienTheSanPham.SanPham.AnhDaiDien,
                            DonGia = c.BienTheSanPham.GiaBan,
                            SoLuong = c.SoLuong,
                            Ton = c.BienTheSanPham.SoLuongTon
                        }).ToList();
            return vm;
        }

        public int CountItems()
        {
            var cart = GetCart(false);
            if (cart == null) return 0;
            return db.GioHangChiTiets.Where(x => x.GioHangId == cart.Id).Sum(x => (int?)x.SoLuong) ?? 0;
        }

        // Gọi sau đăng nhập: gộp giỏ khách (cookie) vào giỏ tài khoản bằng sp_GopGioHang
        public void MergeGuestCart(int userId)
        {
            var ck = HttpContext.Current.Request.Cookies[CookieName];
            if (ck == null || string.IsNullOrEmpty(ck.Value)) return;
            db.Database.ExecuteSqlCommand("EXEC sp_GopGioHang @p0, @p1", ck.Value, userId);
            HttpContext.Current.Response.Cookies.Add(new HttpCookie(CookieName, "") { Expires = DateTime.Now.AddDays(-1) });
        }
    }
}