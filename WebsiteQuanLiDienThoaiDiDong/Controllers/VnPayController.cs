using System;
using System.Collections.Specialized;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Helpers;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    public class VnPayController : Controller
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();

        private class KetQua
        {
            public string RspCode { get; set; }
            public string Message { get; set; }
            public bool ThanhCong { get; set; }   // đơn đã được ghi nhận thanh toán
        }

        // 1) Chuyển khách sang trang thanh toán VNPay
        [RequireLogin]
        public ActionResult Pay(int id)
        {
            int uid = AuthHelper.UserId.Value;
            var dh = db.DonHangs.FirstOrDefault(d => d.Id == id && d.NguoiDungId == uid);
            if (dh == null) return HttpNotFound();

            if (!VnPayHelper.DaCauHinh)
            {
                TempData["Err"] = "Chưa cấu hình VNPay (bảng CauHinhHeThong).";
                return RedirectToAction("ThanhCong", "DatHang", new { id });
            }
            if (dh.PhuongThucThanhToan != "QR" || dh.TrangThaiThanhToan != "ChuaThanhToan" || dh.TrangThai != "ChoXacNhan")
            {
                TempData["Err"] = "Đơn hàng này không còn ở trạng thái chờ thanh toán.";
                return RedirectToAction("ThanhCong", "DatHang", new { id });
            }

            // trang VNPay hết hạn cùng lúc với lúc hệ thống tự hủy đơn
            int phut = int.Parse(CauHinh.Get("QR_HetHan_Phut", "30"));
            int conLai = (int)(dh.NgayDat.AddMinutes(phut) - DateTime.Now).TotalSeconds;
            if (conLai < 120)
            {
                TempData["Err"] = "Đơn hàng sắp hết hạn thanh toán, vui lòng đặt lại đơn mới.";
                return RedirectToAction("ThanhCong", "DatHang", new { id });
            }

            decimal tong = dh.TongTienHang + dh.PhiShip - dh.TienGiam;
            string ip = Request.UserHostAddress;
            if (string.IsNullOrEmpty(ip) || ip == "::1") ip = "127.0.0.1";
            string returnUrl = Url.Action("Return", "VnPay", null, Request.Url.Scheme);

            return Redirect(VnPayHelper.TaoUrlThanhToan(dh.MaDonHang, tong, ip, returnUrl, conLai));
        }

        // 2) Trình duyệt khách được VNPay chuyển về đây sau khi thanh toán xong (hoặc hủy)
        public ActionResult Return()
        {
            KetQua kq;
            try { kq = XuLy(Request.QueryString); }
            catch { kq = new KetQua { RspCode = "99", Message = "Unknown error" }; }

            if (kq.RspCode == "97")
            {
                TempData["Err"] = "Dữ liệu trả về từ VNPay không hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            string ma = VnPayHelper.MaDonTuTxnRef(Request.QueryString["vnp_TxnRef"]);
            var dh = db.DonHangs.FirstOrDefault(d => d.MaDonHang == ma);
            if (dh == null) return HttpNotFound();

            if (kq.ThanhCong)
                TempData["Msg"] = "Thanh toán thành công qua VNPay.";
            else if (Request.QueryString["vnp_ResponseCode"] == "24")
                TempData["Err"] = "Bạn đã hủy thanh toán. Đơn hàng vẫn được giữ, bấm \"Thanh toán với VNPay\" để thanh toán lại trước khi hết hạn.";
            else
                TempData["Err"] = "Thanh toán chưa thành công. Bạn có thể bấm thanh toán lại.";
            return RedirectToAction("ThanhCong", "DatHang", new { id = dh.Id });
        }

        // 3) VNPay (server) báo kết quả. Không cần đăng nhập, bắt buộc trả JSON đúng mẫu
        public ContentResult Ipn()
        {
            KetQua kq;
            try { kq = XuLy(Request.QueryString); }
            catch { kq = new KetQua { RspCode = "99", Message = "Unknown error" }; }
            return Content("{\"RspCode\":\"" + kq.RspCode + "\",\"Message\":\"" + kq.Message + "\"}", "application/json");
        }

        // Dùng chung cho Return và IPN. Gọi nhiều lần vẫn an toàn (idempotent)
        private KetQua XuLy(NameValueCollection q)
        {
            if (!VnPayHelper.KiemTraChuKy(q))
                return new KetQua { RspCode = "97", Message = "Invalid signature" };

            string ma = VnPayHelper.MaDonTuTxnRef(q["vnp_TxnRef"]);
            var dh = db.DonHangs.FirstOrDefault(d => d.MaDonHang == ma);
            if (dh == null)
                return new KetQua { RspCode = "01", Message = "Order not found" };

            long vnpAmount;
            long.TryParse(q["vnp_Amount"], out vnpAmount);
            decimal tong = dh.TongTienHang + dh.PhiShip - dh.TienGiam;
            if (vnpAmount != (long)Math.Round(tong) * 100)
                return new KetQua { RspCode = "04", Message = "Invalid amount" };

            // đã xử lý rồi (IPN gọi lại, hoặc Return đến sau IPN) hoặc đơn đã bị hủy
            // Lưu ý: đơn đã hủy mà khách vẫn trả tiền thì admin phải hoàn tiền thủ công trong Merchant Admin
            if (dh.TrangThaiThanhToan != "ChuaThanhToan" || dh.TrangThai == "DaHuy")
                return new KetQua { RspCode = "02", Message = "Order already confirmed", ThanhCong = dh.TrangThaiThanhToan == "DaThanhToan" };

            bool thanhToanOk = q["vnp_ResponseCode"] == "00" && q["vnp_TransactionStatus"] == "00";
            if (!thanhToanOk)   // khách hủy / thẻ lỗi: giữ đơn chờ thanh toán, vẫn báo VNPay đã nhận
                return new KetQua { RspCode = "00", Message = "Confirm Success", ThanhCong = false };

            try
            {
                db.Database.ExecuteSqlCommand(
                    "EXEC sp_XacNhanThanhToan @DonHangId, @NguoiXacNhanId, @MaGiaoDich",
                    new SqlParameter("@DonHangId", dh.Id),
                    new SqlParameter("@NguoiXacNhanId", SqlDbType.Int) { Value = DBNull.Value },   // hệ thống tự xác nhận
                    new SqlParameter("@MaGiaoDich", q["vnp_TransactionNo"]));
            }
            catch (Exception ex)
            {
                var sql = ex as SqlException ?? ex.InnerException as SqlException;
                if (sql != null && sql.Number == 50031)   // lỡ có luồng khác xác nhận trước một nhịp
                    return new KetQua { RspCode = "02", Message = "Order already confirmed", ThanhCong = true };
                return new KetQua { RspCode = "99", Message = "Unknown error" };
            }
            return new KetQua { RspCode = "00", Message = "Confirm Success", ThanhCong = true };
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}