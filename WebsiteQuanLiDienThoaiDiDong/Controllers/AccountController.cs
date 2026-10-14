using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    public class AccountController : Controller
    {
        // GET: Account
        private QL_DienThoaiEntities1 db = new QL_DienThoaiEntities1();

        //hàm hỗ trợ mã hóa mật khẩu
        private string ComputeSha256Hash(string input)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
        // Phần ĐĂNG KÝ
        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(NguoiDung model, string confirmPassword)
        {
            if (ModelState.IsValid)
            {
                var checkEmail = db.NguoiDungs.FirstOrDefault(x => x.Email == model.Email);
                if (checkEmail == null)
                {
                    ModelState.AddModelError(" Email", "Email đã tồn tại");
                    return View(model);
                }
                if (!string.IsNullOrEmpty(model.MatKhauHash))
                {
                    model.MatKhauHash = ComputeSha256Hash(model.MatKhauHash);
                }
                model.VaiTroId = 3;
                model.TrangThai = true;
                model.NgayTao = DateTime.Now;
                db.NguoiDungs.Add(model);
                db.SaveChanges();
                TempData["Success Message"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }
            return View(model);
        }
        // Phần ĐĂNG NHẬP
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string matKhau) 
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(matKhau))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ email và mật khẩu.");
                return View(); 
            }

            // Mã hóa mật khẩu người dùng nhập để so sánh
            string hashedPassword = ComputeSha256Hash(matKhau);

         
            var user = db.NguoiDungs.FirstOrDefault(u => u.Email == email && u.MatKhauHash == hashedPassword && u.TrangThai == true);

            if (user != null)
            {
                Session["UserId"] = user.Id;
                Session["HoTen"] = user.HoTen;
                Session["VaiTroId"] = user.VaiTroId;
                Session["Email"] = user.Email;

                if (user.VaiTroId == 1)
                {
                    return RedirectToAction("Index", "Admin");
                }
                else if (user.VaiTroId == 2)
                {
                    return RedirectToAction("Index", "Staff");
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                // Thêm một return ở đây cho trường hợp tài khoản/mật khẩu sai (user == null)
                ModelState.AddModelError("", "Email hoặc mật khẩu không chính xác, hoặc tài khoản đã bị khóa.");
                return View();
            }
        }
        // Phần ĐĂNG XUẤT
        public ActionResult Logout()
        {
            Session.Clear();
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "Account");
        }
        //Phần QUÊN MẬT KHẨU
        [HttpGet]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "Vui lòng nhập email của bạn.");
                return View();
            }

            var user = db.NguoiDungs.FirstOrDefault(u => u.Email == email && u.TrangThai == true);
            if (user != null)
            {
                // Ở đây sau này có thể tích hợp gửi mail cấp mật khẩu mới hoặc token đặt lại mật khẩu.
                TempData["SuccessMessage"] = "Yêu cầu đặt lại mật khẩu đã được gửi. Vui lòng kiểm tra email của bạn!";
                return RedirectToAction("Login");
            }
            else
            {
                ModelState.AddModelError("", "Email này không tồn tại trong hệ thống hoặc tài khoản đã bị khóa.");
                return View();
            }
        }
    }
}