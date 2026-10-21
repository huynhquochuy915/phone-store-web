using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Controllers
{
    public class ThongKeController : Controller
    {
        // GET: ThongKe

        QL_DienThoaiEntities db = new QL_DienThoaiEntities();

        public ActionResult Index(int? nam, int? thang)
        {
            int namThongKe = nam ?? DateTime.Now.Year;

            ViewBag.Nam = namThongKe;
            ViewBag.ThangChon = null;
            ViewBag.TenThang = "";
            ViewBag.DanhSachDonHang = null;
            ViewBag.TongDoanhThuNam = 0m;
            ViewBag.TongThuNam = 0m;
            ViewBag.TongSoDonNam = 0;
            ViewBag.ThongBao = null;

            DataTable dt = new DataTable();
            var conn = db.Database.Connection;
            bool moKetNoi = conn.State != ConnectionState.Open;

            try
            {
                if (moKetNoi)
                    conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "sp_DoanhThuTheoThang";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add(new SqlParameter("@Nam", SqlDbType.Int)
                    {
                        Value = namThongKe
                    });

                    using (var reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            }
            finally
            {
                if (moKetNoi)
                    conn.Close();
            }

            decimal tongDoanhThuNam = 0m;
            decimal tongThuNam = 0m;
            int tongSoDonNam = 0;

            foreach (DataRow row in dt.Rows)
            {
                tongDoanhThuNam += Convert.ToDecimal(row["DoanhThu"]);
                tongThuNam += Convert.ToDecimal(row["TongThu"]);
                tongSoDonNam += Convert.ToInt32(row["SoDon"]);
            }

            ViewBag.DoanhThuTheoThang = dt;
            ViewBag.TongDoanhThuNam = tongDoanhThuNam;
            ViewBag.TongThuNam = tongThuNam;
            ViewBag.TongSoDonNam = tongSoDonNam;

            if (tongSoDonNam == 0)
            {
                ViewBag.ThongBao =
                    "Năm " + namThongKe + " chưa có dữ liệu doanh thu.";
            }

            if (thang.HasValue && thang.Value >= 1 && thang.Value <= 12)
            {
                ViewBag.ThangChon = thang.Value;
                ViewBag.TenThang = "Tháng " + thang.Value;

                DataTable dsDonHang = new DataTable();

                DateTime tuNgay = new DateTime(namThongKe, thang.Value, 1);
                DateTime denNgay = tuNgay.AddMonths(1);

                moKetNoi = conn.State != ConnectionState.Open;

                try
                {
                    if (moKetNoi)
                        conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                        SELECT Id, MaDonHang, TenNguoiNhan,
                               SoDienThoai, TongTienHang, TienGiam,
                               TongThanhToan, NgayGiaoXong
                        FROM DonHang
                        WHERE TrangThai = 'HoanTat'
                          AND NgayGiaoXong >= @TuNgay
                          AND NgayGiaoXong < @DenNgay
                        ORDER BY NgayGiaoXong DESC";

                        cmd.CommandType = CommandType.Text;

                        cmd.Parameters.Add(new SqlParameter("@TuNgay", SqlDbType.DateTime2)
                        {
                            Value = tuNgay
                        });

                        cmd.Parameters.Add(new SqlParameter("@DenNgay", SqlDbType.DateTime2)
                        {
                            Value = denNgay
                        });

                        using (var reader = cmd.ExecuteReader())
                        {
                            dsDonHang.Load(reader);
                        }
                    }
                }
                finally
                {
                    if (moKetNoi)
                        conn.Close();
                }

                ViewBag.DanhSachDonHang = dsDonHang;
            }

            return View();
        }

    }
}