using System.Security.Cryptography;
using System.Text;

namespace WebsiteQuanLiDienThoaiDiDong.Helpers
{
    public static class SecurityHelper
    {
        public static string Sha256(string input)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
                var sb = new StringBuilder();
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}