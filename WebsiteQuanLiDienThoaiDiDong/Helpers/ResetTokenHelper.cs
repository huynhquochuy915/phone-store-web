using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Helpers
{
    public static class ResetTokenHelper
    {
        private static string Sign(string data)
        {
            var key = Encoding.UTF8.GetBytes(CauHinh.Get("Token_BiMat", "doan-ltweb-2026"));
            using (var h = new HMACSHA256(key))
                return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(data))).Replace("-", "").ToLower();
        }

        private static string B64(string s)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string UnB64(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
            return Encoding.UTF8.GetString(Convert.FromBase64String(s));
        }

        private static bool SafeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        // token = payload(id.hạn).chữ ký(gắn với mật khẩu hiện tại => đổi mật khẩu xong là hết hiệu lực)
        public static string Create(NguoiDung u, int phut = 30)
        {
            string payload = u.Id + "." + DateTime.UtcNow.AddMinutes(phut).Ticks;
            return B64(payload) + "." + Sign(payload + "|" + u.MatKhauHash);
        }

        public static bool TryValidate(string token, QL_DienThoaiEntities db, out NguoiDung user)
        {
            user = null;
            if (string.IsNullOrEmpty(token)) return false;
            var parts = token.Split('.');
            if (parts.Length != 2) return false;

            string payload;
            try { payload = UnB64(parts[0]); } catch { return false; }

            var p = payload.Split('.');
            int id; long ticks;
            if (p.Length != 2 || !int.TryParse(p[0], out id) || !long.TryParse(p[1], out ticks)) return false;
            if (ticks < DateTime.UtcNow.Ticks) return false;

            var u = db.NguoiDungs.FirstOrDefault(x => x.Id == id && x.TrangThai);
            if (u == null) return false;
            if (!SafeEquals(parts[1], Sign(payload + "|" + u.MatKhauHash))) return false;

            user = u;
            return true;
        }
    }
}