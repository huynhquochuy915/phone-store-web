using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WebsiteQuanLiDienThoaiDiDong.Helpers;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Services
{
    public class ChatSanPham
    {
        public int Id { get; set; }
        public string Ten { get; set; }
        public string ThuongHieu { get; set; }
        public string Anh { get; set; }
        public decimal Gia { get; set; }
        public decimal? GiaGoc { get; set; }
        public string BoNho { get; set; }
        public bool HetHang { get; set; }
        public decimal Sao { get; set; }
        public int SoDanhGia { get; set; }
    }

    public class ChatKetQua
    {
        public string TraLoi { get; set; }
        public List<ChatSanPham> SanPhams { get; set; } = new List<ChatSanPham>();
        public List<string> GoiY { get; set; } = new List<string>();
        public int TongKhop { get; set; }
        public int? LoaiId { get; set; }
        public int? ThuongHieuId { get; set; }      // chỉ có khi người dùng hỏi đúng 1 hãng
        public decimal? GiaTu { get; set; }
        public decimal? GiaDen { get; set; }
        public string LienKetTen { get; set; }      // nút liên kết cho câu hỏi chung (bảo hành, khuyến mãi...)
        public string LienKetUrl { get; set; }
    }

    // Tư vấn theo luật: hiểu câu tiếng Việt (có dấu hoặc không dấu), tách ra giá / hãng / RAM / bộ nhớ / tên máy,
    // rồi lọc trong database. Không cần gọi API ngoài nên miễn phí và chạy offline.
    public static class ChatTuVanService
    {
        private const int SoKetQuaToiDa = 5;
        private const RegexOptions RO = RegexOptions.CultureInvariant;
        private static readonly CultureInfo Vi = new CultureInfo("vi-VN");

        // ===== Regex nhận diện (văn bản đã bỏ dấu + chữ thường) =====
        private static readonly Regex ReKhoangTrang = new Regex(@"\s+", RO);
        private static readonly Regex ReSoNgan = new Regex(@"(?<![\d.,])(?<n>\d{1,3}(?:[.,]\d{3})+)(?:\s*(?:vnd|dong|d))?(?![a-z0-9])", RO);
        private static readonly Regex ReKhoang = new Regex(@"\b(?<a>\d+(?:[.,]\d+)?)\s*(?<ua>trieu|tr|cu|nghin|ngan|k)?\s*(?:-|~|den|toi)\s*(?<b>\d+(?:[.,]\d+)?)\s*(?<ub>trieu|tr|cu|nghin|ngan|k|vnd|dong)?(?![a-z0-9])", RO);
        private static readonly Regex ReToiDa = new Regex(@"(?:\b(?:duoi|toi da|khong qua|khong vuot qua|nho hon|re hon|thap hon|ngan sach|budget)|<=|<)\s*(?<so>\d+(?:[.,]\d+)?)\s*(?<dv>(?:trieu|tr|cu|nghin|ngan|k|vnd|dong)(?![a-z0-9]))?", RO);
        private static readonly Regex ReToiThieu = new Regex(@"(?:\b(?:tren|it nhat|toi thieu|lon hon|cao hon|hon|tu)|>=|>)\s*(?<so>\d+(?:[.,]\d+)?)\s*(?<dv>(?:trieu|tr|cu|nghin|ngan|k|vnd|dong)(?![a-z0-9]))?", RO);
        private static readonly Regex ReKhoangGia = new Regex(@"\b(?:khoang|tam|quanh|gan|xap xi|muc gia|co gia)\s*(?:la\s*)?(?<so>\d+(?:[.,]\d+)?)\s*(?<dv>(?:trieu|tr|cu|nghin|ngan|k|vnd|dong)(?![a-z0-9]))?", RO);
        private static readonly Regex ReSoCoDv = new Regex(@"(?<![\w.,])(?<so>\d+(?:[.,]\d+)?)\s*(?<dv>(?:trieu|tr|cu|nghin|ngan|k|vnd|dong)(?![a-z0-9]))", RO);
        private static readonly Regex ReSoLon = new Regex(@"(?<![\w.,])(?<so>\d{6,})(?:\s*(?:vnd|dong|d))?(?![a-z0-9])", RO);
        private static readonly Regex ReSauSo = new Regex(@"^\s*(gb|tb|g\b|mp|mah|hz|inch|w\b)", RO);

        private static readonly Regex ReRam = new Regex(@"(?:\bram\s*(?<n1>\d{1,2})\s*(?:gb|g)?(?![a-z0-9]))|(?:\b(?<n2>\d{1,2})\s*(?:gb|g)\s*ram\b)", RO);
        private static readonly Regex ReBoNho = new Regex(@"\b(?<n>\d{1,4})\s*(?<u>gb|tb)\b", RO);
        private static readonly Regex ReBoQua = new Regex(@"^\d+(hz|mah|mp|w|inch|k|g|gb|tb)$", RO);

        private static readonly Regex ReChao = new Regex(@"^(xin\s+)?chao\b|^(hello|hi|hey|alo|helo)\b", RO);
        private static readonly Regex ReCamOn = new Regex(@"\b(cam on|thanks|thank you|tks)\b", RO);

        private static readonly Tuple<Regex, string>[] LoaiRules =
        {
            Tuple.Create(new Regex(@"\b(tai nghe|airpods|earbuds|buds|headphone|headset)\b", RO), "tai nghe"),
            Tuple.Create(new Regex(@"(?<!mau )\bsac\b|\b(cap sac|cap usb|cap lightning|cap type c|cu sac|pin du phong|adapter)\b", RO), "sac"),
            Tuple.Create(new Regex(@"\b(op lung|op|case|kinh cuong luc|mieng dan man hinh|bao ve man hinh)\b", RO), "op lung"),
            Tuple.Create(new Regex(@"\b(dien thoai|smartphone|phone|iphone|galaxy|redmi|poco|reno|di dong)\b", RO), "dien thoai"),
        };

        private static readonly Tuple<Regex, string>[] SapXepRules =
        {
            Tuple.Create(new Regex(@"\b(re nhat|gia re|thap nhat|tiet kiem|binh dan)\b", RO), "gia_tang"),
            Tuple.Create(new Regex(@"\b(cao cap|flagship|xin nhat|dat nhat|cao nhat)\b", RO), "gia_giam"),
            Tuple.Create(new Regex(@"\b(ban chay|pho bien|hot|nhieu nguoi mua)\b", RO), "banchay"),
            Tuple.Create(new Regex(@"\b(danh gia cao|tot nhat|nhieu sao|duoc yeu thich)\b", RO), "danhgia"),
            Tuple.Create(new Regex(@"\b(moi nhat|moi ra|vua ra mat)\b", RO), "moi"),
            Tuple.Create(new Regex(@"\b(game|gaming|choi game|cau hinh manh|manh nhat)\b", RO), "ram"),
        };

        // Từ chung chung, không dùng để lọc theo tên sản phẩm
        private static readonly HashSet<string> TuDung = new HashSet<string>(
            ("toi muon mua can tim cho minh em anh chi ban la co nao nhung cai chiec con dien thoai may di dong " +
             "gia tam loai dang xem tu van goi y duoi tren khoang pin tot re dep gi nhat hang cua va voi hay nen " +
             "duoc khong rat thi de mot hai ba tai nghe op lung kinh cuong luc sac cap du phong dung nhu cau " +
             "phone smartphone 5g 4g 3g").Split(' '));

        // =====================================================================
        public static ChatKetQua TraLoi(string cauHoi)
        {
            var kq = new ChatKetQua();
            var goc = (cauHoi ?? "").Trim();
            if (goc.Length > 200) goc = goc.Substring(0, 200);
            var text = ReKhoangTrang.Replace(BoDau(goc), " ").Trim();
            if (text.Length == 0)
            {
                kq.TraLoi = "Bạn hãy nhập nhu cầu của mình nhé, ví dụ: “iPhone dưới 30 triệu”.";
                kq.GoiY = GoiYMau();
                return kq;
            }

            GiaYeuCau gia = TachGia(ref text);
            int? ramGB, boNhoGB;
            TachCauHinh(ref text, out ramGB, out boNhoGB);
            string sapXep = NhanDangSapXep(text);

            // Chào hỏi / hỏi chung (bảo hành, khuyến mãi, hotline...) khi không có thông số mua hàng
            if (!gia.CoGia && !ramGB.HasValue && !boNhoGB.HasValue && TraLoiChung(text, kq)) return kq;

            using (var db = new QL_DienThoaiEntities())
            {
                var sps = db.vw_SanPhamHienThi.AsNoTracking().ToList();
                var loais = db.LoaiSanPhams.AsNoTracking().Where(x => x.HienThi).ToList();
                var hieus = db.ThuongHieux.AsNoTracking().ToList();
                var bt = db.BienTheSanPhams.AsNoTracking().Where(b => b.HienThi)
                    .Select(b => new { b.SanPhamId, b.GiaBan, b.GiaGoc, b.DungLuongGB, b.DungLuong, b.SoLuongTon })
                    .ToList()
                    .Select(b => new BienThe
                    {
                        SanPhamId = b.SanPhamId,
                        GiaBan = b.GiaBan,
                        GiaGoc = b.GiaGoc,
                        DungLuongGB = b.DungLuongGB,
                        DungLuong = b.DungLuong,
                        SoLuongTon = b.SoLuongTon
                    }).ToLookup(b => b.SanPhamId);

                var tuTheoSp = sps.ToDictionary(s => s.Id, s => new HashSet<string>(TachTu(BoDau(s.TenSanPham))));
                var tatCaTuTen = new HashSet<string>(tuTheoSp.Values.SelectMany(x => x));

                var c = new TieuChi { GiaTu = gia.Tu, GiaDen = gia.Den, RamGB = ramGB, BoNhoGB = boNhoGB };

                // Hãng: so khớp với bảng ThuongHieu trong DB
                var tenHieu = new List<string>();
                var tuHieu = new HashSet<string>();
                foreach (var h in hieus)
                {
                    var ten = BoDau(h.TenThuongHieu);
                    if (Regex.IsMatch(text, @"\b" + Regex.Escape(ten) + @"\b", RO))
                    {
                        c.ThuongHieuIds.Add(h.Id);
                        tenHieu.Add(h.TenThuongHieu);
                        tuHieu.UnionWith(TachTu(ten));
                    }
                }

                // Tên dòng máy: iphone, galaxy, pro, max, s24... (chỉ giữ từ có thật trong tên sản phẩm)
                var khongCo = new List<string>();
                foreach (var t in TachTu(text))
                {
                    if (TuDung.Contains(t) || tuHieu.Contains(t) || ReBoQua.IsMatch(t)) continue;
                    if (tatCaTuTen.Contains(t)) { if (!c.TuTen.Contains(t)) c.TuTen.Add(t); }
                    else if (t.Any(char.IsDigit)) khongCo.Add(t);     // vd "17" trong "iphone 17" mà shop chưa có
                }

                // Loại sản phẩm: nói rõ thì dùng, không nói thì mặc định là Điện thoại
                int? loaiId = TimLoai(text, loais);
                bool loaiTuChon = false;
                if (!loaiId.HasValue)
                {
                    var phone = loais.FirstOrDefault(x => BoDau(x.TenLoai).Contains("dien thoai"));
                    if (phone != null && (c.ThuongHieuIds.Count == 0 ||
                        sps.Any(s => s.LoaiId == phone.Id && c.ThuongHieuIds.Contains(s.ThuongHieuId))))
                    {
                        loaiId = phone.Id;
                        loaiTuChon = true;
                    }
                }
                c.LoaiId = loaiId;

                bool coTieuChi = gia.CoGia || c.ThuongHieuIds.Count > 0 || (loaiId.HasValue && !loaiTuChon)
                                 || ramGB.HasValue || boNhoGB.HasValue || c.TuTen.Count > 0 || sapXep != null;
                if (!coTieuChi)
                {
                    kq.TraLoi = "Mình chưa hiểu rõ nhu cầu của bạn. Bạn thử nói rõ hơn về hãng, tầm giá hoặc cấu hình nhé, ví dụ:";
                    kq.GoiY = GoiYMau();
                    return kq;
                }

                // Thử lần lượt: đủ điều kiện -> bỏ loại mặc định -> bỏ giá
                var ds = Loc(sps, bt, tuTheoSp, c);
                if (ds.Count == 0 && loaiTuChon) { c.LoaiId = null; ds = Loc(sps, bt, tuTheoSp, c); }

                var tienTo = khongCo.Count > 0
                    ? "Mình chưa thấy mẫu “" + string.Join("”, “", khongCo) + "” trong cửa hàng. " : "";
                var moTa = MoTaTieuChi(c, loais, tenHieu, gia, loaiTuChon);

                if (ds.Count == 0 && gia.CoGia)
                {
                    var c2 = new TieuChi
                    {
                        LoaiId = c.LoaiId,
                        RamGB = c.RamGB,
                        BoNhoGB = c.BoNhoGB,
                        ThuongHieuIds = c.ThuongHieuIds,
                        TuTen = c.TuTen
                    };
                    var gan = Loc(sps, bt, tuTheoSp, c2);
                    gan = (gia.Kieu == "tren" ? gan.OrderByDescending(x => x.Gia) : gan.OrderBy(x => x.Gia)).ToList();
                    gan = gan.Take(3).ToList();
                    if (gan.Count > 0)
                    {
                        kq.TraLoi = tienTo + "Hiện chưa có " + moTa + ". Một vài lựa chọn gần nhất với yêu cầu của bạn:";
                        kq.SanPhams = gan.Select(ChuyenDong).ToList();
                        kq.TongKhop = gan.Count;
                        return kq;
                    }
                }
                if (ds.Count == 0)
                {
                    kq.TraLoi = tienTo + "Mình chưa tìm thấy " + moTa + ". Bạn thử bỏ bớt điều kiện hoặc hỏi theo cách khác nhé:";
                    kq.GoiY = GoiYMau();
                    return kq;
                }

                ds = SapXepDong(ds, sapXep);
                var hien = ds.Take(SoKetQuaToiDa).ToList();
                kq.SanPhams = hien.Select(ChuyenDong).ToList();
                kq.TongKhop = ds.Count;
                kq.LoaiId = c.LoaiId;
                kq.ThuongHieuId = c.ThuongHieuIds.Count == 1 ? c.ThuongHieuIds.First() : (int?)null;
                kq.GiaTu = c.GiaTu;
                kq.GiaDen = c.GiaDen;

                var dau = tienTo + "Mình tìm thấy " + ds.Count + " " + moTa + ". ";
                var tuyen = TenSapXep(sapXep);
                kq.TraLoi = dau + (ds.Count > hien.Count
                    ? "Đây là " + hien.Count + " gợi ý " + tuyen + ":"
                    : "Mời bạn xem " + (tuyen == "nổi bật" ? "" : tuyen + " ") + "bên dưới:");
                return kq;
            }
        }

        // ===================== Lọc / sắp xếp =====================
        private class BienThe
        {
            public int SanPhamId { get; set; }
            public decimal GiaBan { get; set; }
            public decimal GiaGoc { get; set; }
            public int? DungLuongGB { get; set; }
            public string DungLuong { get; set; }
            public int SoLuongTon { get; set; }
        }

        private class TieuChi
        {
            public int? LoaiId { get; set; }
            public HashSet<int> ThuongHieuIds { get; set; } = new HashSet<int>();
            public List<string> TuTen { get; set; } = new List<string>();
            public decimal? GiaTu { get; set; }
            public decimal? GiaDen { get; set; }
            public int? RamGB { get; set; }
            public int? BoNhoGB { get; set; }
        }

        private class Dong
        {
            public vw_SanPhamHienThi Sp { get; set; }
            public decimal Gia { get; set; }
            public decimal GiaGoc { get; set; }
            public string BoNho { get; set; }
            public bool HetHang { get; set; }
        }

        private static List<Dong> Loc(List<vw_SanPhamHienThi> sps, ILookup<int, BienThe> bt,
            Dictionary<int, HashSet<string>> tuTheoSp, TieuChi c)
        {
            var kq = new List<Dong>();
            foreach (var sp in sps)
            {
                if (c.LoaiId.HasValue && sp.LoaiId != c.LoaiId.Value) continue;
                if (c.ThuongHieuIds.Count > 0 && !c.ThuongHieuIds.Contains(sp.ThuongHieuId)) continue;
                if (c.RamGB.HasValue && (!sp.RamGB.HasValue || sp.RamGB.Value < c.RamGB.Value)) continue;
                var words = tuTheoSp[sp.Id];
                if (c.TuTen.Any(t => !words.Contains(t))) continue;

                // giá và bộ nhớ lọc theo từng phiên bản (biến thể)
                var ok = bt[sp.Id].Where(b =>
                    (!c.GiaTu.HasValue || b.GiaBan >= c.GiaTu.Value) &&
                    (!c.GiaDen.HasValue || b.GiaBan <= c.GiaDen.Value) &&
                    (!c.BoNhoGB.HasValue || b.DungLuongGB == c.BoNhoGB.Value)).ToList();
                if (ok.Count == 0) continue;

                var con = ok.Where(b => b.SoLuongTon > 0).ToList();
                var chon = (con.Count > 0 ? con : ok).OrderBy(b => b.GiaBan).First();
                kq.Add(new Dong { Sp = sp, Gia = chon.GiaBan, GiaGoc = chon.GiaGoc, BoNho = chon.DungLuong, HetHang = con.Count == 0 });
            }
            return kq;
        }

        private static List<Dong> SapXepDong(List<Dong> ds, string kieu)
        {
            IOrderedEnumerable<Dong> q = ds.OrderBy(x => x.HetHang);   // còn hàng lên trước
            switch (kieu)
            {
                case "gia_tang": q = q.ThenBy(x => x.Gia); break;
                case "gia_giam": q = q.ThenByDescending(x => x.Gia); break;
                case "banchay": q = q.ThenByDescending(x => x.Sp.DaBan).ThenByDescending(x => x.Sp.SaoTrungBinh); break;
                case "danhgia": q = q.ThenByDescending(x => x.Sp.SaoTrungBinh).ThenByDescending(x => x.Sp.SoDanhGia); break;
                case "moi": q = q.ThenByDescending(x => x.Sp.NgayTao).ThenByDescending(x => x.Sp.Id); break;
                case "ram": q = q.ThenByDescending(x => x.Sp.RamGB ?? 0).ThenByDescending(x => x.Gia); break;
                default: q = q.ThenByDescending(x => x.Sp.NoiBat).ThenByDescending(x => x.Sp.DaBan).ThenByDescending(x => x.Sp.LuotXem); break;
            }
            return q.ToList();
        }

        private static ChatSanPham ChuyenDong(Dong d)
        {
            return new ChatSanPham
            {
                Id = d.Sp.Id,
                Ten = d.Sp.TenSanPham,
                ThuongHieu = d.Sp.TenThuongHieu,
                Anh = d.Sp.AnhDaiDien,
                Gia = d.Gia,
                GiaGoc = d.GiaGoc > d.Gia ? d.GiaGoc : (decimal?)null,
                BoNho = d.BoNho,
                HetHang = d.HetHang,
                Sao = d.Sp.SaoTrungBinh,
                SoDanhGia = d.Sp.SoDanhGia
            };
        }

        // ===================== Nhận diện giá =====================
        private class GiaYeuCau
        {
            public decimal? Tu { get; set; }
            public decimal? Den { get; set; }
            public string Kieu { get; set; }      // duoi | tren | khoang | trong
            public decimal? Moc { get; set; }     // mốc người dùng nhập (cho kiểu "khoảng")
            public bool CoGia { get { return Tu.HasValue || Den.HasValue; } }
        }

        private static GiaYeuCau TachGia(ref string text)
        {
            var g = new GiaYeuCau();
            text = ReSoNgan.Replace(text, m => m.Groups["n"].Value.Replace(".", "").Replace(",", "") + " ");   // 30.000.000đ -> 30000000

            // "từ 10 đến 20 triệu", "10-20tr"
            var kh = ReKhoang.Match(text);
            if (kh.Success)
            {
                var ub = kh.Groups["ub"].Value;
                var ua = kh.Groups["ua"].Value;
                if (ub.Length == 0) ub = ua;       // "10tr - 20"
                if (ua.Length == 0) ua = ub;       // "10 - 20 triệu"
                // không có đơn vị nào thì cả hai số phải đủ lớn (5000000 - 10000000), tránh nhầm "iphone 15 - 16"
                bool duLon = ua.Length > 0 || (kh.Groups["a"].Value.Length >= 6 && kh.Groups["b"].Value.Length >= 6);
                var a = duLon ? Tien(kh.Groups["a"].Value, ua) : null;
                var b = duLon ? Tien(kh.Groups["b"].Value, ub) : null;
                if (a.HasValue && b.HasValue)
                {
                    g.Tu = Math.Min(a.Value, b.Value);
                    g.Den = Math.Max(a.Value, b.Value);
                    g.Kieu = "trong";
                    text = Xoa(text, kh);
                    return g;
                }
            }

            decimal? t;
            Match m1 = TimHopLe(text, ReToiDa, out t);
            if (m1 != null) { g.Den = t; g.Kieu = "duoi"; text = Xoa(text, m1); return g; }

            m1 = TimHopLe(text, ReToiThieu, out t);
            if (m1 != null) { g.Tu = t; g.Kieu = "tren"; text = Xoa(text, m1); return g; }

            m1 = TimHopLe(text, ReKhoangGia, out t);
            if (m1 == null) m1 = TimHopLe(text, ReSoCoDv, out t);
            if (m1 == null) m1 = TimHopLe(text, ReSoLon, out t);
            if (m1 != null)
            {
                g.Moc = t;
                g.Tu = Math.Round(t.Value * 0.8m);
                g.Den = Math.Round(t.Value * 1.2m);
                g.Kieu = "khoang";
                text = Xoa(text, m1);
            }
            return g;
        }

        private static Match TimHopLe(string text, Regex re, out decimal? tien)
        {
            tien = null;
            foreach (Match m in re.Matches(text))
            {
                var dv = m.Groups["dv"].Value;
                // "dưới 256gb" không phải giá
                if (dv.Length == 0 && ReSauSo.IsMatch(text.Substring(m.Index + m.Length))) continue;
                var t = Tien(m.Groups["so"].Value, dv);
                if (t.HasValue) { tien = t; return m; }
            }
            return null;
        }

        private static decimal? Tien(string so, string dv)
        {
            decimal v;
            if (!decimal.TryParse(so.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out v)) return null;
            switch (dv)
            {
                case "trieu": case "tr": case "cu": v *= 1000000m; break;
                case "nghin": case "ngan": case "k": v *= 1000m; break;
                case "vnd": case "dong": break;
                default:                                   // không ghi đơn vị
                    if (v >= 100000m) break;               // đã là đồng
                    if (v < 1000m) { v *= 1000000m; break; }   // "dưới 30" hiểu là 30 triệu
                    return null;
            }
            return v >= 10000m ? v : (decimal?)null;       // "4k" (độ phân giải) không phải giá
        }

        private static string Xoa(string text, Match m)
        {
            return text.Remove(m.Index, m.Length).Insert(m.Index, " ");
        }

        // ===================== RAM / bộ nhớ / sắp xếp / loại =====================
        private static void TachCauHinh(ref string text, out int? ramGB, out int? boNhoGB)
        {
            ramGB = null; boNhoGB = null;

            var mr = ReRam.Match(text);
            if (mr.Success)
            {
                var n = mr.Groups["n1"].Success ? mr.Groups["n1"].Value : mr.Groups["n2"].Value;
                ramGB = int.Parse(n, CultureInfo.InvariantCulture);
                text = Xoa(text, mr);
            }

            foreach (Match m in ReBoNho.Matches(text))
            {
                int n = int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);
                bool tb = m.Groups["u"].Value == "tb";
                if (tb) boNhoGB = n * 1024;                 // trong DB: 1TB = 1024
                else if (n >= 64) boNhoGB = n;              // 128GB, 256GB...
                else if (!ramGB.HasValue && n >= 2) ramGB = n;   // "12gb" nhiều khả năng là RAM
            }
            text = ReBoNho.Replace(text, " ");
        }

        private static string NhanDangSapXep(string text)
        {
            foreach (var r in SapXepRules) if (r.Item1.IsMatch(text)) return r.Item2;
            return null;
        }

        private static string TenSapXep(string kieu)
        {
            switch (kieu)
            {
                case "gia_tang": return "giá rẻ nhất";
                case "gia_giam": return "cao cấp nhất";
                case "banchay": return "bán chạy nhất";
                case "danhgia": return "được đánh giá cao nhất";
                case "moi": return "mới nhất";
                case "ram": return "cấu hình mạnh nhất";
                default: return "nổi bật";
            }
        }

        private static int? TimLoai(string text, List<LoaiSanPham> loais)
        {
            foreach (var r in LoaiRules)
            {
                if (!r.Item1.IsMatch(text)) continue;
                var l = loais.FirstOrDefault(x => BoDau(x.TenLoai).Contains(r.Item2));
                if (l != null) return l.Id;
            }
            return null;
        }

        // ===================== Mô tả tiêu chí để trả lời =====================
        private static string MoTaTieuChi(TieuChi c, List<LoaiSanPham> loais, List<string> tenHieu, GiaYeuCau gia, bool loaiTuChon)
        {
            var dau = new List<string>();
            var loai = c.LoaiId.HasValue ? loais.FirstOrDefault(x => x.Id == c.LoaiId.Value) : null;
            dau.Add(loai != null ? loai.TenLoai.ToLower(Vi) : "sản phẩm");
            if (tenHieu.Count > 0) dau.Add(string.Join("/", tenHieu));
            if (c.TuTen.Count > 0) dau.Add(string.Join(" ", c.TuTen));

            var duoi = new List<string>();
            if (gia.CoGia)
            {
                switch (gia.Kieu)
                {
                    case "duoi": duoi.Add("giá dưới " + Chu(gia.Den.Value)); break;
                    case "tren": duoi.Add("giá từ " + Chu(gia.Tu.Value) + " trở lên"); break;
                    case "khoang": duoi.Add("giá khoảng " + Chu(gia.Moc.Value)); break;
                    default: duoi.Add("giá từ " + Chu(gia.Tu.Value) + " đến " + Chu(gia.Den.Value)); break;
                }
            }
            if (c.RamGB.HasValue) duoi.Add("RAM từ " + c.RamGB.Value + "GB");
            if (c.BoNhoGB.HasValue)
                duoi.Add("bộ nhớ " + (c.BoNhoGB.Value >= 1024 ? (c.BoNhoGB.Value / 1024) + "TB" : c.BoNhoGB.Value + "GB"));

            return string.Join(" ", dau) + (duoi.Count > 0 ? " " + string.Join(", ", duoi) : "");
        }

        private static string Chu(decimal v)
        {
            return v >= 1000000m ? (v / 1000000m).ToString("0.##", Vi) + " triệu" : v.ToString("N0", Vi) + "đ";
        }

        // ===================== Chào hỏi / câu hỏi chung =====================
        private static bool TraLoiChung(string text, ChatKetQua kq)
        {
            if (ReChao.IsMatch(text) && text.Split(' ').Length <= 4)   // chỉ chào thuần túy, "chào shop, tìm iphone 16" vẫn đi tìm sản phẩm
            {
                kq.TraLoi = "Xin chào! Mình là trợ lý tư vấn của " + CauHinh.Get("TenCuaHang", "cửa hàng") +
                            ". Bạn đang tìm sản phẩm nào, tầm giá bao nhiêu? Ví dụ:";
                kq.GoiY = GoiYMau();
                return true;
            }
            if (ReCamOn.IsMatch(text)) { kq.TraLoi = "Rất vui được hỗ trợ bạn! Cần tìm thêm gì cứ nhắn mình nhé."; return true; }

            if (Regex.IsMatch(text, @"\b(hotline|lien he|tong dai|so dien thoai cua hang|sdt)\b", RO))
            { kq.TraLoi = "Bạn có thể gọi hotline " + CauHinh.Get("Hotline") + " để được nhân viên hỗ trợ trực tiếp."; return true; }

            if (Regex.IsMatch(text, @"\btra gop\b", RO))
            { kq.TraLoi = "Điện thoại bên mình có hỗ trợ trả góp. Bạn mở trang chi tiết sản phẩm, chọn kỳ hạn và mức trả trước để xem số tiền phải trả mỗi tháng nhé."; return true; }

            if (Regex.IsMatch(text, @"\bbao hanh\b", RO))
            { kq.TraLoi = "Bạn có thể tra cứu thông tin bảo hành ở trang bên dưới."; Link(kq, "Tra cứu bảo hành", "~/BaoHanh/TraCuu"); return true; }

            if (Regex.IsMatch(text, @"\b(khuyen mai|ma giam gia|voucher|uu dai)\b", RO))
            { kq.TraLoi = "Bạn xem các chương trình khuyến mãi đang có ở trang bên dưới."; Link(kq, "Xem khuyến mãi", "~/KhuyenMai"); return true; }

            if (Regex.IsMatch(text, @"\b(thu cu|thu mua|doi cu)\b", RO))
            { kq.TraLoi = "Cửa hàng có dịch vụ thu cũ. Bạn gửi thông tin máy cũ ở trang bên dưới để được định giá."; Link(kq, "Thu cũ đổi mới", "~/ThuCu"); return true; }

            if (Regex.IsMatch(text, @"\b(don hang|theo doi don|kiem tra don)\b", RO))
            { kq.TraLoi = "Bạn xem và theo dõi đơn hàng của mình ở trang bên dưới (cần đăng nhập)."; Link(kq, "Đơn hàng của tôi", "~/DonHang"); return true; }

            if (Regex.IsMatch(text, @"\b(gio hang)\b", RO))
            { kq.TraLoi = "Bạn mở giỏ hàng ở trang bên dưới."; Link(kq, "Giỏ hàng", "~/GioHang"); return true; }

            if (Regex.IsMatch(text, @"\b(giao hang|van chuyen|ship|thanh toan|qr|vnpay)\b", RO))
            { kq.TraLoi = "Cửa hàng giao hàng toàn quốc và hỗ trợ thanh toán bằng mã QR. Phí vận chuyển và các hình thức thanh toán sẽ hiển thị ở bước đặt hàng."; return true; }

            return false;
        }

        private static void Link(ChatKetQua kq, string ten, string duongDan)
        {
            kq.LienKetTen = ten;
            kq.LienKetUrl = System.Web.VirtualPathUtility.ToAbsolute(duongDan);
        }

        private static List<string> GoiYMau()
        {
            return new List<string> { "iPhone dưới 30 triệu", "Samsung từ 10 đến 20 triệu", "Điện thoại bán chạy nhất", "Tai nghe dưới 3 triệu" };
        }

        // ===================== Tiện ích chuỗi =====================
        public static string BoDau(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (var ch in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(ch == 'đ' ? 'd' : ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static IEnumerable<string> TachTu(string s)
        {
            return Regex.Split(s, @"[^a-z0-9]+", RO).Where(x => x.Length > 0);
        }
    }
}