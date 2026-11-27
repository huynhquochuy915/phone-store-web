using System;

namespace WebsiteQuanLiDienThoaiDiDong.Helpers
{
    public static class VietQrHelper
    {
        // Tạo link ảnh QR VietQR: đúng số tiền, nội dung chuyển khoản = mã đơn
        public static string BuildUrl(decimal soTien, string noiDung)
        {
            string bank = CauHinh.Get("NganHang_Ma", "");
            string acc = CauHinh.Get("NganHang_SoTaiKhoan", "");
            string name = CauHinh.Get("NganHang_TenChuTK", "");

            return "https://img.vietqr.io/image/" + bank + "-" + acc + "-compact2.png"
                 + "?amount=" + (long)Math.Round(soTien)
                 + "&addInfo=" + Uri.EscapeDataString(noiDung ?? "")
                 + "&accountName=" + Uri.EscapeDataString(name ?? "");
        }
    }
}