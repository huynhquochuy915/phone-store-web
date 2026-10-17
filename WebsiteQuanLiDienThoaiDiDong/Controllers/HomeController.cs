using System.Linq;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Models;
using WebsiteQuanLiDienThoaiDiDong.Models.ViewModels;

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
                    cot + "ORDER BY v.DaBan DESC, v.LuotXem DESC").ToList()
            };
            return View(vm);
        }

        // Menu 2 cấp: Loại sản phẩm -> Thương hiệu (lấy từ DB)
        [ChildActionOnly]
        public ActionResult Menu()
        {
            var loais = db.LoaiSanPhams.Where(l => l.HienThi).OrderBy(l => l.Id).ToList();
            var cap = db.SanPhams.Where(s => s.HienThi)
                        .Select(s => new { s.LoaiId, s.ThuongHieuId, s.ThuongHieu.TenThuongHieu })
                        .Distinct().ToList();
            var vm = loais.Select(l => new MenuLoaiVM
            {
                Id = l.Id,
                Ten = l.TenLoai,
                ThuongHieus = cap.Where(c => c.LoaiId == l.Id)
                                 .Select(c => new MenuThuongHieuVM { Id = c.ThuongHieuId, Ten = c.TenThuongHieu })
                                 .OrderBy(x => x.Ten).ToList()
            }).ToList();
            return PartialView(vm);
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}