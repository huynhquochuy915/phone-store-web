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
    public class DonHangController : Controller
    {
        // GET: DonHang

        QL_DienThoaiEntities db = new QL_DienThoaiEntities();

        public ActionResult Index(string trangThai,DateTime? tuNgay,DateTime? denNgay)
        {
            // Lấy ID người dùng đang đăng nhập
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int nguoiDungId = Convert.ToInt32(Session["UserId"]);

            if (tuNgay.HasValue && denNgay.HasValue
                && tuNgay.Value.Date > denNgay.Value.Date)
            {
                ViewBag.ThongBao = "Ngày bắt đầu không được lớn hơn ngày kết thúc.";
                tuNgay = null;
                denNgay = null;
            }

            if (tuNgay.HasValue && !denNgay.HasValue)
            {
                denNgay = tuNgay.Value.Date;
            }

            if (!tuNgay.HasValue && denNgay.HasValue)
            {
                tuNgay = denNgay.Value.Date;
            }

            DataTable dt = new DataTable();

            var conn = db.Database.Connection;
            bool moKetNoi = conn.State != ConnectionState.Open;

            try
            {
                if (moKetNoi)
                    conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                  

                    string sql = @"
                    SELECT
                        d.Id,
                        d.MaDonHang,
                        d.NgayDat,
                        d.TrangThai,
                        d.TongTienHang,
                        d.TienGiam,
                        d.TongThanhToan,
                        d.PhuongThucThanhToan,

                        (
                            SELECT TOP 1 ct.TenSanPham
                            FROM ChiTietDonHang ct
                            WHERE ct.DonHangId = d.Id
                            ORDER BY ct.Id
                        ) AS TenSanPham,

                        (
                            SELECT TOP 1 sp.AnhDaiDien
                            FROM ChiTietDonHang ct
                            JOIN BienTheSanPham bt ON ct.BienTheId = bt.Id
                            JOIN SanPham sp ON bt.SanPhamId = sp.Id
                            WHERE ct.DonHangId = d.Id
                            ORDER BY ct.Id
                        ) AS AnhDaiDien,

                        (
                            SELECT SUM(ct.SoLuong)
                            FROM ChiTietDonHang ct
                            WHERE ct.DonHangId = d.Id
                        ) AS TongSoLuong

                    FROM DonHang d
                    WHERE d.NguoiDungId = @NguoiDungId";

                    if (!string.IsNullOrWhiteSpace(trangThai))
                    {
                        if (trangThai == "DangTiepNhan")
                        {
                            sql += " AND d.TrangThai IN ('ChoXacNhan', 'DaXacNhan')";
                        }
                        else if (trangThai == "DangGiao")
                        {
                            sql += " AND d.TrangThai = 'DangGiao'";
                        }
                        else if (trangThai == "HoanTat")
                        {
                            sql += " AND d.TrangThai = 'HoanTat'";
                        }
                        else if (trangThai == "DaHuy")
                        {
                            sql += " AND d.TrangThai = 'DaHuy'";
                        }
                    }

                    if (tuNgay.HasValue)
                    {
                        sql += " AND d.NgayDat >= @TuNgay";
                    }

                    if (denNgay.HasValue)
                    {
                        sql += " AND d.NgayDat < @DenNgay";
                    }

                    sql += " ORDER BY d.NgayDat DESC";

                    cmd.CommandText = sql;
                    cmd.CommandType = CommandType.Text;

                    cmd.Parameters.Add(new SqlParameter("@NguoiDungId", SqlDbType.Int)
                    {
                        Value = nguoiDungId
                    });

                    if (tuNgay.HasValue)
                    {
                        cmd.Parameters.Add(new SqlParameter("@TuNgay", SqlDbType.DateTime2)
                        {
                            Value = tuNgay.Value.Date
                        });
                    }

                    if (denNgay.HasValue)
                    {
                        cmd.Parameters.Add(new SqlParameter("@DenNgay", SqlDbType.DateTime2)
                        {
                            Value = denNgay.Value.Date.AddDays(1)
                        });
                    }

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

            ViewBag.DanhSachDonHang = dt;
            ViewBag.TrangThai = trangThai ?? "";
            ViewBag.TuNgay = tuNgay?.ToString("dd/MM/yyyy");
            ViewBag.DenNgay = denNgay?.ToString("dd/MM/yyyy");

            return View();
        }
    }
}