using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace WebsiteQuanLiDienThoaiDiDong.Services
{
    public class Tier
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string Hint { get; set; }
        public decimal Factor { get; set; }
        public Tier(string key, string label, string hint, decimal factor)
        { Key = key; Label = label; Hint = hint; Factor = factor; }
    }

    public class TierGroup
    {
        public string Field { get; set; }
        public string Title { get; set; }
        public Tier[] Tiers { get; set; }
    }

    // Luật định giá: giá ước tính = giá thu tối đa x hệ số 4 nhóm tình trạng, làm tròn xuống 10.000đ
    public static class ThuCuRules
    {
        public static readonly TierGroup[] Nhom = new[]
        {
            new TierGroup { Field = "NgoaiHinh", Title = "Ngoại hình máy", Tiers = new[] {
                new Tier("dep", "Như mới", "Không trầy xước, không móp méo", 1.00m),
                new Tier("tray_nhe", "Trầy xước nhẹ", "Vài vết xước nhỏ ở thân hoặc viền", 0.90m),
                new Tier("tray_nhieu", "Trầy xước nhiều", "Nhiều vết xước, cấn móp ở góc", 0.75m),
                new Tier("nut_vo", "Nứt vỡ, cong vênh", "Nứt mặt lưng, khung hoặc bị cong", 0.50m) } },
            new TierGroup { Field = "ManHinh", Title = "Màn hình", Tiers = new[] {
                new Tier("tot", "Hiển thị tốt", "Không ám, không điểm chết", 1.00m),
                new Tier("cham_diem", "Ám nhẹ, điểm chết nhỏ", "Nhìn kỹ mới thấy", 0.85m),
                new Tier("soc", "Sọc, loang, ám nặng", "Nhìn thấy rõ khi dùng", 0.60m),
                new Tier("nut", "Nứt kính, vỡ màn", "Màn hình bị vỡ", 0.35m) } },
            new TierGroup { Field = "Pin", Title = "Tình trạng pin", Tiers = new[] {
                new Tier("tren90", "Từ 90% trở lên", "Dung lượng pin còn tốt", 1.00m),
                new Tier("80_90", "Từ 80% đến 90%", "Dùng bình thường", 0.94m),
                new Tier("duoi80", "Dưới 80%", "Tụt pin nhanh, cần thay", 0.85m),
                new Tier("phong", "Pin phồng", "Pin bị phồng, mất an toàn", 0.70m) } },
            new TierGroup { Field = "ChucNang", Title = "Chức năng", Tiers = new[] {
                new Tier("tot", "Hoạt động tốt", "Mọi tính năng đều bình thường", 1.00m),
                new Tier("loi_nho", "Lỗi nhỏ", "Loa, mic, nút bấm hoặc sạc chập chờn", 0.88m),
                new Tier("loi_lon", "Lỗi lớn", "Mất Face ID/vân tay, hỏng camera, liệt cảm ứng", 0.65m),
                new Tier("khong_nguon", "Không lên nguồn", "Máy không khởi động được", 0.25m) } }
        };

        public static readonly string[] TrangThai = { "MoiGui", "DaLienHe", "DaDinhGia", "HoanTat", "Huy" };

        public static string TenTrangThai(string k)
        {
            switch (k)
            {
                case "MoiGui": return "Mới gửi";
                case "DaLienHe": return "Đã liên hệ";
                case "DaDinhGia": return "Đã định giá";
                case "HoanTat": return "Hoàn tất";
                case "Huy": return "Đã hủy";
                default: return k;
            }
        }

        public static string MauTrangThai(string k)
        {
            switch (k)
            {
                case "MoiGui": return "adm-badge-amber";
                case "DaLienHe":
                case "DaDinhGia": return "adm-badge-blue";
                case "HoanTat": return "adm-badge-green";
                default: return "adm-badge-red";
            }
        }

        public static string Nhan(string field, string key)
        {
            var g = Nhom.FirstOrDefault(x => x.Field == field);
            var t = g == null ? null : g.Tiers.FirstOrDefault(x => x.Key == key);
            return t == null ? key : t.Label;
        }

        // null nếu có lựa chọn không hợp lệ
        public static decimal? Tinh(decimal giaToiDa, string nh, string mh, string pin, string cn)
        {
            var pairs = new[] { Tuple.Create("NgoaiHinh", nh), Tuple.Create("ManHinh", mh), Tuple.Create("Pin", pin), Tuple.Create("ChucNang", cn) };
            decimal g = giaToiDa;
            foreach (var p in pairs)
            {
                var t = Nhom.First(x => x.Field == p.Item1).Tiers.FirstOrDefault(x => x.Key == p.Item2);
                if (t == null) return null;
                g *= t.Factor;
            }
            return Math.Floor(g / 10000m) * 10000m;
        }
    }

    public class MayThuCu
    {
        public int Id { get; set; }
        public string HangMay { get; set; }
        public string TenMay { get; set; }
        public int DungLuongGB { get; set; }
        public decimal GiaToiDa { get; set; }
        public string Nhan { get { return TenMay + " " + DungLuongGB + "GB"; } }
    }

    public class MayMoi
    {
        public int Id { get; set; }
        public string Ten { get; set; }
        public decimal Gia { get; set; }
    }

    // khớp từng cột của bảng YeuCauThuCu
    public class YeuCauThuCu
    {
        public int Id { get; set; }
        public string MaYeuCau { get; set; }
        public int? NguoiDungId { get; set; }
        public string HoTen { get; set; }
        public string SoDienThoai { get; set; }
        public string Email { get; set; }
        public int BangGiaId { get; set; }
        public string TenMay { get; set; }
        public string NgoaiHinh { get; set; }
        public string ManHinh { get; set; }
        public string Pin { get; set; }
        public string ChucNang { get; set; }
        public decimal GiaUocTinh { get; set; }
        public string MayMuonMua { get; set; }
        public string GhiChu { get; set; }
        public string TrangThai { get; set; }
        public decimal? GiaChot { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayCapNhat { get; set; }
    }

    public class TrangThaiDem
    {
        public string TrangThai { get; set; }
        public int SoLuong { get; set; }
    }

    public class ThongTinMayCu
    {
        public int SanPhamId { get; set; }
        public string TinhTrang { get; set; }
        public int? PinPhanTram { get; set; }
        public int BaoHanhThang { get; set; }
        public string GhiChu { get; set; }
    }

    public class ThuCuForm
    {
        public int? MayId { get; set; }
        public string NgoaiHinh { get; set; } = "dep";
        public string ManHinh { get; set; } = "tot";
        public string Pin { get; set; } = "tren90";
        public string ChucNang { get; set; } = "tot";
        public int? MayMoiId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(100)]
        public string HoTen { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại"),
         RegularExpression(@"^(0|\+84)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ")]
        public string SoDienThoai { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(150)]
        public string Email { get; set; }

        [StringLength(500)]
        public string GhiChu { get; set; }

        public string Get(string field)
        {
            switch (field)
            {
                case "NgoaiHinh": return NgoaiHinh;
                case "ManHinh": return ManHinh;
                case "Pin": return Pin;
                case "ChucNang": return ChucNang;
                default: return null;
            }
        }
    }

    public class ThuCuVM
    {
        public ThuCuForm Form { get; set; }
        public List<MayThuCu> May { get; set; }
        public List<MayMoi> MayMoi { get; set; }
    }
}