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

                Session["pendingReg"] = new PendingRegister
                {
                    HoTen = model.HoTen,
                    Email = model.Email,
                    SoDienThoai = model.SoDienThoai,
                    MatKhauHash = ComputeSha256Hash(model.MatKhauHash)
                };
                Session["pendingRegReturn"] = returnUrl;

                string loi;
                if (!OtpHelper.Send("register", model.Email, model.HoTen,
                        "Dùng mã dưới đây để hoàn tất đăng ký tài khoản:", out loi))
                {
                    ModelState.AddModelError("", "Không gửi được email xác thực: " + loi);
                    return View(model);
                }
                return RedirectToAction("VerifyEmail");
            }
            return View(model);
        }
        [HttpGet]
        public ActionResult VerifyEmail()
        {
            var p = Session["pendingReg"] as PendingRegister;
            if (p == null) return RedirectToAction("Register");
            ViewBag.Email = p.Email;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyEmail(string code)
        {
            var p = Session["pendingReg"] as PendingRegister;
            if (p == null) return RedirectToAction("Register");
            ViewBag.Email = p.Email;

            string loi = OtpHelper.Verify("register", p.Email, code);
            if (loi != null) { ModelState.AddModelError("", loi); return View(); }

            if (db.NguoiDungs.Any(x => x.Email == p.Email))
            {
                Session.Remove("pendingReg");
                TempData["Err"] = "Email này đã được sử dụng.";
                return RedirectToAction("Register");
            }

            db.NguoiDungs.Add(new NguoiDung
            {
                HoTen = p.HoTen,
                Email = p.Email,
                SoDienThoai = p.SoDienThoai,
                MatKhauHash = p.MatKhauHash,
                VaiTroId = 3,
                TrangThai = true,
                NgayTao = DateTime.Now
            });
            db.SaveChanges();

            string returnUrl = Session["pendingRegReturn"] as string;
            Session.Remove("pendingReg");
            Session.Remove("pendingRegReturn");
            OtpHelper.Clear("register");

            TempData["SuccessMessage"] = "Xác thực thành công! Vui lòng đăng nhập.";
            return RedirectToAction("Login", new { returnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResendRegisterCode()
        {
            var p = Session["pendingReg"] as PendingRegister;
            if (p == null) return RedirectToAction("Register");

            string loi;
            if (OtpHelper.Send("register", p.Email, p.HoTen, "Dùng mã dưới đây để hoàn tất đăng ký tài khoản:", out loi))
                TempData["Msg"] = "Đã gửi lại mã xác thực.";
            else
                TempData["Err"] = loi;
            return RedirectToAction("VerifyEmail");
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
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("", "Vui lòng nhập email của bạn.");
                return View();
            }

            email = email.Trim();
            Session["resetEmail"] = email;
            Session.Remove("resetVerifiedEmail");
            Session.Remove("resetVerifiedAt");

            var user = db.NguoiDungs.FirstOrDefault(u => u.Email == email && u.TrangThai);
            if (user != null)
            {
                string loi;
                if (!OtpHelper.Send("reset", email, user.HoTen, "Dùng mã dưới đây để đặt lại mật khẩu:", out loi))
                {
                    ModelState.AddModelError("", "Không gửi được email: " + loi);
                    return View();
                }
            }
            // luôn sang bước nhập mã, không để lộ email nào có trong hệ thống
            return RedirectToAction("VerifyResetCode");
        }

        // ===== BƯỚC 2: nhập mã OTP =====
        [HttpGet]
        public ActionResult VerifyResetCode()
        {
            var email = Session["resetEmail"] as string;
            if (string.IsNullOrEmpty(email)) return RedirectToAction("ForgotPassword");
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyResetCode(string code)
        {
            var email = Session["resetEmail"] as string;
            if (string.IsNullOrEmpty(email)) return RedirectToAction("ForgotPassword");
            ViewBag.Email = email;

            string loi = OtpHelper.Verify("reset", email, code);
            if (loi != null) { ModelState.AddModelError("", loi); return View(); }

            // OTP đúng: xóa mã, đánh dấu đã xác thực (có hiệu lực 10 phút)
            OtpHelper.Clear("reset");
            Session["resetVerifiedEmail"] = email;
            Session["resetVerifiedAt"] = DateTime.Now;
            return RedirectToAction("ResetPassword");
        }

        // ===== BƯỚC 3: nhập mật khẩu mới (chỉ vào được khi đã qua bước 2) =====
        private bool DaXacThucOtpReset(out string email)
        {
            email = Session["resetEmail"] as string;
            var daXacThuc = Session["resetVerifiedEmail"] as string;
            var luc = Session["resetVerifiedAt"] as DateTime?;
            return !string.IsNullOrEmpty(email)
                && email == daXacThuc
                && luc.HasValue
                && (DateTime.Now - luc.Value).TotalMinutes <= 10;
        }

        private ActionResult VeBuocResetTruoc()
        {
            return RedirectToAction(string.IsNullOrEmpty(Session["resetEmail"] as string)
                ? "ForgotPassword" : "VerifyResetCode");
        }

        [HttpGet]
        public ActionResult ResetPassword()
        {
            string email;
            if (!DaXacThucOtpReset(out email)) return VeBuocResetTruoc();
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(string matKhauMoi, string xacNhan)
        {
            string email;
            if (!DaXacThucOtpReset(out email)) return VeBuocResetTruoc();
            ViewBag.Email = email;

            if (string.IsNullOrEmpty(matKhauMoi) || matKhauMoi.Length < 6)
            { ModelState.AddModelError("", "Mật khẩu phải có ít nhất 6 ký tự."); return View(); }
            if (matKhauMoi != xacNhan)
            { ModelState.AddModelError("", "Mật khẩu xác nhận không khớp."); return View(); }

            var user = db.NguoiDungs.FirstOrDefault(u => u.Email == email && u.TrangThai);
            if (user == null) { ModelState.AddModelError("", "Tài khoản không tồn tại."); return View(); }

            user.MatKhauHash = ComputeSha256Hash(matKhauMoi);
            db.SaveChanges();

            Session.Remove("resetEmail");
            Session.Remove("resetVerifiedEmail");
            Session.Remove("resetVerifiedAt");

            TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResendResetCode()
        {
            var email = Session["resetEmail"] as string;
            if (string.IsNullOrEmpty(email)) return RedirectToAction("ForgotPassword");

            var user = db.NguoiDungs.FirstOrDefault(u => u.Email == email && u.TrangThai);
            if (user != null)
            {
                string loi;
                if (!OtpHelper.Send("reset", email, user.HoTen, "Dùng mã dưới đây để đặt lại mật khẩu:", out loi))
                { TempData["Err"] = loi; return RedirectToAction("VerifyResetCode"); }
            }
            TempData["Msg"] = "Đã gửi lại mã xác thực.";
            return RedirectToAction("VerifyResetCode");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult SocialLogin(string provider, string token, string returnUrl)
        {
            string email = null, name = null, loi = "Cách đăng nhập không hợp lệ.";
            bool ok = false;
            if (provider == "google") ok = SocialLoginHelper.VerifyGoogle(token, out email, out name, out loi);
            else if (provider == "facebook") ok = SocialLoginHelper.VerifyFacebook(token, out email, out name, out loi);
            if (!ok) return Json(new { ok = false, message = loi });

            email = email.Trim().ToLower();
            var user = db.NguoiDungs.Include("VaiTro").FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                string hoTen = string.IsNullOrWhiteSpace(name) ? email : name.Trim();
                if (hoTen.Length > 100) hoTen = hoTen.Substring(0, 100);
                var moi = new NguoiDung
                {
                    HoTen = hoTen,
                    Email = email,
                    SoDienThoai = null,
                    MatKhauHash = ComputeSha256Hash(Guid.NewGuid().ToString("N")),   // mật khẩu ngẫu nhiên, khách đăng nhập bằng mạng xã hội
                    VaiTroId = 3,
                    TrangThai = true,
                    NgayTao = DateTime.Now
                };
                db.NguoiDungs.Add(moi);
                db.SaveChanges();
                user = db.NguoiDungs.Include("VaiTro").First(u => u.Id == moi.Id);
            }
            else if (!user.TrangThai)
            {
                return Json(new { ok = false, message = "Tài khoản đã bị khóa." });
            }

            AuthHelper.SignIn(user, db);
            Session["VaiTroId"] = user.VaiTroId;
            Session["Email"] = user.Email;

            string url;
            if (AuthHelper.IsStaff) url = Url.Content("~/Admin");
            else if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) url = returnUrl;
            else url = Url.Action("Index", "Home");
            return Json(new { ok = true, url = url });
        }
    }
}

