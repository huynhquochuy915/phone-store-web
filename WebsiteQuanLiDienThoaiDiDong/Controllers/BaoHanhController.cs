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
    public class BaoHanhController : Controller
    {
        QL_DienThoaiEntities db = new QL_DienThoaiEntities();

        // GET: BaoHanh

        public ActionResult TraCuu(string imei)
        {
            ViewBag.Imei = imei;

            if (string.IsNullOrWhiteSpace(imei))
                return View();

            imei = imei.Trim();

            if (imei.Length != 15 || !imei.All(char.IsDigit))
            {
                ViewBag.ThongBao = "IMEI phải gồm đúng 15 chữ số.";
                return View();
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
                    cmd.CommandText = "sp_TraCuuBaoHanh";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add(new SqlParameter("@Imei", SqlDbType.Char, 15)
                    {
                        Value = imei
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

            if (dt.Rows.Count == 0)
            {
                ViewBag.ThongBao = "Không tìm thấy thông tin bảo hành.";
                return View();
            }

            ViewBag.KetQua = dt.Rows[0];

            var anh = (from i in db.ImeiSanPhams
                       join ct in db.ChiTietDonHangs
                           on i.ChiTietDonHangId equals ct.Id
                       join bt in db.BienTheSanPhams
                           on ct.BienTheId equals bt.Id
                       join sp in db.SanPhams
                           on bt.SanPhamId equals sp.Id
                       where i.Imei == imei
                       select sp.AnhDaiDien).FirstOrDefault();

            ViewBag.AnhDaiDien = anh;

            return View();
        }
    }
}