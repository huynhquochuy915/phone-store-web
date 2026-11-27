using System;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.SessionState;

namespace WebsiteQuanLiDienThoaiDiDong.Helpers
{
    [Serializable]
    public class OtpState { public string Hash; public DateTime Expires; public DateTime IssuedAt; public int Fails; }

    [Serializable]
    public class PendingRegister { public string HoTen, Email, SoDienThoai, MatKhauHash; }

    public static class OtpHelper
    {
        public const int HetHanPhut = 10;
        private const int ToiDaSai = 5;
        private const int ChoGuiLaiGiay = 60;

        private static HttpSessionState S { get { return HttpContext.Current.Session; } }
        private static string Key(string purpose) { return "otp_" + purpose; }

        private static string Hash(string purpose, string email, string code)
        {
            var key = Encoding.UTF8.GetBytes(CauHinh.Get("Token_BiMat", "doan-ltweb-2026"));
            using (var h = new HMACSHA256(key))
                return BitConverter.ToString(h.ComputeHash(
                    Encoding.UTF8.GetBytes(purpose + "|" + email.Trim().ToLower() + "|" + code))).Replace("-", "");
        }

        private static bool SafeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int d = 0;
            for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i];
            return d == 0;
        }

        public static void Clear(string purpose) { S.Remove(Key(purpose)); }

        private static bool Issue(string purpose, string email, out string code, out string loi)
        {
            code = null; loi = null;
            var old = S[Key(purpose)] as OtpState;
            if (old != null)
            {
                int con = ChoGuiLaiGiay - (int)(DateTime.Now - old.IssuedAt).TotalSeconds;
                if (con > 0) { loi = "Vui lòng đợi " + con + " giây rồi gửi lại mã."; return false; }
            }
            var b = new byte[4];
            using (var r = new RNGCryptoServiceProvider()) r.GetBytes(b);
            code = (BitConverter.ToUInt32(b, 0) % 1000000).ToString("D6");
            S[Key(purpose)] = new OtpState
            {
                Hash = Hash(purpose, email, code),
                IssuedAt = DateTime.Now,
                Expires = DateTime.Now.AddMinutes(HetHanPhut)
            };
            return true;
        }

        // tạo mã + gửi mail. Gửi lỗi thì xóa mã để người dùng thử lại ngay được
        public static bool Send(string purpose, string email, string hoTen, string noiDung, out string loi)
        {
            string code;
            if (!Issue(purpose, email, out code, out loi)) return false;

            string shop = CauHinh.Get("TenCuaHang", "PhoneStore");
            string body =
                "<p>Xin chào <b>" + HttpUtility.HtmlEncode(hoTen) + "</b>,</p>" +
                "<p>" + noiDung + "</p>" +
                "<p style=\"font-size:30px;font-weight:bold;letter-spacing:8px;color:#d70018;margin:16px 0\">" + code + "</p>" +
                "<p>Mã có hiệu lực " + HetHanPhut + " phút. Không chia sẻ mã này với bất kỳ ai.</p>";

            if (!EmailHelper.Send(email, "Mã xác thực " + code + " - " + shop, body, out loi))
            {
                Clear(purpose);
                return false;
            }
            return true;
        }

        // trả về null nếu đúng, ngược lại trả thông báo lỗi
        public static string Verify(string purpose, string email, string code)
        {
            var st = S[Key(purpose)] as OtpState;
            if (st == null) return "Mã đã hết hạn hoặc chưa được gửi. Vui lòng gửi lại mã.";
            if (DateTime.Now > st.Expires) { Clear(purpose); return "Mã đã hết hạn. Vui lòng gửi lại mã."; }
            if (st.Fails >= ToiDaSai) { Clear(purpose); return "Bạn nhập sai quá nhiều lần. Vui lòng gửi lại mã."; }

            code = (code ?? "").Trim();
            if (!SafeEquals(st.Hash, Hash(purpose, email, code)))
            {
                st.Fails++;
                S[Key(purpose)] = st;
                return "Mã không đúng. Bạn còn " + (ToiDaSai - st.Fails) + " lần thử.";
            }
            return null;
        }
    }
}