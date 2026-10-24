using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace WebsiteQuanLiDienThoaiDiDong.Helpers
{
    public static class SocialLoginHelper
    {
        private static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : null;
        }

        private static bool IsTrue(string s) { return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase); }

        private static Dictionary<string, object> GetJson(string url, out string loi)
        {
            loi = null;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 8000;
                req.UserAgent = "PhoneStore/1.0";
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream()))
                    return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(sr.ReadToEnd());
            }
            catch (WebException ex)
            {
                loi = ex.Response != null ? "Mã đăng nhập không hợp lệ hoặc đã hết hạn." : "Không kết nối được máy chủ xác thực.";
                return null;
            }
            catch (Exception ex) { loi = "Lỗi xác thực: " + ex.Message; return null; }
        }

        public static bool VerifyGoogle(string idToken, out string email, out string name, out string loi)
        {
            email = null; name = null;
            string clientId = CauHinh.Get("Google_ClientId", "");
            if (clientId == "") { loi = "Chưa cấu hình Google_ClientId."; return false; }
            if (string.IsNullOrEmpty(idToken)) { loi = "Thiếu mã đăng nhập."; return false; }

            var d = GetJson("https://oauth2.googleapis.com/tokeninfo?id_token=" + Uri.EscapeDataString(idToken), out loi);
            if (d == null) return false;

            string iss = Str(d, "iss");
            if (Str(d, "aud") != clientId || (iss != "accounts.google.com" && iss != "https://accounts.google.com"))
            { loi = "Mã đăng nhập không dành cho ứng dụng này."; return false; }
            if (!IsTrue(Str(d, "email_verified"))) { loi = "Email Google chưa được xác minh."; return false; }

            email = Str(d, "email");
            name = Str(d, "name");
            if (string.IsNullOrEmpty(email)) { loi = "Google không trả về email."; return false; }
            loi = null;
            return true;
        }

        public static bool VerifyFacebook(string accessToken, out string email, out string name, out string loi)
        {
            email = null; name = null;
            string appId = CauHinh.Get("Facebook_AppId", "");
            string secret = CauHinh.Get("Facebook_AppSecret", "");
            if (appId == "" || secret == "") { loi = "Chưa cấu hình Facebook_AppId / Facebook_AppSecret."; return false; }
            if (string.IsNullOrEmpty(accessToken)) { loi = "Thiếu mã đăng nhập."; return false; }

            // kiểm tra token có thật sự cấp cho app của mình (chặn token của app khác)
            var dbg = GetJson("https://graph.facebook.com/debug_token?input_token=" + Uri.EscapeDataString(accessToken)
                + "&access_token=" + Uri.EscapeDataString(appId + "|" + secret), out loi);
            if (dbg == null) return false;
            var data = dbg.ContainsKey("data") ? dbg["data"] as Dictionary<string, object> : null;
            if (data == null || Str(data, "app_id") != appId || !IsTrue(Str(data, "is_valid")))
            { loi = "Mã đăng nhập Facebook không hợp lệ."; return false; }

            var me = GetJson("https://graph.facebook.com/me?fields=id,name,email&access_token=" + Uri.EscapeDataString(accessToken), out loi);
            if (me == null) return false;

            email = Str(me, "email");
            name = Str(me, "name");
            if (string.IsNullOrEmpty(email))
            { loi = "Tài khoản Facebook này không cung cấp email. Vui lòng đăng nhập bằng cách khác."; return false; }
            loi = null;
            return true;
        }
    }
}