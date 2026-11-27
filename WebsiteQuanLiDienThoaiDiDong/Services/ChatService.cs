using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using WebsiteQuanLiDienThoaiDiDong.Models;

namespace WebsiteQuanLiDienThoaiDiDong.Services
{
    // khớp từng cột của bảng ChatPhien
    public class ChatPhien
    {
        public int Id { get; set; }
        public int? NguoiDungId { get; set; }
        public Guid? KhachToken { get; set; }
        public string TenKhach { get; set; }
        public string SoDienThoai { get; set; }
        public string TrangThai { get; set; }
        public int? NhanVienId { get; set; }
        public string TinCuoi { get; set; }
        public string NguoiGuiCuoi { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime NgayCapNhat { get; set; }
    }

    // một tin nhắn kèm tên nhân viên gửi (nếu có)
    public class ChatTinNhanVM
    {
        public int Id { get; set; }
        public int PhienId { get; set; }
        public string NguoiGui { get; set; }
        public int? NhanVienId { get; set; }
        public string TenNV { get; set; }
        public string NoiDung { get; set; }
        public bool DaDoc { get; set; }
        public DateTime NgayGui { get; set; }
    }

    // một dòng trong danh sách hội thoại của trang quản trị
    public class ChatPhienVM
    {
        public int Id { get; set; }
        public string Ten { get; set; }
        public string Sdt { get; set; }
        public string Email { get; set; }
        public string TrangThai { get; set; }
        public string TinCuoi { get; set; }
        public string NguoiGuiCuoi { get; set; }
        public DateTime NgayCapNhat { get; set; }
        public bool LaThanhVien { get; set; }
        public int ChuaDoc { get; set; }
    }

    public class ChatService
    {
        public const string Khach = "Khach";
        public const string NhanVien = "NhanVien";
        public const int MaxLen = 1000;

        private static volatile bool _ready;
        private static readonly object _lock = new object();
        private readonly QL_DienThoaiEntities _db;

        public ChatService(QL_DienThoaiEntities db)
        {
            _db = db;
            EnsureSchema();
        }

