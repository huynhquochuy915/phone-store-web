using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Areas.Admin.Controllers
{
    public class ThongKeController : AdminBaseController
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();
        private static readonly string[] TatCaTrangThai = { "ChoXacNhan", "DaXacNhan", "DangGiao", "HoanTat", "DaHuy" };

        public static string NhanTrangThai(string tt)
        {
            switch (tt)
            {
                case "ChoXacNhan": return "Chờ xác nhận";
                case "DaXacNhan": return "Đã xác nhận";
                case "DangGiao": return "Đang giao";
                case "HoanTat": return "Hoàn tất";
                case "DaHuy": return "Đã hủy";
                default: return tt;
            }
        }
        public static string MauTrangThai(string tt)
        {
            switch (tt)
            {
                case "ChoXacNhan": return "amber";
                case "DaXacNhan": return "blue";
                case "DangGiao": return "blue";
                case "HoanTat": return "green";
                default: return "red";
            }
        }

        public ActionResult Index(int? nam, int? thang)
        {
            int homNay = DateTime.Now.Year;
            int n = (nam.HasValue && nam.Value >= 2000 && nam.Value <= 2100) ? nam.Value : homNay;
            var dauNam = new DateTime(n, 1, 1);
            var cuoiNam = dauNam.AddYears(1);

            var vm = new ThongKeVM
            {
                Nam = n,
                DoanhThuThang = new decimal[12],
                DoanhThuNamTruoc = new decimal[12],
                SoDonThang = new int[12]
            };

            LayDoanhThu(n, vm.DoanhThuThang, vm.SoDonThang);
            LayDoanhThu(n - 1, vm.DoanhThuNamTruoc, new int[12]);

            vm.DoanhThuNam = vm.DoanhThuThang.Sum();
            vm.DoanhThuNamTruocTong = vm.DoanhThuNamTruoc.Sum();
            vm.SoDonHoanTat = vm.SoDonThang.Sum();
            vm.GiaTriTrungBinh = vm.SoDonHoanTat > 0 ? Math.Round(vm.DoanhThuNam / vm.SoDonHoanTat) : 0;
            if (vm.DoanhThuNamTruocTong > 0)
                vm.TangTruong = Math.Round((vm.DoanhThuNam - vm.DoanhThuNamTruocTong) / vm.DoanhThuNamTruocTong * 100, 1);

            // đơn theo trạng thái (tính theo ngày đặt trong năm)
            var dem = db.DonHangs.Where(d => d.NgayDat >= dauNam && d.NgayDat < cuoiNam)
                .GroupBy(d => d.TrangThai)
                .Select(g => new { TrangThai = g.Key, SoLuong = g.Count() }).ToList();
            vm.TrangThaiDon = TatCaTrangThai.Select(t => new TrangThaiDemVM
            {
                Ten = NhanTrangThai(t),
                SoLuong = dem.Where(x => x.TrangThai == t).Select(x => x.SoLuong).FirstOrDefault()
            }).ToList();

            vm.DonChoXacNhan = db.DonHangs.Count(d => d.TrangThai == "ChoXacNhan");

            // top 5 sản phẩm bán chạy trong năm
            vm.TopBanChay = db.ChiTietDonHangs
                .Where(c => c.DonHang.TrangThai == "HoanTat" && c.DonHang.NgayGiaoXong >= dauNam && c.DonHang.NgayGiaoXong < cuoiNam)
                .GroupBy(c => new { Id = c.BienTheSanPham.SanPham.Id, Ten = c.BienTheSanPham.SanPham.TenSanPham })
                .Select(g => new TopSanPhamVM
                {
                    SanPhamId = g.Key.Id,
                    Ten = g.Key.Ten,
                    SoLuong = g.Sum(x => x.SoLuong),
                    DoanhThu = g.Sum(x => x.SoLuong * x.DonGia)
                })
                .OrderByDescending(x => x.SoLuong).Take(5).ToList();

            // cảnh báo tồn kho
            var canhBao = db.BienTheSanPhams.Where(b => b.HienThi && b.SanPham.HienThi && b.SoLuongTon <= b.NguongCanhBao);
            vm.SoCanhBaoTon = canhBao.Count();
            vm.CanhBaoTon = canhBao.OrderBy(b => b.SoLuongTon).Take(6)
                .Select(b => new CanhBaoTonVM
                {
                    BienTheId = b.Id,
                    Ten = b.SanPham.TenSanPham,
                    DungLuong = b.DungLuong,
                    MauSac = b.MauSac,
                    SoLuongTon = b.SoLuongTon
                }).ToList();

            // đơn gần đây
            vm.DonGanDay = db.DonHangs.OrderByDescending(d => d.NgayDat).Take(6)
                .Select(d => new DonGanDayVM
                {
                    MaDonHang = d.MaDonHang,
                    TenNguoiNhan = d.TenNguoiNhan,
                    Tong = d.TongThanhToan,
                    TrangThai = d.TrangThai,
                    NgayDat = d.NgayDat
                }).ToList();

            // chi tiết đơn của tháng được chọn
            if (thang.HasValue && thang.Value >= 1 && thang.Value <= 12)
            {
                vm.Thang = thang.Value;
                var tu = new DateTime(n, thang.Value, 1);
                var den = tu.AddMonths(1);
                vm.DonTrongThang = db.DonHangs
                    .Where(d => d.TrangThai == "HoanTat" && d.NgayGiaoXong >= tu && d.NgayGiaoXong < den)
                    .OrderByDescending(d => d.NgayGiaoXong)
                    .Select(d => new DonThangVM
                    {
                        MaDonHang = d.MaDonHang,
                        TenNguoiNhan = d.TenNguoiNhan,
                        SoDienThoai = d.SoDienThoai,
                        TongTienHang = d.TongTienHang,
                        TienGiam = d.TienGiam,
                        TongThanhToan = d.TongThanhToan,
                        NgayGiaoXong = d.NgayGiaoXong
                    }).ToList();
            }

            return View(vm);
        }

        // doanh thu = tiền hàng sau giảm giá của đơn HoanTat, gom theo tháng giao xong
        private void LayDoanhThu(int nam, decimal[] doanhThu, int[] soDon)
        {
            var dau = new DateTime(nam, 1, 1);
            var cuoi = dau.AddYears(1);
            var rows = db.DonHangs
                .Where(d => d.TrangThai == "HoanTat" && d.NgayGiaoXong >= dau && d.NgayGiaoXong < cuoi)
                .GroupBy(d => d.NgayGiaoXong.Value.Month)
                .Select(g => new { Thang = g.Key, DoanhThu = g.Sum(x => x.TongTienHang - x.TienGiam), SoDon = g.Count() })
                .ToList();
            foreach (var r in rows) { doanhThu[r.Thang - 1] = r.DoanhThu; soDon[r.Thang - 1] = r.SoDon; }
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }

    // ================= VIEW MODELS =================
    public class ThongKeVM
    {
        public int Nam { get; set; }
        public int? Thang { get; set; }
        public decimal[] DoanhThuThang { get; set; }
        public decimal[] DoanhThuNamTruoc { get; set; }
        public int[] SoDonThang { get; set; }
        public decimal DoanhThuNam { get; set; }
        public decimal DoanhThuNamTruocTong { get; set; }
        public decimal? TangTruong { get; set; }
        public int SoDonHoanTat { get; set; }
        public decimal GiaTriTrungBinh { get; set; }
        public int DonChoXacNhan { get; set; }
        public int SoCanhBaoTon { get; set; }
        public List<TrangThaiDemVM> TrangThaiDon { get; set; }
        public List<TopSanPhamVM> TopBanChay { get; set; }
        public List<CanhBaoTonVM> CanhBaoTon { get; set; }
        public List<DonGanDayVM> DonGanDay { get; set; }
        public List<DonThangVM> DonTrongThang { get; set; }
    }
    public class TrangThaiDemVM { public string Ten { get; set; } public int SoLuong { get; set; } }
    public class TopSanPhamVM { public int SanPhamId { get; set; } public string Ten { get; set; } public int SoLuong { get; set; } public decimal DoanhThu { get; set; } }
    public class CanhBaoTonVM
    {
        public int BienTheId { get; set; }
        public string Ten { get; set; }
        public string DungLuong { get; set; }
        public string MauSac { get; set; }
        public int SoLuongTon { get; set; }
    }
    public class DonGanDayVM
    {
        public string MaDonHang { get; set; }
        public string TenNguoiNhan { get; set; }
        public decimal? Tong { get; set; }
        public string TrangThai { get; set; }
        public DateTime NgayDat { get; set; }
        public string TenTrangThai { get { return ThongKeController.NhanTrangThai(TrangThai); } }
        public string Mau { get { return ThongKeController.MauTrangThai(TrangThai); } }
    }
    public class DonThangVM
    {
        public string MaDonHang { get; set; }
        public string TenNguoiNhan { get; set; }
        public string SoDienThoai { get; set; }
        public decimal TongTienHang { get; set; }
        public decimal TienGiam { get; set; }
        public decimal? TongThanhToan { get; set; }
        public DateTime? NgayGiaoXong { get; set; }
    }
}