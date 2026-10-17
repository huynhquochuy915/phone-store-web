using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    public class SellerController : Controller
    {
        private readonly QL_DienThoaiEntities csdl = new QL_DienThoaiEntities();
        private static readonly string[] DuoiAnhHopLe = { ".jpg", ".jpeg", ".png", ".webp" };

        // GET: Seller
        public ActionResult Index()
        {
            // tồn kho ít nhất xếp lên đầu
            var danhSachBienThe = csdl.BienTheSanPhams
                                      .Include(b => b.SanPham)
                                      .OrderBy(b => b.SoLuongTon)
                                      .ToList();
            return View(danhSachBienThe);
        }

        [HttpGet]
        public ActionResult Sua(int id)
        {
            var bienThe = csdl.BienTheSanPhams.Include(b => b.SanPham).FirstOrDefault(b => b.Id == id);
            if (bienThe == null) return HttpNotFound();
            return View(bienThe);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Sua(int id, decimal giaBan, int soLuongTon)
        {
            var bienThe = csdl.BienTheSanPhams.Find(id);
            if (bienThe == null) return HttpNotFound();

            // database có ràng buộc: 0 < GiaBan <= GiaGoc và SoLuongTon >= 0
            if (giaBan <= 0 || giaBan > bienThe.GiaGoc)
            {
                TempData["Loi"] = "Giá bán phải lớn hơn 0 và không vượt quá giá gốc (" + bienThe.GiaGoc.ToString("N0") + " đ).";
                return RedirectToAction("Sua", new { id });
            }
            if (soLuongTon < 0)
            {
                TempData["Loi"] = "Số lượng tồn không được âm.";
                return RedirectToAction("Sua", new { id });
            }

            // ghi lịch sử tồn kho để tổng lịch sử luôn khớp SoLuongTon
            int chenhLech = soLuongTon - bienThe.SoLuongTon;
            if (chenhLech != 0)
            {
                csdl.Set<LichSuTonKho>().Add(new LichSuTonKho
                {
                    BienTheId = id,
                    SoLuongThayDoi = chenhLech,
                    LyDo = "Người bán điều chỉnh tồn kho",
                    NgayTao = DateTime.Now
                });
            }

            bienThe.GiaBan = giaBan;
            bienThe.SoLuongTon = soLuongTon;
            csdl.SaveChanges();

            TempData["Msg"] = "Đã cập nhật sản phẩm.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Hinh(int id)
        {
            var sanPham = csdl.SanPhams.Include(s => s.HinhAnhSanPhams).FirstOrDefault(s => s.Id == id);
            if (sanPham == null) return HttpNotFound();
            return View(sanPham);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadHinh(int id, IEnumerable<HttpPostedFileBase> files)
        {
            if (!csdl.SanPhams.Any(s => s.Id == id)) return HttpNotFound();

            var thuMuc = Server.MapPath("~/Content/images/products/");
            int thuTu = csdl.HinhAnhSanPhams.Where(h => h.SanPhamId == id).Select(h => (int?)h.ThuTu).Max() ?? 0;
            // sản phẩm chưa có ảnh chính thì ảnh đầu tiên tải lên làm ảnh chính
            bool chuaCoAnhChinh = !csdl.HinhAnhSanPhams.Any(h => h.SanPhamId == id && h.LaAnhChinh);
            int soAnhLuu = 0, soAnhBoQua = 0;

            foreach (var file in files ?? new HttpPostedFileBase[0])
            {
                if (file == null || file.ContentLength == 0) continue;

                var duoi = Path.GetExtension(file.FileName).ToLower();
                if (!DuoiAnhHopLe.Contains(duoi) || file.ContentLength > 5 * 1024 * 1024)
                {
                    soAnhBoQua++;
                    continue;
                }

                var tenFile = "sp" + id + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + duoi;
                file.SaveAs(Path.Combine(thuMuc, tenFile));

                csdl.HinhAnhSanPhams.Add(new HinhAnhSanPham
                {
                    SanPhamId = id,
                    DuongDan = "/Content/images/products/" + tenFile,
                    LaAnhChinh = chuaCoAnhChinh,
                    ThuTu = ++thuTu
                });
                chuaCoAnhChinh = false;
                soAnhLuu++;
            }
            csdl.SaveChanges();

            if (soAnhLuu > 0) TempData["Msg"] = "Đã tải lên " + soAnhLuu + " hình.";
            if (soAnhBoQua > 0) TempData["Loi"] = soAnhBoQua + " file bị bỏ qua (chỉ nhận jpg, png, webp, tối đa 5MB).";
            return RedirectToAction("Hinh", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult XoaHinh(int id)
        {
            var hinh = csdl.HinhAnhSanPhams.Find(id);
            if (hinh == null) return HttpNotFound();

            int sanPhamId = hinh.SanPhamId;
            if (hinh.LaAnhChinh)
            {
                TempData["Loi"] = "Không xóa được ảnh chính.";
                return RedirectToAction("Hinh", new { id = sanPhamId });
            }

            csdl.HinhAnhSanPhams.Remove(hinh);   // chỉ xóa dòng trong database, giữ nguyên file ảnh
            csdl.SaveChanges();
            TempData["Msg"] = "Đã xóa hình.";
            return RedirectToAction("Hinh", new { id = sanPhamId });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) csdl.Dispose();
            base.Dispose(disposing);
        }
    }
}