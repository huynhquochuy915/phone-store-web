using System;
using System.Collections.Generic;
using System.Linq;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Services
{
    public class HangTV
    {
        public string Ten { get; set; }
        public decimal Nguong { get; set; }        // tích lũy tối thiểu để đạt hạng
        public decimal PhanTramGiam { get; set; }  // % giảm trên tiền hàng
        public decimal GiamToiDa { get; set; }     // giảm tối đa mỗi đơn
        public decimal PhanTramShip { get; set; }  // % giảm phí vận chuyển (100 = miễn phí)

        // chữ hiển thị, sinh từ chính các con số bên trên nên không bao giờ lệch với thực tế
        public string[] Quyen
        {
            get
            {
                var ds = new List<string>();
                if (PhanTramGiam > 0) ds.Add("Giảm " + PhanTramGiam.ToString("0.##") + "% tiền hàng mỗi đơn (tối đa " + GiamToiDa.ToString("N0") + "đ)");
                if (PhanTramShip >= 100) ds.Add("Miễn phí vận chuyển");
                else if (PhanTramShip > 0) ds.Add("Giảm " + PhanTramShip.ToString("0.##") + "% phí vận chuyển");
                if (ds.Count == 0) ds.Add("Tích lũy từ các đơn đã giao thành công để lên hạng");
                return ds.ToArray();
            }
        }
    }

    public class UuDai
    {
        public decimal GiamHang { get; set; }
        public decimal GiamShip { get; set; }
        public decimal Tong { get { return GiamHang + GiamShip; } }
    }

    public class TienDon
    {
        public decimal TongTienHang { get; set; }
        public decimal PhiShip { get; set; }
        public decimal TienGiam { get; set; }
    }

    public static class UuDaiHang
    {
        // ===== CHỈNH Ở ĐÂY: ngưỡng và ưu đãi từng hạng (số liệu demo) =====
        public static readonly HangTV[] Hangs = new[]
        {
            new HangTV { Ten = "Thành viên", Nguong = 0,        PhanTramGiam = 0, GiamToiDa = 0,       PhanTramShip = 0 },
            new HangTV { Ten = "Bạc",        Nguong = 10000000, PhanTramGiam = 1, GiamToiDa = 300000,  PhanTramShip = 50 },
            new HangTV { Ten = "Vàng",       Nguong = 30000000, PhanTramGiam = 2, GiamToiDa = 700000,  PhanTramShip = 100 },
            new HangTV { Ten = "Kim cương",  Nguong = 60000000, PhanTramGiam = 3, GiamToiDa = 1500000, PhanTramShip = 100 }
        };

        public static decimal TichLuy(QL_DienThoaiEntities db, int uid)
        {
            return db.DonHangs.Where(d => d.NguoiDungId == uid && d.TrangThai == "HoanTat")
                .Select(d => (decimal?)(d.TongTienHang + d.PhiShip - d.TienGiam)).Sum() ?? 0m;
        }

        public static int ChiSo(decimal tichLuy)
        {
            int idx = 0;
            for (int i = 0; i < Hangs.Length; i++) if (tichLuy >= Hangs[i].Nguong) idx = i;
            return idx;
        }

        // một công thức duy nhất, dùng cho cả trang xem trước lẫn lúc tạo đơn nên hai con số luôn khớp
        public static UuDai Tinh(int idx, decimal tongHang, decimal phiShip, decimal daGiam)
        {
            var h = Hangs[Math.Max(0, Math.Min(idx, Hangs.Length - 1))];
            decimal gh = Math.Min(Math.Floor(tongHang * h.PhanTramGiam / 100m / 1000m) * 1000m, h.GiamToiDa);
            decimal gs = Math.Round(phiShip * h.PhanTramShip / 100m, 0);

            decimal conLai = Math.Max(0m, tongHang + phiShip - daGiam);   // không giảm quá số tiền còn phải trả
            if (gh + gs > conLai) { gs = Math.Min(gs, conLai); gh = Math.Min(gh, conLai - gs); }
            return new UuDai { GiamHang = gh, GiamShip = gs };
        }
    }
}