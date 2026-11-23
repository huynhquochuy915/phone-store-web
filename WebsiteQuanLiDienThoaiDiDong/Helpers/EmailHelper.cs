using System;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace WebsiteQuanLiDienThoaiDiDong.Helpers
{
    public static class EmailHelper
    {
        public static bool Send(string to, string subject, string htmlBody, out string loi)
        {
            loi = null;
            string user = CauHinh.Get("Smtp_Email", "");
            string pass = CauHinh.Get("Smtp_MatKhau", "").Replace(" ", "");
            if (user == "" || pass == "")
            {
                loi = "Chưa cấu hình Smtp_Email / Smtp_MatKhau trong bảng CauHinhHeThong.";
                return false;
            }
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var msg = new MailMessage())
                {
                    msg.From = new MailAddress(user, CauHinh.Get("TenCuaHang", "PhoneStore"), Encoding.UTF8);
                    msg.To.Add(to);
                    msg.Subject = subject;
                    msg.SubjectEncoding = Encoding.UTF8;
                    msg.Body = htmlBody;
                    msg.BodyEncoding = Encoding.UTF8;
                    msg.IsBodyHtml = true;
                    using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                    {
                        smtp.EnableSsl = true;
                        smtp.Credentials = new NetworkCredential(user, pass);
                        smtp.Timeout = 15000;
                        smtp.Send(msg);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                loi = ex.GetBaseException().Message;
                return false;
            }
        }
    }
}