        // Nếu chưa chạy Chat.sql thì tự tạo 2 bảng ở lần dùng đầu tiên (giống file Chat.sql)
        private void EnsureSchema()
        {
            if (_ready) return;
            lock (_lock)
            {
                if (_ready) return;
                _db.Database.ExecuteSqlCommand(@"
IF OBJECT_ID(N'dbo.ChatPhien', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatPhien (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChatPhien PRIMARY KEY,
        NguoiDungId   INT NULL,
        KhachToken    UNIQUEIDENTIFIER NULL,
        TenKhach      NVARCHAR(100) NOT NULL,
        SoDienThoai   NVARCHAR(20) NULL,
        TrangThai     NVARCHAR(10) NOT NULL CONSTRAINT DF_ChatPhien_TT DEFAULT N'Mo',
        NhanVienId    INT NULL,
        TinCuoi       NVARCHAR(200) NULL,
        NguoiGuiCuoi  NVARCHAR(10) NULL,
        NgayTao       DATETIME NOT NULL CONSTRAINT DF_ChatPhien_NgayTao DEFAULT GETDATE(),
        NgayCapNhat   DATETIME NOT NULL CONSTRAINT DF_ChatPhien_NgayCN DEFAULT GETDATE()
    );
    CREATE INDEX IX_ChatPhien_NguoiDung ON dbo.ChatPhien(NguoiDungId);
    CREATE INDEX IX_ChatPhien_Token     ON dbo.ChatPhien(KhachToken);
    CREATE INDEX IX_ChatPhien_CapNhat   ON dbo.ChatPhien(NgayCapNhat DESC);
END
IF OBJECT_ID(N'dbo.ChatTinNhan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatTinNhan (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChatTinNhan PRIMARY KEY,
        PhienId     INT NOT NULL CONSTRAINT FK_ChatTinNhan_Phien REFERENCES dbo.ChatPhien(Id) ON DELETE CASCADE,
        NguoiGui    NVARCHAR(10) NOT NULL,
        NhanVienId  INT NULL,
        NoiDung     NVARCHAR(1000) NOT NULL,
        DaDoc       BIT NOT NULL CONSTRAINT DF_ChatTinNhan_DaDoc DEFAULT 0,
        NgayGui     DATETIME NOT NULL CONSTRAINT DF_ChatTinNhan_NgayGui DEFAULT GETDATE()
    );
    CREATE INDEX IX_ChatTinNhan_Phien ON dbo.ChatTinNhan(PhienId, Id) INCLUDE (NguoiGui, DaDoc);
END");
                _ready = true;
            }
        }

        // ===================== PHIÊN (CUỘC TRÒ CHUYỆN) =====================

        // Tìm cuộc trò chuyện của người đang truy cập: thành viên theo tài khoản, khách vãng lai theo cookie
        public ChatPhien TimPhien(int? userId, Guid? token)
        {
            if (userId.HasValue)
            {
                var p = PhienCuaThanhVien(userId.Value);
                if (p == null && token.HasValue)
                {
                    // khách chat trước rồi mới đăng nhập: gắn cuộc trò chuyện đó vào tài khoản
                    int n = _db.Database.ExecuteSqlCommand(
                        "UPDATE ChatPhien SET NguoiDungId = @u WHERE KhachToken = @t AND NguoiDungId IS NULL",
                        new SqlParameter("@u", userId.Value), new SqlParameter("@t", token.Value));
                    if (n > 0) p = PhienCuaThanhVien(userId.Value);
                }
                return p;
            }
            if (!token.HasValue) return null;
            return _db.Database.SqlQuery<ChatPhien>(
                "SELECT TOP 1 * FROM ChatPhien WHERE KhachToken = @t AND NguoiDungId IS NULL ORDER BY NgayCapNhat DESC",
                new SqlParameter("@t", token.Value)).FirstOrDefault();
        }

        private ChatPhien PhienCuaThanhVien(int userId)
        {
            return _db.Database.SqlQuery<ChatPhien>(
                "SELECT TOP 1 * FROM ChatPhien WHERE NguoiDungId = @u ORDER BY NgayCapNhat DESC",
                new SqlParameter("@u", userId)).FirstOrDefault();
        }

        public ChatPhien LayPhien(int id)
        {
            return _db.Database.SqlQuery<ChatPhien>("SELECT * FROM ChatPhien WHERE Id = @id",
                new SqlParameter("@id", id)).FirstOrDefault();
        }

        public ChatPhien TaoPhien(int? userId, Guid? token, string ten, string sdt)
        {
            int id = _db.Database.SqlQuery<int>(
                "INSERT INTO ChatPhien (NguoiDungId, KhachToken, TenKhach, SoDienThoai) VALUES (@u, @t, @ten, @sdt); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT)",
                new SqlParameter("@u", (object)userId ?? DBNull.Value),
                new SqlParameter("@t", (object)token ?? DBNull.Value),
                new SqlParameter("@ten", ten),
                new SqlParameter("@sdt", string.IsNullOrEmpty(sdt) ? (object)DBNull.Value : sdt)).Single();
            return LayPhien(id);
        }

        public void DatTrangThai(int phienId, bool dong)
        {
            _db.Database.ExecuteSqlCommand("UPDATE ChatPhien SET TrangThai = @tt WHERE Id = @id",
                new SqlParameter("@tt", dong ? "Dong" : "Mo"), new SqlParameter("@id", phienId));
        }

        // ===================== TIN NHẮN =====================

        // after > 0: các tin mới hơn id đó. after = 0: 100 tin gần nhất (để hiện lịch sử khi mở khung chat)
        public List<ChatTinNhanVM> LayTinNhan(int phienId, int after, int top = 100)
        {
            const string cot = "m.Id, m.PhienId, m.NguoiGui, m.NhanVienId, nv.HoTen AS TenNV, m.NoiDung, m.DaDoc, m.NgayGui ";
            const string tu = "FROM ChatTinNhan m LEFT JOIN NguoiDung nv ON nv.Id = m.NhanVienId ";
            if (after > 0)
                return _db.Database.SqlQuery<ChatTinNhanVM>(
                    "SELECT TOP (@top) " + cot + tu + "WHERE m.PhienId = @p AND m.Id > @a ORDER BY m.Id",
                    new SqlParameter("@top", top), new SqlParameter("@p", phienId), new SqlParameter("@a", after)).ToList();

            return _db.Database.SqlQuery<ChatTinNhanVM>(
                "SELECT * FROM (SELECT TOP (@top) " + cot + tu + "WHERE m.PhienId = @p ORDER BY m.Id DESC) x ORDER BY x.Id",
                new SqlParameter("@top", top), new SqlParameter("@p", phienId)).ToList();
        }

        // Ghi một tin nhắn, cập nhật tin cuối của phiên và mở lại phiên nếu đang đóng. Trả về Id tin nhắn.
        public int GuiTin(int phienId, string nguoiGui, int? nhanVienId, string noiDung)
        {
            string xem = noiDung.Replace("\n", " ");
            if (xem.Length > 200) xem = xem.Substring(0, 200);

            return _db.Database.SqlQuery<int>(
                "INSERT INTO ChatTinNhan (PhienId, NguoiGui, NhanVienId, NoiDung) VALUES (@p, @g, @nv, @nd); " +
                "DECLARE @id INT = SCOPE_IDENTITY(); " +
                "UPDATE ChatPhien SET TinCuoi = @xem, NguoiGuiCuoi = @g, NgayCapNhat = GETDATE(), TrangThai = N'Mo', " +
                "       NhanVienId = CASE WHEN @g = N'NhanVien' THEN @nv ELSE NhanVienId END " +
                "WHERE Id = @p; " +
                "SELECT @id",
                new SqlParameter("@p", phienId),
                new SqlParameter("@g", nguoiGui),
                new SqlParameter("@nv", (object)nhanVienId ?? DBNull.Value),
                new SqlParameter("@nd", noiDung),
                new SqlParameter("@xem", xem)).Single();
        }

        // Đánh dấu đã đọc các tin do "nguoiGui" gửi (người nhận đã mở xem), đến hết tin có Id = denId
        public void DanhDauDaDoc(int phienId, string nguoiGui, int denId)
        {
            if (denId <= 0) return;
            _db.Database.ExecuteSqlCommand(
                "UPDATE ChatTinNhan SET DaDoc = 1 WHERE PhienId = @p AND NguoiGui = @g AND DaDoc = 0 AND Id <= @max",
                new SqlParameter("@p", phienId), new SqlParameter("@g", nguoiGui), new SqlParameter("@max", denId));
        }

        // số tin nhân viên đã gửi mà khách chưa đọc
        public int DemChuaDocCuaKhach(int phienId)
        {
            return _db.Database.SqlQuery<int>(
                "SELECT COUNT(*) FROM ChatTinNhan WHERE PhienId = @p AND NguoiGui = N'NhanVien' AND DaDoc = 0",
                new SqlParameter("@p", phienId)).Single();
        }

        // số tin khách đã gửi mà chưa nhân viên nào mở xem (tất cả hội thoại)
        public int TongChuaDocCuaNhanVien()
        {
            return _db.Database.SqlQuery<int>(
                "SELECT COUNT(*) FROM ChatTinNhan WHERE NguoiGui = N'Khach' AND DaDoc = 0").Single();
        }

        // số tin khách này đã gửi trong 1 phút gần nhất (chống spam)
        public int SoTinKhachGanDay(int phienId)
        {
            return _db.Database.SqlQuery<int>(
                "SELECT COUNT(*) FROM ChatTinNhan WHERE PhienId = @p AND NguoiGui = N'Khach' AND NgayGui > DATEADD(MINUTE, -1, GETDATE())",
                new SqlParameter("@p", phienId)).Single();
        }

        // ===================== DANH SÁCH CHO TRANG QUẢN TRỊ =====================

        // tt: "dong" = đã đóng, còn lại = đang mở. q: tìm theo tên / số điện thoại.
        public List<ChatPhienVM> DanhSach(string tt, string q)
        {
            string like = "%" + EscapeLike((q ?? "").Trim()) + "%";
            return _db.Database.SqlQuery<ChatPhienVM>(
                "SELECT TOP 100 p.Id, ISNULL(u.HoTen, p.TenKhach) AS Ten, ISNULL(u.SoDienThoai, p.SoDienThoai) AS Sdt, " +
                "       u.Email AS Email, p.TrangThai, p.TinCuoi, p.NguoiGuiCuoi, p.NgayCapNhat, " +
                "       CAST(CASE WHEN p.NguoiDungId IS NULL THEN 0 ELSE 1 END AS BIT) AS LaThanhVien, " +
                "       (SELECT COUNT(*) FROM ChatTinNhan m WHERE m.PhienId = p.Id AND m.NguoiGui = N'Khach' AND m.DaDoc = 0) AS ChuaDoc " +
                "FROM ChatPhien p LEFT JOIN NguoiDung u ON u.Id = p.NguoiDungId " +
                "WHERE p.TrangThai = @tt " +
                "  AND (@q = N'' OR ISNULL(u.HoTen, p.TenKhach) LIKE @like ESCAPE N'\\' OR ISNULL(u.SoDienThoai, p.SoDienThoai) LIKE @like ESCAPE N'\\') " +
                "ORDER BY p.NgayCapNhat DESC",
                new SqlParameter("@tt", tt == "dong" ? "Dong" : "Mo"),
                new SqlParameter("@q", (q ?? "").Trim()),
                new SqlParameter("@like", like)).ToList();
        }

        // thông tin hiển thị ở đầu khung chat của một phiên (tên/sđt/email lấy từ tài khoản nếu là thành viên)
        public ChatPhienVM ThongTinPhien(int phienId)
        {
            return _db.Database.SqlQuery<ChatPhienVM>(
                "SELECT p.Id, ISNULL(u.HoTen, p.TenKhach) AS Ten, ISNULL(u.SoDienThoai, p.SoDienThoai) AS Sdt, " +
                "       u.Email AS Email, p.TrangThai, p.TinCuoi, p.NguoiGuiCuoi, p.NgayCapNhat, " +
                "       CAST(CASE WHEN p.NguoiDungId IS NULL THEN 0 ELSE 1 END AS BIT) AS LaThanhVien, 0 AS ChuaDoc " +
                "FROM ChatPhien p LEFT JOIN NguoiDung u ON u.Id = p.NguoiDungId WHERE p.Id = @id",
                new SqlParameter("@id", phienId)).FirstOrDefault();
        }

        // ===================== TIỆN ÍCH =====================

        // bỏ ký tự điều khiển, gọn dòng trống thừa, cắt theo độ dài tối đa
        public static string LamSach(string s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s.Replace("\r\n", "\n").Replace('\r', '\n'))
                if (ch == '\n' || ch == '\t' || !char.IsControl(ch)) sb.Append(ch);
            string t = Regex.Replace(sb.ToString(), "\n{3,}", "\n\n").Trim();
            if (t.Length > max)
            {
                int cut = max;
                if (char.IsHighSurrogate(t[cut - 1])) cut--;   // đừng cắt đôi emoji
                t = t.Substring(0, cut).TrimEnd();
            }
            return t;
        }

        public static string LamSachSdt(string s)
        {
            string t = Regex.Replace(s ?? "", @"[^\d+ ]", "").Trim();
            return t.Length > 20 ? t.Substring(0, 20) : t;
        }

        // mỗi địa chỉ IP chỉ được mở tối đa 8 cuộc trò chuyện mới mỗi giờ
        public static bool VuotGioiHanTaoPhien(HttpRequestBase req)
        {
            string key = "chat:new:" + req.UserHostAddress;
            var cache = HttpRuntime.Cache;
            int n = cache[key] is int ? (int)cache[key] : 0;
            if (n >= 8) return true;
            cache.Insert(key, n + 1, null, DateTime.UtcNow.AddHours(1), System.Web.Caching.Cache.NoSlidingExpiration);
            return false;
        }

        private static string EscapeLike(string s)
        {
            return s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
        }

        public static string Ts(DateTime d) { return d.ToString("yyyy-MM-dd'T'HH:mm:ss"); }
    }
}
