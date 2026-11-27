using System;
using System.Linq;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Services;

namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    // Trợ lý tư vấn tự động (bot theo luật). Tách riêng khỏi ChatController (chat với nhân viên).
    public class TuVanController : Controller
    {
        // GET /TuVan/Hoi?cauHoi=iphone duoi 30 trieu
        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "none")]
        public JsonResult Hoi(string cauHoi)
        {
            ChatKetQua kq;
            try
            {
                kq = ChatTuVanService.TraLoi(cauHoi);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                return Json(new
                {
                    reply = "Xin lỗi, hệ thống tư vấn đang bận. Bạn thử lại sau ít phút nhé.",
                    products = new object[0],
                    suggestions = new string[0]
                }, JsonRequestBehavior.AllowGet);
            }

            var noImg = Url.Content("~/Content/images/no-image.png");

            // Nút "Xem tất cả" mở trang danh sách sản phẩm với đúng bộ lọc vừa tìm
            string xemTatCa = null;
            if (kq.SanPhams.Count > 0 && kq.TongKhop > kq.SanPhams.Count)
            {
                xemTatCa = Url.Action("Index", "SanPham", new
                {
                    area = "",
                    loaiId = kq.LoaiId,
                    thuongHieuId = kq.ThuongHieuId,
                    giaTu = kq.GiaTu,
                    giaDen = kq.GiaDen
                });
            }

            return Json(new
            {
                reply = kq.TraLoi,
                products = kq.SanPhams.Select(x => new
                {
                    id = x.Id,
                    name = x.Ten,
                    brand = x.ThuongHieu,
                    storage = x.BoNho,
                    image = string.IsNullOrEmpty(x.Anh) ? noImg : Url.Content("~" + x.Anh),
                    price = x.Gia,
                    oldPrice = x.GiaGoc,
                    outOfStock = x.HetHang,
                    stars = x.SoDanhGia > 0 ? x.Sao : (decimal?)null,
                    url = Url.Action("ChiTiet", "SanPham", new { area = "", id = x.Id })
                }),
                suggestions = kq.GoiY,
                total = kq.TongKhop,
                viewAll = xemTatCa,
                link = kq.LienKetUrl == null ? (object)null : new { text = kq.LienKetTen, url = kq.LienKetUrl }
            }, JsonRequestBehavior.AllowGet);
        }
    }
}
