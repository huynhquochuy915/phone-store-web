using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Helpers;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Models.ViewModels;

namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    public class HangTV
    {
        public string Ten { get; set; }
        public decimal Nguong { get; set; }
        public string[] Quyen { get; set; }
    }

    public class DonRow
    {
        public int Id { get; set; }
        public string Ma { get; set; }
        public DateTime Ngay { get; set; }
        public string TrangThai { get; set; }
        public decimal Tong { get; set; }
        public string TenSP { get; set; }
        public int ConLai { get; set; }
        public string Anh { get; set; }
        public bool ChoThanhToanQR { get; set; }
    }

    public class CanhBao
    {
        public string Key { get; set; }
        public string Text { get; set; }
        public string LinkText { get; set; }
        public string Url { get; set; }
        public bool DongDuoc { get; set; }
    }

    public class TongQuanVM
    {
        public string HoTen { get; set; }
        public string ChuCai { get; set; }
        public string Email { get; set; }
        public string Sdt { get; set; }
        public string SdtAn { get; set; }
        public DateTime? NgayTao { get; set; }
        public int TongDon { get; set; }
        public decimal TichLuy { get; set; }
        public int HangIdx { get; set; }
        public decimal ConThieu { get; set; }      // còn thiếu bao nhiêu để lên hạng kế tiếp (0 nếu đã cao nhất)
        public int PhanTram { get; set; }          // % tiến độ lên hạng kế tiếp
        public List<DonRow> Don { get; set; }
        public List<SanPhamCardVM> YeuThich { get; set; }
        public int SoYeuThich { get; set; }
        public int SoDiaChi { get; set; }
        public int ThuCuDangXuLy { get; set; }
        public int ThuCuDaDinhGia { get; set; }
        public List<CanhBao> CanhBaos { get; set; }
    }

    public class ThongTinVM
    {
        public string HoTen { get; set; }
        public string Sdt { get; set; }
        public string Email { get; set; }
    }

    public class DiaChiVM
    {
        public List<DiaChiGiaoHang> Ds { get; set; }
        public List<string> Tinh { get; set; }
    }

    public class ImgRow
    {
        public int DonHangId { get; set; }
        public string Anh { get; set; }
    }

    [RequireLogin]
    public class ThanhVienController : Controller
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();
        private int Uid { get { return AuthHelper.UserId.Value; } }

        // ===== hạng thành viên: số liệu minh họa, chỉnh tùy ý. Quyền lợi chỉ để hiển thị. =====
        public static readonly HangTV[] Hangs = new[]
        {
            new HangTV { Ten = "Thành viên", Nguong = 0,        Quyen = new[] { "Tích lũy theo các đơn đã giao thành công", "Nhận thông báo đơn hàng và thu cũ" } },
            new HangTV { Ten = "Bạc",        Nguong = 10000000, Quyen = new[] { "Tất cả quyền lợi hạng Thành viên", "Nhận thông tin ưu đãi sớm" } },
            new HangTV { Ten = "Vàng",       Nguong = 30000000, Quyen = new[] { "Tất cả quyền lợi hạng Bạc", "Ưu tiên hỗ trợ bảo hành" } },
            new HangTV { Ten = "Kim cương",  Nguong = 60000000, Quyen = new[] { "Tất cả quyền lợi hạng Vàng", "Ưu tiên giao hàng và hỗ trợ riêng" } }
        };

        public static string TenTT(string t)
        {
            switch (t)
            {
                case "DaXacNhan": return "Đã xác nhận";
                case "DangGiao": return "Đang giao";
                case "HoanTat": return "Đã giao";
                case "DaHuy": return "Đã hủy";
                default: return "Chờ xác nhận";
            }
        }

        public static string MauTT(string t)
        {
            switch (t)
            {
                case "DaXacNhan":
                case "DangGiao": return "adm-badge-blue";
                case "HoanTat": return "adm-badge-green";
                case "DaHuy": return "adm-badge-red";
                default: return "adm-badge-amber";
            }
        }

        public static string AnSdt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.Trim();
            return s.Length >= 7 ? s.Substring(0, 3) + "*****" + s.Substring(s.Length - 2) : s;
        }

        private static string Hash(string input)
        {
            using (var sha = SHA256.Create())
            {
                var b = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder();
                foreach (var x in b) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }

        private TongQuanVM Build(bool day)
        {
            var u = db.NguoiDungs.Find(Uid);
            string ten = u.HoTen ?? "";
            var vm = new TongQuanVM
            {
                HoTen = ten,
                ChuCai = ten.Trim().Length > 0 ? ten.Trim().Substring(0, 1).ToUpper() : "?",
                Email = u.Email,
                Sdt = u.SoDienThoai,
                SdtAn = !string.IsNullOrWhiteSpace(u.SoDienThoai) ? AnSdt(u.SoDienThoai) : u.Email,
                NgayTao = u.NgayTao,
                CanhBaos = new List<CanhBao>()
            };

            var donAll = db.DonHangs.Where(d => d.NguoiDungId == Uid).ToList();
            vm.TongDon = donAll.Count(d => d.TrangThai != "DaHuy");
            vm.TichLuy = donAll.Where(d => d.TrangThai == "HoanTat").Sum(d => d.TongTienHang + d.PhiShip - d.TienGiam);

            int idx = 0;
            for (int i = 0; i < Hangs.Length; i++) if (vm.TichLuy >= Hangs[i].Nguong) idx = i;
            vm.HangIdx = idx;
            if (idx < Hangs.Length - 1)
            {
                var cur = Hangs[idx]; var nxt = Hangs[idx + 1];
                vm.ConThieu = nxt.Nguong - vm.TichLuy;
                vm.PhanTram = (int)Math.Min(100, Math.Max(0, (vm.TichLuy - cur.Nguong) * 100 / (nxt.Nguong - cur.Nguong)));
            }
            else vm.PhanTram = 100;

            vm.SoDiaChi = db.DiaChiGiaoHangs.Count(x => x.NguoiDungId == Uid);
            vm.SoYeuThich = db.YeuThiches.Count(x => x.NguoiDungId == Uid);

            // yêu cầu thu cũ (bỏ qua nếu chưa chạy ThuCu.sql)
            try
            {
                string mail = u.Email;
                const string dk = "(NguoiDungId = @u OR (@e IS NOT NULL AND Email = @e))";
                vm.ThuCuDangXuLy = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM YeuCauThuCu WHERE " + dk + " AND TrangThai NOT IN ('HoanTat','Huy')",
                    new SqlParameter("@u", Uid), new SqlParameter("@e", (object)mail ?? DBNull.Value)).First();
                vm.ThuCuDaDinhGia = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM YeuCauThuCu WHERE " + dk + " AND TrangThai = 'DaDinhGia'",
                    new SqlParameter("@u", Uid), new SqlParameter("@e", (object)mail ?? DBNull.Value)).First();
            }
            catch { }

            if (day)
            {
                // đơn hàng gần đây
                var recent = db.DonHangs.Include("ChiTietDonHangs").Where(d => d.NguoiDungId == Uid)
                    .OrderByDescending(d => d.NgayDat).Take(3).ToList();
                var imgs = new Dictionary<int, string>();
                if (recent.Any())
                {
                    try
                    {
                        string ids = string.Join(",", recent.Select(d => d.Id));
                        foreach (var r in db.Database.SqlQuery<ImgRow>(
                            "SELECT c.DonHangId AS DonHangId, MIN(s.AnhDaiDien) AS Anh FROM ChiTietDonHang c " +
                            "JOIN BienTheSanPham b ON b.Id = c.BienTheId JOIN SanPham s ON s.Id = b.SanPhamId " +
                            "WHERE c.DonHangId IN (" + ids + ") GROUP BY c.DonHangId").ToList())
                            imgs[r.DonHangId] = r.Anh;
                    }
                    catch { }   // không lấy được ảnh thì hiện biểu tượng
                }
                vm.Don = recent.Select(d =>
                {
                    var ct = d.ChiTietDonHangs.ToList();
                    string anh; imgs.TryGetValue(d.Id, out anh);
                    return new DonRow
                    {
                        Id = d.Id,
                        Ma = d.MaDonHang,
                        Ngay = d.NgayDat,
                        TrangThai = d.TrangThai,
                        Tong = d.TongTienHang + d.PhiShip - d.TienGiam,
                        TenSP = ct.Count > 0 ? ct[0].TenSanPham : "Đơn hàng",
                        ConLai = Math.Max(0, ct.Count - 1),
                        Anh = anh,
                        ChoThanhToanQR = d.PhuongThucThanhToan == "QR" && d.TrangThaiThanhToan == "ChuaThanhToan" && d.TrangThai == "ChoXacNhan"
                    };
                }).ToList();

                // yêu thích
                var fav = (from f in db.YeuThiches
                           join p in db.vw_SanPhamHienThi on f.SanPhamId equals p.Id
                           where f.NguoiDungId == Uid
                           orderby f.NgayThem descending
                           select p).Take(6).ToList();
                vm.YeuThich = fav.Select(x => new SanPhamCardVM
                {
                    Id = x.Id,
                    LoaiId = x.LoaiId,
                    TenSanPham = x.TenSanPham,
                    AnhDaiDien = x.AnhDaiDien,
                    GiaThapNhat = x.GiaThapNhat,
                    GiaCaoNhat = x.GiaCaoNhat,
                    DaYeuThich = true
                }).ToList();

                // các thông báo gợi ý
                var qr = vm.Don.FirstOrDefault(d => d.ChoThanhToanQR);
                if (qr != null)
                    vm.CanhBaos.Add(new CanhBao { Key = "qr", Text = "Đơn hàng " + qr.Ma + " đang chờ bạn thanh toán bằng mã QR.", LinkText = "Thanh toán", Url = Url.Action("ThanhCong", "DatHang", new { area = "", id = qr.Id }), DongDuoc = false });
                if (vm.ThuCuDaDinhGia > 0)
                    vm.CanhBaos.Add(new CanhBao { Key = "thucu" + vm.ThuCuDaDinhGia, Text = "Có " + vm.ThuCuDaDinhGia + " yêu cầu thu cũ đã được định giá chính thức.", LinkText = "Xem ngay", Url = Url.Action("TraCuu", "ThuCu", new { area = "" }), DongDuoc = true });
                if (vm.ConThieu > 0)
                    vm.CanhBaos.Add(new CanhBao { Key = "hang" + vm.HangIdx, Text = "Mua thêm " + vm.ConThieu.ToString("N0") + "đ để lên hạng " + Hangs[vm.HangIdx + 1].Ten + ".", LinkText = "Xem hạng", Url = Url.Action("Hang", "ThanhVien", new { area = "" }), DongDuoc = true });
                if (vm.SoDiaChi == 0)
                    vm.CanhBaos.Add(new CanhBao { Key = "diachi", Text = "Thêm địa chỉ để đặt hàng nhanh hơn.", LinkText = "Thêm địa chỉ", Url = Url.Action("DiaChi", "ThanhVien", new { area = "" }), DongDuoc = true });
            }
            return vm;
        }

        public ActionResult Index() { return View(Build(true)); }

        public ActionResult Hang() { return View(Build(false)); }

        // ===== thông tin tài khoản =====
        [HttpGet]
        public ActionResult ThongTin()
        {
            var u = db.NguoiDungs.Find(Uid);
            return View(new ThongTinVM { HoTen = u.HoTen, Sdt = u.SoDienThoai, Email = u.Email });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult CapNhatThongTin(string hoTen, string soDienThoai)
        {
            hoTen = (hoTen ?? "").Trim();
            soDienThoai = string.IsNullOrWhiteSpace(soDienThoai) ? null : soDienThoai.Replace(" ", "");
            if (hoTen == "" || hoTen.Length > 100) { TempData["Err"] = "Họ tên không được để trống và tối đa 100 ký tự."; return RedirectToAction("ThongTin"); }
            if (soDienThoai != null && !Regex.IsMatch(soDienThoai, @"^(0|\+84)\d{9,10}$")) { TempData["Err"] = "Số điện thoại không hợp lệ."; return RedirectToAction("ThongTin"); }
            if (soDienThoai != null && db.NguoiDungs.Any(x => x.Id != Uid && x.SoDienThoai == soDienThoai))
            { TempData["Err"] = "Số điện thoại này đã được tài khoản khác sử dụng."; return RedirectToAction("ThongTin"); }

            var u = db.NguoiDungs.Find(Uid);
            u.HoTen = hoTen;
            u.SoDienThoai = soDienThoai;
            db.SaveChanges();
            Session["HoTen"] = hoTen;
            TempData["Msg"] = "Đã cập nhật thông tin tài khoản.";
            return RedirectToAction("ThongTin");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DoiMatKhau(string matKhauCu, string matKhauMoi, string xacNhan)
        {
            var u = db.NguoiDungs.Find(Uid);
            if (string.IsNullOrEmpty(matKhauCu) || Hash(matKhauCu) != u.MatKhauHash)
            { TempData["Err"] = "Mật khẩu hiện tại không đúng. Đăng nhập bằng Google/Facebook thì hãy dùng \"Quên mật khẩu\" để tạo mật khẩu."; return RedirectToAction("ThongTin"); }
            if (string.IsNullOrEmpty(matKhauMoi) || matKhauMoi.Length < 6) { TempData["Err"] = "Mật khẩu mới phải có ít nhất 6 ký tự."; return RedirectToAction("ThongTin"); }
            if (matKhauMoi != xacNhan) { TempData["Err"] = "Mật khẩu xác nhận không khớp."; return RedirectToAction("ThongTin"); }
            u.MatKhauHash = Hash(matKhauMoi);
            db.SaveChanges();
            TempData["Msg"] = "Đã đổi mật khẩu.";
            return RedirectToAction("ThongTin");
        }

        // ===== sổ địa chỉ =====
        public ActionResult DiaChi()
        {
            return View(new DiaChiVM
            {
                Ds = db.DiaChiGiaoHangs.Where(x => x.NguoiDungId == Uid).OrderByDescending(x => x.MacDinh).ThenByDescending(x => x.Id).ToList(),
                Tinh = db.PhiVanChuyens.Where(p => p.TinhThanh != "Khác").OrderBy(p => p.TinhThanh).Select(p => p.TinhThanh).ToList()
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ThemDiaChi(string tenNguoiNhan, string soDienThoai, string tinhThanh, string quanHuyen, string phuongXa, string diaChiChiTiet, bool macDinh = false)
        {
            soDienThoai = (soDienThoai ?? "").Replace(" ", "");
            if (string.IsNullOrWhiteSpace(tenNguoiNhan) || string.IsNullOrWhiteSpace(tinhThanh) || string.IsNullOrWhiteSpace(quanHuyen)
                || string.IsNullOrWhiteSpace(phuongXa) || string.IsNullOrWhiteSpace(diaChiChiTiet))
            { TempData["Err"] = "Vui lòng nhập đầy đủ thông tin địa chỉ."; return RedirectToAction("DiaChi"); }
            if (!Regex.IsMatch(soDienThoai, @"^(0|\+84)\d{9,10}$")) { TempData["Err"] = "Số điện thoại không hợp lệ."; return RedirectToAction("DiaChi"); }

            try
            {
                bool dau = !db.DiaChiGiaoHangs.Any(x => x.NguoiDungId == Uid);
                if (macDinh || dau)
                    foreach (var x in db.DiaChiGiaoHangs.Where(x => x.NguoiDungId == Uid && x.MacDinh).ToList()) x.MacDinh = false;
                db.DiaChiGiaoHangs.Add(new DiaChiGiaoHang
                {
                    NguoiDungId = Uid,
                    TenNguoiNhan = tenNguoiNhan.Trim(),
                    SoDienThoai = soDienThoai,
                    TinhThanh = tinhThanh,
                    QuanHuyen = quanHuyen.Trim(),
                    PhuongXa = phuongXa.Trim(),
                    DiaChiChiTiet = diaChiChiTiet.Trim(),
                    MacDinh = macDinh || dau
                });
                db.SaveChanges();
                TempData["Msg"] = "Đã thêm địa chỉ.";
            }
            catch (Exception ex) { TempData["Err"] = "Không lưu được địa chỉ: " + ex.GetBaseException().Message; }
            return RedirectToAction("DiaChi");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DatMacDinh(int id)
        {
            var ds = db.DiaChiGiaoHangs.Where(x => x.NguoiDungId == Uid).ToList();
            if (ds.Any(x => x.Id == id))
            {
                foreach (var x in ds) x.MacDinh = x.Id == id;
                db.SaveChanges();
                TempData["Msg"] = "Đã đặt làm địa chỉ mặc định.";
            }
            return RedirectToAction("DiaChi");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult XoaDiaChi(int id)
        {
            var x = db.DiaChiGiaoHangs.FirstOrDefault(a => a.Id == id && a.NguoiDungId == Uid);
            if (x != null)
            {
                bool laMacDinh = x.MacDinh;
                db.DiaChiGiaoHangs.Remove(x);
                db.SaveChanges();
                if (laMacDinh)
                {
                    var khac = db.DiaChiGiaoHangs.Where(a => a.NguoiDungId == Uid).OrderByDescending(a => a.Id).FirstOrDefault();
                    if (khac != null) { khac.MacDinh = true; db.SaveChanges(); }
                }
                TempData["Msg"] = "Đã xóa địa chỉ.";
            }
            return RedirectToAction("DiaChi");
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}