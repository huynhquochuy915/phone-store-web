using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using WebsiteQuanLiDienThoaiDiDong.Models;
using System.Data.Entity;
using WebsiteQuanLiDienThoaiDiDong.Helpers;
namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    public class AccountController : Controller
    {
        // GET: Account
        private QL_DienThoaiEntities db = new QL_DienThoaiEntities();

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
        public ActionResult Register(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(NguoiDung model, string confirmPassword, string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (ModelState.IsValid)
            {
                model.HoTen = (model.HoTen ?? "").Trim();
                model.Email = (model.Email ?? "").Trim();
                model.SoDienThoai = String.IsNullOrWhiteSpace(model.SoDienThoai) ? null : model.SoDienThoai.Trim();

                if (String.IsNullOrWhiteSpace(model.HoTen) || String.IsNullOrWhiteSpace(model.Email) || String.IsNullOrEmpty(model.MatKhauHash))
                {
                    ModelState.AddModelError("", "Vui lòng nhập họ tên, email và mật khẩu.");
                    return View(model);
                }
                if (!String.Equals(model.MatKhauHash, confirmPassword, StringComparison.Ordinal))
                {
                    ModelState.AddModelError("confirmPassword", "Mật khẩu xác nhận không khớp.");
                    return View(model);
                }

                if (db.NguoiDungs.Any(x => x.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng.");
                    return View(model);
                }

                model.MatKhauHash = ComputeSha256Hash(model.MatKhauHash);
                model.VaiTroId = 3;
                model.TrangThai = true;
                model.NgayTao = DateTime.Now;
                db.NguoiDungs.Add(model);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login", new { returnUrl = returnUrl });
            }
            return View(model);
        }
        // Phần ĐĂNG NHẬP
        [HttpGet]
        public ActionResult Login(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string matKhau, string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(matKhau))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ email và mật khẩu.");
                return View(); 
            }

            // Mã hóa mật khẩu người dùng nhập để so sánh
            string hashedPassword = ComputeSha256Hash(matKhau);


            var taiKhoan = email.Trim();
            var user = db.NguoiDungs.Include("VaiTro").FirstOrDefault(u =>
                (u.Email == taiKhoan || u.SoDienThoai == taiKhoan) &&
                u.MatKhauHash == hashedPassword && u.TrangThai);

            if (user != null)
            {
                AuthHelper.SignIn(user, db);          // lưu UserId, HoTen, VaiTro và gộp giỏ hàng
                Session["VaiTroId"] = user.VaiTroId;  // giữ lại cho code cũ nếu có chỗ dùng
                Session["Email"] = user.Email;

                if (AuthHelper.IsStaff)
                    return Redirect("~/Admin");

                if (!String.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("Index", "Home");
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
            AuthHelper.SignOut();
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

