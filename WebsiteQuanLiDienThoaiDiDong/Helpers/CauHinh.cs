using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Helpers
{
    // Đọc bảng CauHinhHeThong, cache lại (đổi cấu hình thì khởi động lại app)
    public static class CauHinh
    {
        private static Dictionary<string, string> _cache;
        private static readonly object _lock = new object();

        public static string Get(string khoa, string macDinh = "")
        {
            if (_cache == null)
            {
                lock (_lock)
                {
                    if (_cache == null)
                        using (var db = new QL_DienThoaiEntities())
                            _cache = db.CauHinhHeThongs.ToDictionary(x => x.Khoa, x => x.GiaTri);
                }
            }
            string v;
            return _cache.TryGetValue(khoa, out v) ? v : macDinh;
        }
    }

    public static class FormatExt
    {
        private static readonly CultureInfo vi = new CultureInfo("vi-VN");
        public static string VND(this decimal v) { return v.ToString("N0", vi) + "₫"; }
        public static string VND(this decimal? v) { return v.HasValue ? v.Value.VND() : "Liên hệ"; }
    }

    public static class VietQrHelper
    {
        // Ảnh QR động của VietQR: số tiền + nội dung chuyển khoản = mã đơn
        public static string BuildUrl(decimal soTien, string noiDung)
        {
            return string.Format(
                "https://img.vietqr.io/image/{0}-{1}-compact2.png?amount={2}&addInfo={3}&accountName={4}",
                CauHinh.Get("NganHang_Ma"), CauHinh.Get("NganHang_SoTaiKhoan"), (long)soTien,
                Uri.EscapeDataString(noiDung), Uri.EscapeDataString(CauHinh.Get("NganHang_TenChuTK")));
        }
    }
}