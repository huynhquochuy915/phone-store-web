using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Models.ViewModels;
using WebsiteQuanLiDienThoaiDiDong.Services;

namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    public class HomeController : Controller
    {
        private readonly QL_DienThoaiEntities db = new QL_DienThoaiEntities();

        public ActionResult Index()
        {
            // lấy thêm GiaGoc của biến thể rẻ nhất để tính % giảm
            const string cot = "SELECT TOP 10 v.*, (SELECT TOP 1 b.GiaGoc FROM BienTheSanPham b " +
                               "WHERE b.SanPhamId = v.Id AND b.HienThi = 1 ORDER BY b.GiaBan) AS GiaGoc " +
                               "FROM vw_SanPhamHienThi v ";
            var vm = new TrangChuVM
            {
                NoiBat = db.Database.SqlQuery<SanPhamCardVM>(
                    cot + "WHERE v.NoiBat = 1 ORDER BY v.DaBan DESC, v.LuotXem DESC").ToList(),
                BanChay = db.Database.SqlQuery<SanPhamCardVM>(
                    cot + "ORDER BY v.DaBan DESC, v.LuotXem DESC").ToList(),
                DienThoai = db.Database.SqlQuery<SanPhamCardVM>(
                     cot + "WHERE v.LoaiId = 1 ORDER BY v.DaBan DESC, v.LuotXem DESC").ToList(),
                PhuKien = db.Database.SqlQuery<SanPhamCardVM>(
                     cot + "WHERE v.LoaiId <> 1 ORDER BY v.DaBan DESC, v.LuotXem DESC").ToList(),
                Tin = NewsService.GetNews(4),
                Video = NewsService.GetVideos(3)
            };
            return View(vm);
        }

        // Menu 2 cấp: Loại sản phẩm -> Thương hiệu (lấy từ DB)
        [ChildActionOnly]
        public ActionResult Menu()
        {
            var loais = db.LoaiSanPhams.Where(x => x.HienThi).OrderBy(x => x.Id).ToList();
            var vm = new List<MenuLoaiVM>();
            foreach (var l in loais)
            {
                var hangs = db.SanPhams.Where(s => s.HienThi && s.LoaiId == l.Id)
                    .Select(s => s.ThuongHieu).Distinct().OrderBy(t => t.TenThuongHieu).ToList();
                vm.Add(new MenuLoaiVM
                {
                    Id = l.Id,
                    LoaiId = l.Id,
                    Ten = l.TenLoai,
                    ThuongHieus = hangs.Select(t => new MenuThuongHieuVM { Id = t.Id, Ten = t.TenThuongHieu }).ToList()
                });
            }
            return PartialView(vm);
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}