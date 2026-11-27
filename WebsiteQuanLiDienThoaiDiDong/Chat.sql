-- =====================================================================
--  CHAT "LIÊN HỆ" GIỮA KHÁCH HÀNG VÀ NHÂN VIÊN / ADMIN
--  Chạy trên database QL_DienThoai. Chạy lại nhiều lần cũng không sao.
--  (Nếu quên chạy file này, website sẽ tự tạo 2 bảng ở lần chat đầu tiên.)
-- =====================================================================
USE QL_DienThoai;
GO

-- Mỗi phiên = một cuộc trò chuyện của một khách (thành viên hoặc khách vãng lai)
IF OBJECT_ID(N'dbo.ChatPhien', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatPhien (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChatPhien PRIMARY KEY,
        NguoiDungId   INT NULL,                 -- có giá trị nếu khách đã đăng nhập
        KhachToken    UNIQUEIDENTIFIER NULL,    -- định danh khách vãng lai (lưu trong cookie)
        TenKhach      NVARCHAR(100) NOT NULL,
        SoDienThoai   NVARCHAR(20) NULL,
        TrangThai     NVARCHAR(10) NOT NULL CONSTRAINT DF_ChatPhien_TT DEFAULT N'Mo',   -- Mo | Dong
        NhanVienId    INT NULL,                 -- nhân viên trả lời gần nhất
        TinCuoi       NVARCHAR(200) NULL,
        NguoiGuiCuoi  NVARCHAR(10) NULL,        -- Khach | NhanVien
        NgayTao       DATETIME NOT NULL CONSTRAINT DF_ChatPhien_NgayTao DEFAULT GETDATE(),
        NgayCapNhat   DATETIME NOT NULL CONSTRAINT DF_ChatPhien_NgayCN DEFAULT GETDATE()
    );
    CREATE INDEX IX_ChatPhien_NguoiDung ON dbo.ChatPhien(NguoiDungId);
    CREATE INDEX IX_ChatPhien_Token     ON dbo.ChatPhien(KhachToken);
    CREATE INDEX IX_ChatPhien_CapNhat   ON dbo.ChatPhien(NgayCapNhat DESC);
END
GO

IF OBJECT_ID(N'dbo.ChatTinNhan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatTinNhan (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChatTinNhan PRIMARY KEY,
        PhienId     INT NOT NULL CONSTRAINT FK_ChatTinNhan_Phien REFERENCES dbo.ChatPhien(Id) ON DELETE CASCADE,
        NguoiGui    NVARCHAR(10) NOT NULL,      -- Khach | NhanVien
        NhanVienId  INT NULL,                   -- nhân viên nào gửi (nếu NguoiGui = NhanVien)
        NoiDung     NVARCHAR(1000) NOT NULL,
        DaDoc       BIT NOT NULL CONSTRAINT DF_ChatTinNhan_DaDoc DEFAULT 0,   -- người nhận đã đọc chưa
        NgayGui     DATETIME NOT NULL CONSTRAINT DF_ChatTinNhan_NgayGui DEFAULT GETDATE()
    );
    CREATE INDEX IX_ChatTinNhan_Phien ON dbo.ChatTinNhan(PhienId, Id) INCLUDE (NguoiGui, DaDoc);
END
GO
