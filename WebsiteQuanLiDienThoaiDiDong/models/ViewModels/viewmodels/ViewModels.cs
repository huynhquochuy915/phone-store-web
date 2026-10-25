using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace WebsiteQuanLiDienThoaiDiDong.Models.ViewModels
{
    public class MenuThuongHieuVM { public int Id { get; set; } public string Ten { get; set; } }
    public class MenuLoaiVM
    {
        public int LoaiId { get; set; }          
        public decimal? GiaGoc { get; set; }    
        public int Id { get; set; }
        public string Ten { get; set; }
        public List<MenuThuongHieuVM> ThuongHieus { get; set; }
    }

    // map từ view vw_SanPhamHienThi
    public class SanPhamCardVM
    {
        public int Id { get; set; }
        public int LoaiId { get; set; }              
        public string TenSanPham { get; set; }
        public string TenThuongHieu { get; set; }
        public string AnhDaiDien { get; set; }
        public decimal? GiaThapNhat { get; set; }
        public decimal? GiaCaoNhat { get; set; }
        public decimal? GiaGoc { get; set; }        
        public int? TongTon { get; set; }
        public decimal SaoTrungBinh { get; set; }
        public int SoDanhGia { get; set; }
        public int DaBan { get; set; }
        public bool DaYeuThich { get; set; }
    }
    public class TrangChuVM
    {
        public List<SanPhamCardVM> MayCu { get; set; } = new List<SanPhamCardVM>();
        public List<WebsiteQuanLiDienThoaiDiDong.Services.NewsItem> Tin { get; set; } = new List<WebsiteQuanLiDienThoaiDiDong.Services.NewsItem>();
        public List<WebsiteQuanLiDienThoaiDiDong.Services.VideoItem> Video { get; set; } = new List<WebsiteQuanLiDienThoaiDiDong.Services.VideoItem>();
        public List<SanPhamCardVM> NoiBat { get; set; }
        public List<SanPhamCardVM> BanChay { get; set; }
        public List<SanPhamCardVM> DienThoai { get; set; } = new List<SanPhamCardVM>();
        public List<SanPhamCardVM> PhuKien { get; set; } = new List<SanPhamCardVM>();
    }

    public class DanhSachSanPhamVM
    {
        public List<SanPhamCardVM> SanPhams { get; set; } = new List<SanPhamCardVM>();
        public List<LoaiSanPham> Loais { get; set; } = new List<LoaiSanPham>();
        public List<ThuongHieu> ThuongHieus { get; set; } = new List<ThuongHieu>();
        public string TuKhoa { get; set; }
        public int? LoaiId { get; set; }
        public int? ThuongHieuId { get; set; }
        public decimal? GiaTu { get; set; }
        public decimal? GiaDen { get; set; }
        public byte? RamGB { get; set; }
        public int? DungLuongGB { get; set; }
        public bool ConHang { get; set; }
        public string SapXep { get; set; }
        public int Trang { get; set; }
        public int SoTrang { get; set; }
    }

    public class SanPhamChiTietVM
    {
        public SanPham SanPham { get; set; }
        public List<HinhAnhSanPham> HinhAnhs { get; set; } = new List<HinhAnhSanPham>();
        public List<DanhGia> DanhGias { get; set; } = new List<DanhGia>();
        public bool CoTheDanhGia { get; set; }
        public bool DaDanhGia { get; set; }
        public List<byte> KyHanTraGop { get; set; } = new List<byte>();
        public List<SanPhamCardVM> SanPhamLienQuan { get; set; } = new List<SanPhamCardVM>();
    }

    public class DanhGiaVM
    {
        public int SanPhamId { get; set; }
        [Required(ErrorMessage = "Vui lòng chọn số sao")]
        [Range(1, 5, ErrorMessage = "Số sao từ 1 đến 5")]
        public byte SoSao { get; set; }
        [StringLength(1000, ErrorMessage = "Nội dung tối đa 1000 ký tự")]
        public string NoiDung { get; set; }
    }

    public class SoSanhVM
    {
        public List<SanPham> YeuThichs { get; set; } = new List<SanPham>();  // điện thoại trong yêu thích
        public List<int> DaChon { get; set; } = new List<int>();
        public List<SanPham> SanPhams { get; set; } = new List<SanPham>();   // các máy được chọn để so
    }

    public class CartItemVM
    {
        public int LineId { get; set; }
        public int BienTheId { get; set; }
        public int SanPhamId { get; set; }
        public string Ten { get; set; }
        public string MauSac { get; set; }
        public string DungLuong { get; set; }
        public string Anh { get; set; }
        public decimal DonGia { get; set; }
        
        public decimal GiaGoc { get; set; }                                   // THÊM
        public decimal TietKiem { get { return GiaGoc > DonGia ? (GiaGoc - DonGia) * SoLuong : 0; } }   // THÊM
        public int SoLuong { get; set; }
        public int Ton { get; set; }
        public decimal ThanhTien { get { return DonGia * SoLuong; } }
    }
    public class CartVM
    {
        public List<CartItemVM> Items { get; set; } = new List<CartItemVM>();
        public decimal TongTien { get { return Items.Sum(i => i.ThanhTien); } }
        public int TongSoLuong { get { return Items.Sum(i => i.SoLuong); } }
        public decimal TongGiaGoc { get { return Items.Sum(i => i.GiaGoc > i.DonGia ? i.GiaGoc * i.SoLuong : i.ThanhTien); } }   // THÊM
        public decimal TongGiamGia { get { return Items.Sum(i => i.TietKiem); } }                                              // THÊM
        public bool CoLoi { get { return Items.Any(i => i.SoLuong > i.Ton); } }
    }

    public class CheckoutVM
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên người nhận"), StringLength(100)]
        public string TenNguoiNhan { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại"),
         RegularExpression(@"^(0|\+84)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ")]
        public string SoDienThoai { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(150)]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn tỉnh/thành"), StringLength(50)]
        public string TinhThanh { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng"), StringLength(300)]
        public string DiaChiGiao { get; set; }

        [StringLength(300)] public string GhiChu { get; set; }
        [StringLength(30)] public string MaKhuyenMai { get; set; }
        public string PhuongThucThanhToan { get; set; } = "COD";

        // dữ liệu hiển thị (không bind từ form)
        public CartVM Cart { get; set; }
        public List<string> DanhSachTinh { get; set; }
        public decimal PhiShip { get; set; }
        public decimal TienGiam { get; set; }
        public decimal TongThanhToan { get { return (Cart == null ? 0 : Cart.TongTien) + PhiShip - TienGiam; } }
    }

    public class KetQuaDatHang
    {
        public int DonHangId { get; set; }
        public string MaDonHang { get; set; }
        public decimal TongThanhToan { get; set; }
    }
}
