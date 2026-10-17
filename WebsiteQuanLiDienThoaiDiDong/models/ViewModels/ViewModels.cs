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
    }
    public class TrangChuVM
    {
        public List<SanPhamCardVM> NoiBat { get; set; }
        public List<SanPhamCardVM> BanChay { get; set; }
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
        public int SoLuong { get; set; }
        public int Ton { get; set; }
        public decimal ThanhTien { get { return DonGia * SoLuong; } }
    }
    public class CartVM
    {
        public List<CartItemVM> Items { get; set; } = new List<CartItemVM>();
        public decimal TongTien { get { return Items.Sum(i => i.ThanhTien); } }
        public int TongSoLuong { get { return Items.Sum(i => i.SoLuong); } }
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