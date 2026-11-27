using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace WebsiteQuanLiDienThoaiDiDong.Helpers
{
    public static class VnPayHelper
    {
        public static bool DaCauHinh
        {
            get
            {
                return !string.IsNullOrEmpty(CauHinh.Get("VnPay_TmnCode"))
                    && !string.IsNullOrEmpty(CauHinh.Get("VnPay_HashSecret"))
                    && !string.IsNullOrEmpty(CauHinh.Get("VnPay_Url"));
            }
        }

        // VNPay yêu cầu giờ Việt Nam (GMT+7)
        private static DateTime NowVn()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
        }

        private static string Enc(string s) { return WebUtility.UrlEncode(s ?? ""); }

        // key=value&key=value, sắp xếp theo tên, bỏ giá trị rỗng. Chuỗi này vừa là query vừa là dữ liệu để ký
        private static string GhepQuery(SortedDictionary<string, string> p)
        {
            return string.Join("&", p.Where(kv => !string.IsNullOrEmpty(kv.Value))
                                     .Select(kv => Enc(kv.Key) + "=" + Enc(kv.Value)));
        }

        private static string Hmac512(string key, string data)
        {
            using (var h = new HMACSHA512(Encoding.UTF8.GetBytes(key)))
            {
                var bytes = h.ComputeHash(Encoding.UTF8.GetBytes(data));
                return string.Concat(bytes.Select(b => b.ToString("x2")));
            }
        }

        // Mã tham chiếu gửi VNPay = mã đơn + "T" + giờphútgiây, để khách thanh toán lại không bị trùng
        public static string TaoTxnRef(string maDon) { return maDon + "T" + NowVn().ToString("HHmmss"); }
        public static string MaDonTuTxnRef(string txnRef) { return (txnRef ?? "").Split('T')[0]; }

        public static string TaoUrlThanhToan(string maDon, decimal soTien, string ip, string returnUrl, int conLaiGiay)
        {
            string secret = CauHinh.Get("VnPay_HashSecret");
            var now = NowVn();

            var p = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { "vnp_Version", "2.1.0" },
                { "vnp_Command", "pay" },
                { "vnp_TmnCode", CauHinh.Get("VnPay_TmnCode") },
                { "vnp_Amount", ((long)Math.Round(soTien) * 100).ToString() },   // VNPay yêu cầu nhân 100
                { "vnp_CreateDate", now.ToString("yyyyMMddHHmmss") },
                { "vnp_ExpireDate", now.AddSeconds(conLaiGiay).ToString("yyyyMMddHHmmss") },
                { "vnp_CurrCode", "VND" },
                { "vnp_IpAddr", ip },
                { "vnp_Locale", "vn" },
                { "vnp_OrderInfo", "Thanh toan don hang " + maDon },   // tiếng Việt không dấu, không ký tự đặc biệt
                { "vnp_OrderType", "other" },
                { "vnp_ReturnUrl", returnUrl },
                { "vnp_TxnRef", TaoTxnRef(maDon) }
            };

            string query = GhepQuery(p);
            return CauHinh.Get("VnPay_Url") + "?" + query + "&vnp_SecureHash=" + Hmac512(secret, query);
        }

        // Kiểm tra dữ liệu VNPay gửi về (Return hoặc IPN) có đúng chữ ký không
        public static bool KiemTraChuKy(NameValueCollection q)
        {
            var p = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string k in q.AllKeys)
            {
                if (k != null && k.StartsWith("vnp_") && k != "vnp_SecureHash" && k != "vnp_SecureHashType")
                    p[k] = q[k];
            }
            string tinh = Hmac512(CauHinh.Get("VnPay_HashSecret"), GhepQuery(p));
            return string.Equals(tinh, q["vnp_SecureHash"], StringComparison.OrdinalIgnoreCase);
        }
    }
}