USE master;
GO
SET QUOTED_IDENTIFIER ON;   -- bắt buộc cho index có điều kiện (WHERE ...)
SET ANSI_NULLS ON;
GO
 
-- ============ PHẦN 1: TẠO DATABASE VÀ BẢNG ============
IF DB_ID(N'QL_DienThoai') IS NOT NULL
BEGIN
    ALTER DATABASE QL_DienThoai SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE QL_DienThoai;
END
GO
CREATE DATABASE QL_DienThoai;
GO
USE QL_DienThoai;
GO
 
-- 1. TÀI KHOẢN, PHÂN QUYỀN, ĐỊA CHỈ, PHÍ SHIP
CREATE TABLE VaiTro (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TenVaiTro NVARCHAR(30) NOT NULL UNIQUE
);
 
CREATE TABLE NguoiDung (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VaiTroId INT NOT NULL REFERENCES VaiTro(Id),
    HoTen NVARCHAR(100) NOT NULL,
    Email NVARCHAR(150) NOT NULL UNIQUE,
    SoDienThoai VARCHAR(15) NULL,
    MatKhauHash VARCHAR(100) NULL,          -- NULL nếu chỉ đăng nhập bằng Google. Demo dùng SHA-256; làm thật nên dùng BCrypt (60 ký tự)
    GoogleId VARCHAR(100) NULL,
    TrangThai BIT NOT NULL DEFAULT 1,       -- 0 = khóa tài khoản
    NgayTao DATETIME2 NOT NULL DEFAULT SYSDATETIME()
);
CREATE UNIQUE INDEX UX_NguoiDung_GoogleId ON NguoiDung(GoogleId) WHERE GoogleId IS NOT NULL;
 
CREATE TABLE TokenDatLaiMatKhau (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NguoiDungId INT NOT NULL REFERENCES NguoiDung(Id) ON DELETE CASCADE,
    Token VARCHAR(100) NOT NULL UNIQUE,      -- nên lưu SHA-256 của token, gửi token gốc qua email
    HetHan DATETIME2 NOT NULL,
    DaDung BIT NOT NULL DEFAULT 0
);
 
CREATE TABLE DiaChiGiaoHang (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NguoiDungId INT NOT NULL REFERENCES NguoiDung(Id) ON DELETE CASCADE,
    TenNguoiNhan NVARCHAR(100) NOT NULL,
    SoDienThoai VARCHAR(15) NOT NULL,
    TinhThanh NVARCHAR(50) NOT NULL,
    QuanHuyen NVARCHAR(50) NULL,            -- để NULL nếu API địa chỉ chỉ còn 2 cấp
    PhuongXa NVARCHAR(50) NULL,
    DiaChiChiTiet NVARCHAR(200) NOT NULL,
    MacDinh BIT NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX UX_DiaChi_MacDinh ON DiaChiGiaoHang(NguoiDungId) WHERE MacDinh = 1;  -- mỗi khách tối đa 1 địa chỉ mặc định
CREATE INDEX IX_DiaChi_NguoiDung ON DiaChiGiaoHang(NguoiDungId);
 
CREATE TABLE PhiVanChuyen (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TinhThanh NVARCHAR(50) NOT NULL UNIQUE,  -- dòng N'Khác' dùng làm mặc định
    PhiShip DECIMAL(18,0) NOT NULL CHECK (PhiShip >= 0),
    SoNgayGiao TINYINT NOT NULL
);
 
-- 2. LOẠI, THƯƠNG HIỆU, SẢN PHẨM, BIẾN THỂ
CREATE TABLE LoaiSanPham (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TenLoai NVARCHAR(100) NOT NULL UNIQUE,
    MoTa NVARCHAR(300) NULL,
    HienThi BIT NOT NULL DEFAULT 1
);
 
CREATE TABLE ThuongHieu (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TenThuongHieu NVARCHAR(100) NOT NULL UNIQUE
);
 
CREATE TABLE SanPham (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    LoaiId INT NOT NULL REFERENCES LoaiSanPham(Id),
    ThuongHieuId INT NOT NULL REFERENCES ThuongHieu(Id),
    TenSanPham NVARCHAR(200) NOT NULL,
    MoTa NVARCHAR(MAX) NULL,
    ManHinh NVARCHAR(150) NULL,              -- các cột thông số dùng cho trang So sánh
    ChipXuLy NVARCHAR(100) NULL,
    RAM NVARCHAR(20) NULL,                   -- dạng chữ để hiển thị ('8GB')
    RamGB TINYINT NULL,                      -- dạng số để lọc/sắp xếp (8)
    CameraSau NVARCHAR(200) NULL,
    CameraTruoc NVARCHAR(100) NULL,
    Pin NVARCHAR(100) NULL,
    HeDieuHanh NVARCHAR(50) NULL,
    TrongLuong NVARCHAR(30) NULL,
    BaoHanhThang INT NOT NULL DEFAULT 12 CHECK (BaoHanhThang >= 0),
    AnhDaiDien NVARCHAR(250) NULL,
    NoiBat BIT NOT NULL DEFAULT 0,
    HienThi BIT NOT NULL DEFAULT 1,          -- xóa mềm: đặt về 0 thay vì DELETE
    LuotXem INT NOT NULL DEFAULT 0,
    NgayTao DATETIME2 NOT NULL DEFAULT SYSDATETIME()
);
 
CREATE TABLE BienTheSanPham (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SanPhamId INT NOT NULL REFERENCES SanPham(Id),
    MauSac NVARCHAR(50) NOT NULL,
    DungLuong NVARCHAR(20) NULL,             -- dạng chữ ('256GB'); NULL với phụ kiện
    DungLuongGB INT NULL,                    -- dạng số để lọc (256; 1TB = 1024)
    SKU VARCHAR(50) NOT NULL UNIQUE,
    GiaGoc DECIMAL(18,0) NOT NULL,
    GiaBan DECIMAL(18,0) NOT NULL,
    SoLuongTon INT NOT NULL DEFAULT 0 CHECK (SoLuongTon >= 0),
    NguongCanhBao INT NOT NULL DEFAULT 5,
    HienThi BIT NOT NULL DEFAULT 1,
    CONSTRAINT CK_BienThe_Gia CHECK (GiaBan > 0 AND GiaGoc >= GiaBan),
    CONSTRAINT UQ_BienThe UNIQUE (SanPhamId, MauSac, DungLuong)
);
 
CREATE TABLE HinhAnhSanPham (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SanPhamId INT NOT NULL REFERENCES SanPham(Id) ON DELETE CASCADE,
    BienTheId INT NULL REFERENCES BienTheSanPham(Id),
    DuongDan NVARCHAR(250) NOT NULL,
    LaAnhChinh BIT NOT NULL DEFAULT 0,
    ThuTu INT NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX UX_HinhAnh_Chinh ON HinhAnhSanPham(SanPhamId) WHERE LaAnhChinh = 1;  -- mỗi sản phẩm tối đa 1 ảnh chính
CREATE INDEX IX_HinhAnh_SanPham ON HinhAnhSanPham(SanPhamId);
 
CREATE TABLE SanPhamLienQuan (               -- sản phẩm liên quan / phụ kiện mua cùng
    SanPhamId INT NOT NULL REFERENCES SanPham(Id),
    SanPhamLienQuanId INT NOT NULL REFERENCES SanPham(Id),
    LoaiLienQuan VARCHAR(10) NOT NULL CHECK (LoaiLienQuan IN ('PhuKien','TuongTu')),
    PRIMARY KEY (SanPhamId, SanPhamLienQuanId),
    CHECK (SanPhamId <> SanPhamLienQuanId)
);
 
CREATE TABLE YeuThich (
    NguoiDungId INT NOT NULL REFERENCES NguoiDung(Id) ON DELETE CASCADE,
    SanPhamId INT NOT NULL REFERENCES SanPham(Id),
    NgayThem DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    PRIMARY KEY (NguoiDungId, SanPhamId)
);
CREATE INDEX IX_YeuThich_SanPham ON YeuThich(SanPhamId);
 
CREATE TABLE DanhGia (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SanPhamId INT NOT NULL REFERENCES SanPham(Id),
    NguoiDungId INT NOT NULL REFERENCES NguoiDung(Id),
    SoSao TINYINT NOT NULL CHECK (SoSao BETWEEN 1 AND 5),
    NoiDung NVARCHAR(1000) NULL,
    NgayDanhGia DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    DaDuyet BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_DanhGia UNIQUE (SanPhamId, NguoiDungId)
);
 
-- 3. GIỎ HÀNG (hỗ trợ khách chưa đăng nhập), KHUYẾN MÃI, TRẢ GÓP
CREATE TABLE GioHang (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NguoiDungId INT NULL REFERENCES NguoiDung(Id) ON DELETE CASCADE,   -- NULL = giỏ của khách chưa đăng nhập
    SessionToken VARCHAR(64) NULL,                                      -- mã định danh khách (lưu trong cookie)
    NgayCapNhat DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_GioHang_ChuSoHuu CHECK (NguoiDungId IS NOT NULL OR SessionToken IS NOT NULL)
);
CREATE UNIQUE INDEX UX_GioHang_NguoiDung ON GioHang(NguoiDungId)  WHERE NguoiDungId IS NOT NULL;
CREATE UNIQUE INDEX UX_GioHang_Session   ON GioHang(SessionToken) WHERE SessionToken IS NOT NULL;
 
CREATE TABLE GioHangChiTiet (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    GioHangId INT NOT NULL REFERENCES GioHang(Id) ON DELETE CASCADE,
    BienTheId INT NOT NULL REFERENCES BienTheSanPham(Id),
    SoLuong INT NOT NULL CHECK (SoLuong > 0),
    CONSTRAINT UQ_GioHangChiTiet UNIQUE (GioHangId, BienTheId)
);
CREATE INDEX IX_GioHangCT_BienThe ON GioHangChiTiet(BienTheId);
 
CREATE TABLE KhuyenMai (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    MaCode VARCHAR(30) NOT NULL UNIQUE,
    TenKhuyenMai NVARCHAR(150) NOT NULL,
    LoaiGiam VARCHAR(10) NOT NULL CHECK (LoaiGiam IN ('PHANTRAM','SOTIEN')),
    GiaTriGiam DECIMAL(18,2) NOT NULL CHECK (GiaTriGiam > 0),
    GiamToiDa DECIMAL(18,0) NULL,
    DonToiThieu DECIMAL(18,0) NOT NULL DEFAULT 0,
    NgayBatDau DATETIME2 NOT NULL,
    NgayKetThuc DATETIME2 NOT NULL,
    SoLuong INT NULL,                        -- NULL = không giới hạn tổng lượt dùng
    GioiHanMoiNguoi INT NULL CHECK (GioiHanMoiNguoi > 0),   -- NULL = mỗi người dùng không giới hạn lần
    DaDung INT NOT NULL DEFAULT 0,
    TrangThai BIT NOT NULL DEFAULT 1,
    CHECK (NgayKetThuc > NgayBatDau)
);
 
CREATE TABLE GoiTraGop (                     -- cấu hình tính trả góp 0%
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SoThang TINYINT NOT NULL UNIQUE,
    TraTruocToiThieuPT TINYINT NOT NULL DEFAULT 0 CHECK (TraTruocToiThieuPT BETWEEN 0 AND 90),
    PhiChuyenDoiPT DECIMAL(4,1) NOT NULL DEFAULT 0,   -- % phí chuyển đổi trên số tiền vay (0 = trả góp 0% thật sự)
    HienThi BIT NOT NULL DEFAULT 1
);
 
-- 4. ĐƠN HÀNG, THANH TOÁN, IMEI, TỒN KHO
CREATE TABLE DonHang (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    MaDonHang VARCHAR(20) NOT NULL UNIQUE,   -- dùng làm nội dung chuyển khoản QR
    NguoiDungId INT NOT NULL REFERENCES NguoiDung(Id),
    TenNguoiNhan NVARCHAR(100) NOT NULL,
    SoDienThoai VARCHAR(15) NOT NULL,
    Email NVARCHAR(150) NOT NULL,
    DiaChiGiao NVARCHAR(300) NOT NULL,
    TinhThanh NVARCHAR(50) NOT NULL,
    GhiChu NVARCHAR(300) NULL,
    KhuyenMaiId INT NULL REFERENCES KhuyenMai(Id),
    TongTienHang DECIMAL(18,0) NOT NULL DEFAULT 0,
    PhiShip DECIMAL(18,0) NOT NULL DEFAULT 0,
    TienGiam DECIMAL(18,0) NOT NULL DEFAULT 0,
    TongThanhToan AS (TongTienHang + PhiShip - TienGiam) PERSISTED,
    PhuongThucThanhToan VARCHAR(10) NOT NULL CHECK (PhuongThucThanhToan IN ('COD','QR')),
    TrangThaiThanhToan VARCHAR(20) NOT NULL DEFAULT 'ChuaThanhToan'
        CHECK (TrangThaiThanhToan IN ('ChuaThanhToan','DaThanhToan','HoanTien')),
    TrangThai VARCHAR(20) NOT NULL DEFAULT 'ChoXacNhan'
        CHECK (TrangThai IN ('ChoXacNhan','DaXacNhan','DangGiao','HoanTat','DaHuy')),
    NgayDat DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    NgayThanhToan DATETIME2 NULL,
    NgayGiaoXong DATETIME2 NULL
);
 
CREATE TABLE KhuyenMaiSuDung (               -- ghi nhận ai đã dùng mã nào (để giới hạn số lần mỗi người)
    Id INT IDENTITY(1,1) PRIMARY KEY,
    KhuyenMaiId INT NOT NULL REFERENCES KhuyenMai(Id),
    NguoiDungId INT NOT NULL REFERENCES NguoiDung(Id),
    DonHangId INT NOT NULL UNIQUE REFERENCES DonHang(Id),
    NgayDung DATETIME2 NOT NULL DEFAULT SYSDATETIME()
);
CREATE INDEX IX_KMSuDung ON KhuyenMaiSuDung(KhuyenMaiId, NguoiDungId);
 
CREATE TABLE ChiTietDonHang (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    DonHangId INT NOT NULL REFERENCES DonHang(Id),
    BienTheId INT NOT NULL REFERENCES BienTheSanPham(Id),
    TenSanPham NVARCHAR(250) NOT NULL,       -- lưu lại tên + màu + dung lượng lúc mua
    SoLuong INT NOT NULL CHECK (SoLuong > 0),
    DonGia DECIMAL(18,0) NOT NULL,
    ThanhTien AS (SoLuong * DonGia) PERSISTED,
    CONSTRAINT UQ_ChiTietDonHang UNIQUE (DonHangId, BienTheId)
);
 
CREATE TABLE ImeiSanPham (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Imei CHAR(15) NOT NULL UNIQUE CHECK (Imei NOT LIKE '%[^0-9]%'),
    ChiTietDonHangId INT NOT NULL REFERENCES ChiTietDonHang(Id),
    NgayBan DATE NOT NULL,
    NgayHetBaoHanh DATE NOT NULL
);
CREATE INDEX IX_Imei_ChiTiet ON ImeiSanPham(ChiTietDonHangId);
 
CREATE TABLE ThanhToanGiaoDich (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    DonHangId INT NOT NULL REFERENCES DonHang(Id),
    PhuongThuc VARCHAR(10) NOT NULL CHECK (PhuongThuc IN ('COD','QR')),
    SoTien DECIMAL(18,0) NOT NULL,
    MaGiaoDich VARCHAR(50) NULL,
    NoiDungCK NVARCHAR(200) NULL,
    NgayThanhToan DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    NguoiXacNhanId INT NULL REFERENCES NguoiDung(Id)
);
CREATE UNIQUE INDEX UX_GiaoDich_MaGD ON ThanhToanGiaoDich(MaGiaoDich) WHERE MaGiaoDich IS NOT NULL;  -- 1 mã ngân hàng chỉ tính 1 lần
CREATE INDEX IX_GiaoDich_DonHang ON ThanhToanGiaoDich(DonHangId);
 
CREATE TABLE DonHangLichSu (                 -- timeline trạng thái cho khách theo dõi
    Id INT IDENTITY(1,1) PRIMARY KEY,
    DonHangId INT NOT NULL REFERENCES DonHang(Id),
    TrangThai VARCHAR(20) NOT NULL,
    GhiChu NVARCHAR(200) NULL,
    NguoiCapNhatId INT NULL REFERENCES NguoiDung(Id),
    NgayCapNhat DATETIME2 NOT NULL DEFAULT SYSDATETIME()
);
CREATE INDEX IX_LichSuDon_DonHang ON DonHangLichSu(DonHangId);
 
CREATE TABLE LichSuTonKho (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BienTheId INT NOT NULL REFERENCES BienTheSanPham(Id),
    SoLuongThayDoi INT NOT NULL,
    LyDo NVARCHAR(100) NOT NULL,
    DonHangId INT NULL REFERENCES DonHang(Id),
    NgayTao DATETIME2 NOT NULL DEFAULT SYSDATETIME()
);
CREATE INDEX IX_TonKho_BienThe ON LichSuTonKho(BienTheId);
 
-- 5. CẤU HÌNH, NHẬT KÝ EMAIL, MÃ ĐƠN
CREATE TABLE CauHinhHeThong (
    Khoa VARCHAR(50) PRIMARY KEY,
    GiaTri NVARCHAR(300) NOT NULL,
    MoTa NVARCHAR(200) NULL
);
 
CREATE TABLE NhatKyEmail (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    DonHangId INT NULL REFERENCES DonHang(Id),
    EmailNhan NVARCHAR(150) NOT NULL,
    TieuDe NVARCHAR(200) NOT NULL,
    TrangThai VARCHAR(10) NOT NULL CHECK (TrangThai IN ('ThanhCong','ThatBai')),
    ThongBaoLoi NVARCHAR(300) NULL,          -- lý do gửi thất bại (nếu có)
    NgayGui DATETIME2 NOT NULL DEFAULT SYSDATETIME()
);
 
CREATE SEQUENCE dbo.seq_MaDonHang AS INT START WITH 2001 INCREMENT BY 1;
 
-- 6. INDEX TÌM KIẾM / LỌC
CREATE INDEX IX_SanPham_Loai ON SanPham(LoaiId);
CREATE INDEX IX_SanPham_ThuongHieu ON SanPham(ThuongHieuId);
CREATE INDEX IX_SanPham_Ten ON SanPham(TenSanPham);
CREATE INDEX IX_BienThe_SanPham ON BienTheSanPham(SanPhamId);
CREATE INDEX IX_DanhGia_SanPham ON DanhGia(SanPhamId);
CREATE INDEX IX_DonHang_NguoiDung ON DonHang(NguoiDungId, NgayDat DESC);
CREATE INDEX IX_DonHang_TrangThai ON DonHang(TrangThai);
CREATE INDEX IX_ChiTiet_BienThe ON ChiTietDonHang(BienTheId);
GO
-- ============ PHẦN 2: DỮ LIỆU MẪU ============
USE QL_DienThoai;
GO
SET NOCOUNT ON;
 
-- Vai trò
SET IDENTITY_INSERT VaiTro ON;
INSERT INTO VaiTro (Id, TenVaiTro) VALUES (1, N'Admin'), (2, N'NhanVien'), (3, N'KhachHang');
SET IDENTITY_INSERT VaiTro OFF;
 
-- Người dùng. Mật khẩu demo: 123456, lưu dạng SHA-256 (chữ thường)
DECLARE @mk VARCHAR(100) = '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92';
SET IDENTITY_INSERT NguoiDung ON;
INSERT INTO NguoiDung (Id, VaiTroId, HoTen, Email, SoDienThoai, MatKhauHash, GoogleId, NgayTao) VALUES
(1, 1, N'Quản trị viên',   'admin@example.com',        '0281234567', @mk, NULL, '2026-01-05'),
(2, 2, N'Nguyễn Thị Lan',  'lan.nhanvien@example.com', '0281234568', @mk, NULL, '2026-01-05'),
(3, 3, N'Nguyễn Văn An',   'an.nguyen@example.com',    '0901234567', @mk, NULL, '2026-03-02'),
(4, 3, N'Trần Thị Bích',   'bich.tran@example.com',    '0912345678', @mk, NULL, '2026-03-15'),
(5, 3, N'Lê Hoàng Cường',  'cuong.le@example.com',     '0933456789', @mk, NULL, '2025-08-01'),
(6, 3, N'Phạm Minh Đức',   'duc.pham@example.com',     '0944567890', @mk, NULL, '2026-05-30'),
(7, 3, N'Võ Thị Hà',       'ha.vo@example.com',        '0977678901', @mk, NULL, '2026-06-18'),
(8, 3, N'Đặng Quốc Khánh', 'khanh.dang@example.com',   '0988789012', NULL, 'google-oauth-100234567890', '2026-07-22');
SET IDENTITY_INSERT NguoiDung OFF;
 
-- Địa chỉ giao hàng (dòng MacDinh = 1 được dùng khi tạo đơn mẫu)
INSERT INTO DiaChiGiaoHang (NguoiDungId, TenNguoiNhan, SoDienThoai, TinhThanh, PhuongXa, DiaChiChiTiet, MacDinh) VALUES
(3, N'Nguyễn Văn An',   '0901234567', N'Hồ Chí Minh', N'Phường Bến Thành', N'45 Lê Lợi', 1),
(4, N'Trần Thị Bích',   '0912345678', N'Hà Nội',      N'Phường Cầu Giấy',  N'28 Trần Thái Tông', 1),
(5, N'Lê Hoàng Cường',  '0933456789', N'Đà Nẵng',     N'Phường Hải Châu',  N'72 Bạch Đằng', 1),
(6, N'Phạm Minh Đức',   '0944567890', N'Hồ Chí Minh', N'Phường Thủ Đức',   N'15 Võ Văn Ngân', 1),
(7, N'Võ Thị Hà',       '0977678901', N'Cần Thơ',     N'Phường Ninh Kiều', N'88 Đường 3 Tháng 2', 1),
(8, N'Đặng Quốc Khánh', '0988789012', N'Hải Phòng',   N'Phường Hồng Bàng', N'19 Điện Biên Phủ', 1),
(3, N'Nguyễn Văn An',   '0901234567', N'Đồng Nai',    N'Phường Biên Hòa',  N'12 Phạm Văn Thuận', 0);
 
-- Phí vận chuyển theo tỉnh/thành
INSERT INTO PhiVanChuyen (TinhThanh, PhiShip, SoNgayGiao) VALUES
(N'Hồ Chí Minh', 20000, 1), (N'Hà Nội', 35000, 3), (N'Đà Nẵng', 35000, 2),
(N'Cần Thơ', 30000, 2), (N'Hải Phòng', 35000, 3), (N'Huế', 35000, 3),
(N'Đồng Nai', 25000, 2), (N'Khánh Hòa', 30000, 3), (N'Lâm Đồng', 30000, 3),
(N'Khác', 40000, 5);
 
-- Loại và thương hiệu
SET IDENTITY_INSERT LoaiSanPham ON;
INSERT INTO LoaiSanPham (Id, TenLoai, MoTa) VALUES
(1, N'Điện thoại', N'Điện thoại di động chính hãng'),
(2, N'Tai nghe', N'Tai nghe không dây, chống ồn'),
(3, N'Sạc, cáp & pin dự phòng', N'Củ sạc nhanh, cáp, pin sạc dự phòng'),
(4, N'Ốp lưng & kính cường lực', N'Bảo vệ điện thoại');
SET IDENTITY_INSERT LoaiSanPham OFF;
 
SET IDENTITY_INSERT ThuongHieu ON;
INSERT INTO ThuongHieu (Id, TenThuongHieu) VALUES
(1, N'Apple'), (2, N'Samsung'), (3, N'Xiaomi'), (4, N'OPPO'),
(5, N'vivo'), (6, N'Anker'), (7, N'Spigen'), (8, N'Baseus');
SET IDENTITY_INSERT ThuongHieu OFF;
 
-- Sản phẩm (thông số và giá chỉ mang tính tham khảo, hãy đối chiếu lại với trang hãng)
SET IDENTITY_INSERT SanPham ON;
INSERT INTO SanPham (Id, LoaiId, ThuongHieuId, TenSanPham, MoTa, ManHinh, ChipXuLy, RAM, CameraSau, CameraTruoc, Pin, HeDieuHanh, TrongLuong, BaoHanhThang, NoiBat, AnhDaiDien, LuotXem) VALUES
(1, 1, 1, N'iPhone 16 Pro Max', N'Flagship khung Titan, chip A18 Pro, camera 48MP với zoom quang học 5x, nút Camera Control.', N'6.9" Super Retina XDR OLED, 120Hz', N'Apple A18 Pro', N'8GB', N'48MP + 48MP + 12MP (zoom 5x)', N'12MP', N'Xem video đến 33 giờ', N'iOS 18', N'227 g', 12, 1, '/Content/images/products/iphone-16-pro-max.jpg', 3250),
(2, 1, 1, N'iPhone 16', N'Chip A18, nút Camera Control, camera Fusion 48MP, Dynamic Island.', N'6.1" Super Retina XDR OLED, 60Hz', N'Apple A18', N'8GB', N'48MP + 12MP', N'12MP', N'Xem video đến 22 giờ', N'iOS 18', N'170 g', 12, 1, '/Content/images/products/iphone-16.jpg', 2870),
(3, 1, 1, N'iPhone 15', N'Dynamic Island, camera chính 48MP, cổng USB-C, chip A16 Bionic.', N'6.1" Super Retina XDR OLED, 60Hz', N'Apple A16 Bionic', N'6GB', N'48MP + 12MP', N'12MP', N'Xem video đến 20 giờ', N'iOS 17', N'171 g', 12, 0, '/Content/images/products/iphone-15.jpg', 2410),
(4, 1, 2, N'Samsung Galaxy S25 Ultra', N'Flagship Galaxy AI, bút S Pen, camera chính 200MP, khung Titan.', N'6.9" Dynamic AMOLED 2X, 120Hz', N'Snapdragon 8 Elite for Galaxy', N'12GB', N'200MP + 50MP + 50MP (zoom 5x) + 10MP (zoom 3x)', N'12MP', N'5000 mAh, sạc nhanh 45W', N'Android 15 (One UI 7)', N'218 g', 12, 1, '/Content/images/products/galaxy-s25-ultra.jpg', 2980),
(5, 1, 2, N'Samsung Galaxy Z Flip6', N'Điện thoại gập vỏ sò, màn hình ngoài 3.4 inch, hỗ trợ Galaxy AI.', N'6.7" Dynamic AMOLED 2X, 120Hz (màn ngoài 3.4")', N'Snapdragon 8 Gen 3 for Galaxy', N'12GB', N'50MP + 12MP', N'10MP', N'4000 mAh, sạc nhanh 25W', N'Android 14 (One UI 6.1)', N'187 g', 12, 1, '/Content/images/products/galaxy-z-flip6.jpg', 1760),
(6, 1, 2, N'Samsung Galaxy A56 5G', N'Tầm trung cao cấp, màn hình Super AMOLED 120Hz, cập nhật phần mềm dài hạn.', N'6.7" Super AMOLED, 120Hz', N'Exynos 1580', N'8GB', N'50MP + 12MP + 5MP', N'12MP', N'5000 mAh, sạc nhanh 45W', N'Android 15 (One UI 7)', N'198 g', 12, 0, '/Content/images/products/galaxy-a56-5g.jpg', 1520),
(7, 1, 3, N'Xiaomi Redmi Note 14 Pro 5G', N'Camera chính 200MP, màn hình AMOLED 120Hz, sạc nhanh 45W, giá dễ tiếp cận.', N'6.67" AMOLED, 120Hz', N'Snapdragon 7s Gen 3', N'8GB', N'200MP + 8MP + 2MP', N'20MP', N'5110 mAh, sạc nhanh 45W', N'HyperOS (Android 14)', N'190 g', 12, 1, '/Content/images/products/redmi-note-14-pro-5g.jpg', 2190),
(8, 1, 4, N'OPPO Reno12 F 5G', N'Thiết kế mỏng nhẹ, camera selfie 32MP, sạc nhanh SUPERVOOC 45W.', N'6.67" AMOLED, 120Hz', N'MediaTek Dimensity 6300', N'8GB', N'50MP + 8MP + 2MP', N'32MP', N'5000 mAh, sạc nhanh 45W', N'ColorOS 14 (Android 14)', N'177 g', 12, 0, '/Content/images/products/oppo-reno12-f-5g.jpg', 980),
(9, 1, 5, N'vivo V40 5G', N'Camera ZEISS, camera selfie 50MP, pin 5500mAh sạc nhanh 80W.', N'6.78" AMOLED cong, 120Hz', N'Snapdragon 7 Gen 3', N'8GB', N'50MP + 50MP', N'50MP', N'5500 mAh, sạc nhanh 80W', N'Funtouch OS 14 (Android 14)', N'183 g', 12, 0, '/Content/images/products/vivo-v40-5g.jpg', 870),
(10, 2, 1, N'AirPods Pro 2 (USB-C)', N'Chống ồn chủ động, chip H2, hộp sạc USB-C.', NULL, NULL, NULL, NULL, NULL, N'Khoảng 6 giờ nghe (30 giờ cùng hộp sạc)', NULL, NULL, 12, 1, '/Content/images/products/airpods-pro-2.jpg', 1340),
(11, 2, 2, N'Samsung Galaxy Buds3 Pro', N'Chống ồn chủ động thông minh, âm thanh Hi-Fi 24-bit.', NULL, NULL, NULL, NULL, NULL, N'Khoảng 6 giờ nghe (khoảng 26 giờ cùng hộp sạc)', NULL, NULL, 12, 0, '/Content/images/products/galaxy-buds3-pro.jpg', 760),
(12, 3, 6, N'Củ sạc nhanh Anker Nano 30W USB-C', N'Củ sạc nhỏ gọn 30W, cổng USB-C, sạc nhanh cho iPhone và Android.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 12, 0, '/Content/images/products/anker-nano-30w.jpg', 1120),
(13, 3, 8, N'Cáp sạc USB-C to USB-C Baseus 100W 1m', N'Cáp bọc dù chống đứt, hỗ trợ sạc nhanh tối đa 100W.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, 0, '/Content/images/products/baseus-cap-100w.jpg', 640),
(14, 3, 6, N'Pin sạc dự phòng Anker 10.000mAh 22.5W', N'Pin dự phòng mỏng nhẹ, sạc nhanh 22.5W.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 12, 0, '/Content/images/products/anker-pin-10000.jpg', 590),
(15, 4, 7, N'Ốp lưng Spigen Liquid Air iPhone 16 Pro Max', N'Ốp mỏng nhẹ, ôm sát máy, chống sốc, bám tay.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, '/Content/images/products/spigen-ip16pm.jpg', 710),
(16, 4, 7, N'Ốp lưng Spigen Ultra Hybrid Galaxy S25 Ultra', N'Ốp trong suốt viền chống sốc, không ố vàng.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, '/Content/images/products/spigen-s25-ultra.jpg', 520),
(17, 4, 7, N'Kính cường lực Spigen GlasTR EZ Fit iPhone 16', N'Kính cường lực kèm khung dán dễ canh, độ cứng 9H.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, '/Content/images/products/kinh-cuong-luc-ip16.jpg', 430);
SET IDENTITY_INSERT SanPham OFF;
 
-- Biến thể: màu + dung lượng, mỗi biến thể có giá và tồn kho riêng
SET IDENTITY_INSERT BienTheSanPham ON;
INSERT INTO BienTheSanPham (Id, SanPhamId, MauSac, DungLuong, SKU, GiaGoc, GiaBan, SoLuongTon, NguongCanhBao) VALUES
(1, 1, N'Titan Sa Mạc', '256GB', 'IP16PM-256-DES', 34990000, 33990000, 12, 5),
(2, 1, N'Titan Tự Nhiên', '256GB', 'IP16PM-256-NAT', 34990000, 33990000, 15, 5),
(3, 1, N'Titan Trắng', '256GB', 'IP16PM-256-WHT', 34990000, 33990000, 9, 5),
(4, 1, N'Titan Đen', '256GB', 'IP16PM-256-BLK', 34990000, 33990000, 18, 5),
(5, 1, N'Titan Sa Mạc', '512GB', 'IP16PM-512-DES', 40990000, 39490000, 6, 3),
(6, 1, N'Titan Đen', '512GB', 'IP16PM-512-BLK', 40990000, 39490000, 7, 3),
(7, 1, N'Titan Tự Nhiên', '1TB', 'IP16PM-1TB-NAT', 46990000, 45490000, 2, 3),
(8, 2, N'Đen', '128GB', 'IP16-128-BLK', 22990000, 21990000, 25, 5),
(9, 2, N'Trắng', '128GB', 'IP16-128-WHT', 22990000, 21990000, 20, 5),
(10, 2, N'Xanh Lưu Ly', '128GB', 'IP16-128-ULT', 22990000, 21990000, 14, 5),
(11, 2, N'Hồng', '128GB', 'IP16-128-PNK', 22990000, 21990000, 17, 5),
(12, 2, N'Đen', '256GB', 'IP16-256-BLK', 25990000, 24990000, 11, 5),
(13, 2, N'Xanh Mòng Két', '256GB', 'IP16-256-TEA', 25990000, 24990000, 8, 5),
(14, 3, N'Đen', '128GB', 'IP15-128-BLK', 19990000, 17990000, 22, 5),
(15, 3, N'Xanh', '128GB', 'IP15-128-BLU', 19990000, 17990000, 16, 5),
(16, 3, N'Hồng', '128GB', 'IP15-128-PNK', 19990000, 17990000, 13, 5),
(17, 3, N'Đen', '256GB', 'IP15-256-BLK', 22990000, 20490000, 10, 5),
(18, 4, N'Xám Titan', '256GB', 'S25U-256-GRY', 33990000, 31990000, 19, 5),
(19, 4, N'Đen Titan', '256GB', 'S25U-256-BLK', 33990000, 31990000, 21, 5),
(20, 4, N'Xanh Titan', '256GB', 'S25U-256-BLU', 33990000, 31990000, 8, 5),
(21, 4, N'Xám Titan', '512GB', 'S25U-512-GRY', 37990000, 35490000, 9, 5),
(22, 4, N'Đen Titan', '512GB', 'S25U-512-BLK', 37990000, 35490000, 9, 5),
(23, 5, N'Xanh Dương', '256GB', 'ZF6-256-BLU', 28990000, 24990000, 10, 4),
(24, 5, N'Bạc', '256GB', 'ZF6-256-SLV', 28990000, 24990000, 7, 4),
(25, 5, N'Vàng', '256GB', 'ZF6-256-YLW', 28990000, 24990000, 6, 4),
(26, 5, N'Xanh Dương', '512GB', 'ZF6-512-BLU', 32490000, 28490000, 3, 4),
(27, 6, N'Xám Graphite', '128GB', 'A56-128-GRY', 11990000, 10990000, 30, 5),
(28, 6, N'Hồng', '128GB', 'A56-128-PNK', 11990000, 10990000, 24, 5),
(29, 6, N'Xám Graphite', '256GB', 'A56-256-GRY', 13490000, 12490000, 18, 5),
(30, 6, N'Xanh Olive', '256GB', 'A56-256-OLV', 13490000, 12490000, 14, 5),
(31, 7, N'Đen', '256GB', 'RN14P-256-BLK', 8990000, 8490000, 35, 8),
(32, 7, N'Xanh Dương', '256GB', 'RN14P-256-BLU', 8990000, 8490000, 28, 8),
(33, 7, N'Tím', '256GB', 'RN14P-256-PUR', 8990000, 8490000, 20, 8),
(34, 8, N'Cam Hổ Phách', '256GB', 'RN12F-256-ORG', 8990000, 8490000, 15, 5),
(35, 8, N'Xanh Olive', '256GB', 'RN12F-256-OLV', 8990000, 8490000, 12, 5),
(36, 9, N'Tím Lotus', '256GB', 'V40-256-PUR', 10990000, 10490000, 13, 5),
(37, 9, N'Xanh Ganges', '256GB', 'V40-256-BLU', 10990000, 10490000, 9, 5),
(38, 10, N'Trắng', NULL, 'APP2-USBC-WHT', 6190000, 5790000, 4, 5),
(39, 11, N'Bạc', NULL, 'BUDS3P-SLV', 5490000, 4790000, 18, 5),
(40, 11, N'Trắng', NULL, 'BUDS3P-WHT', 5490000, 4790000, 14, 5),
(41, 12, N'Đen', NULL, 'ANK-N30-BLK', 490000, 390000, 60, 10),
(42, 12, N'Trắng', NULL, 'ANK-N30-WHT', 490000, 390000, 45, 10),
(43, 13, N'Đen', NULL, 'BSS-C100-BLK', 250000, 190000, 80, 10),
(44, 13, N'Trắng', NULL, 'BSS-C100-WHT', 250000, 190000, 50, 10),
(45, 14, N'Đen', NULL, 'ANK-PB10K-BLK', 990000, 790000, 32, 8),
(46, 14, N'Xanh', NULL, 'ANK-PB10K-BLU', 990000, 790000, 6, 8),
(47, 15, N'Trong suốt', NULL, 'SPG-LA-IP16PM-CLR', 590000, 490000, 40, 10),
(48, 15, N'Đen', NULL, 'SPG-LA-IP16PM-BLK', 590000, 490000, 38, 10),
(49, 16, N'Trong suốt', NULL, 'SPG-UH-S25U-CLR', 550000, 450000, 35, 10),
(50, 16, N'Đen', NULL, 'SPG-UH-S25U-BLK', 550000, 450000, 28, 10),
(51, 17, N'Trong suốt', NULL, 'SPG-GL-IP16-CLR', 350000, 290000, 55, 10);
SET IDENTITY_INSERT BienTheSanPham OFF;
 
-- Chuyển RAM / dung lượng dạng chữ sang số để lọc nâng cao
UPDATE SanPham SET RamGB = TRY_CAST(REPLACE(RAM, 'GB', '') AS TINYINT);
UPDATE BienTheSanPham
SET DungLuongGB = CASE WHEN DungLuong LIKE '%TB' THEN TRY_CAST(REPLACE(DungLuong, 'TB', '') AS INT) * 1024
                       WHEN DungLuong LIKE '%GB' THEN TRY_CAST(REPLACE(DungLuong, 'GB', '') AS INT) END;
 
-- Hình ảnh (cần tự bỏ file ảnh tương ứng vào thư mục Content/images/products)
INSERT INTO HinhAnhSanPham (SanPhamId, DuongDan, LaAnhChinh, ThuTu)
SELECT Id, AnhDaiDien, 1, 1 FROM SanPham;
INSERT INTO HinhAnhSanPham (SanPhamId, DuongDan, LaAnhChinh, ThuTu) VALUES
(1, '/Content/images/products/iphone-16-pro-max-2.jpg', 0, 2),
(1, '/Content/images/products/iphone-16-pro-max-3.jpg', 0, 3),
(2, '/Content/images/products/iphone-16-2.jpg', 0, 2),
(4, '/Content/images/products/galaxy-s25-ultra-2.jpg', 0, 2),
(4, '/Content/images/products/galaxy-s25-ultra-3.jpg', 0, 3),
(5, '/Content/images/products/galaxy-z-flip6-2.jpg', 0, 2);
 
-- Sản phẩm liên quan: phụ kiện mua cùng + sản phẩm tương tự
INSERT INTO SanPhamLienQuan (SanPhamId, SanPhamLienQuanId, LoaiLienQuan) VALUES
(1,10,'PhuKien'),(1,12,'PhuKien'),(1,14,'PhuKien'),(1,15,'PhuKien'),
(2,10,'PhuKien'),(2,12,'PhuKien'),(2,17,'PhuKien'),
(3,12,'PhuKien'),(3,13,'PhuKien'),
(4,11,'PhuKien'),(4,12,'PhuKien'),(4,14,'PhuKien'),(4,16,'PhuKien'),
(5,11,'PhuKien'),(5,12,'PhuKien'),
(6,12,'PhuKien'),(6,13,'PhuKien'),(7,12,'PhuKien'),(7,13,'PhuKien'),
(8,12,'PhuKien'),(9,12,'PhuKien'),
(1,4,'TuongTu'),(1,2,'TuongTu'),(2,3,'TuongTu'),(2,1,'TuongTu'),(3,2,'TuongTu'),
(4,1,'TuongTu'),(4,5,'TuongTu'),(6,7,'TuongTu'),(7,6,'TuongTu'),(8,9,'TuongTu'),(9,8,'TuongTu');
 
INSERT INTO YeuThich (NguoiDungId, SanPhamId) VALUES (3,4),(3,1),(4,5),(5,9),(6,4),(7,2),(8,7);
 
-- Khuyến mãi (CHAOBAN10 giảm 10% tối đa 500K, mỗi người 1 lần; GIAM500K áp cho đơn từ 15 triệu, mỗi người 1 lần;
--             TETNGUYENDAN đã hết hạn)
SET IDENTITY_INSERT KhuyenMai ON;
INSERT INTO KhuyenMai (Id, MaCode, TenKhuyenMai, LoaiGiam, GiaTriGiam, GiamToiDa, DonToiThieu, NgayBatDau, NgayKetThuc, SoLuong, GioiHanMoiNguoi, DaDung, TrangThai) VALUES
(1, 'CHAOBAN10',    N'Chào bạn mới - giảm 10%',                  'PHANTRAM', 10,     500000, 1000000,  '2026-01-01', '2026-12-31 23:59:59', 500,  1,    2,   1),
(2, 'GIAM500K',     N'Giảm 500.000đ cho đơn từ 15 triệu',        'SOTIEN',   500000, NULL,   15000000, '2026-09-01', '2026-12-31 23:59:59', 100,  1,    3,   1),
(3, 'PHUKIEN15',    N'Tháng 10 - giảm 15% tối đa 200.000đ',      'PHANTRAM', 15,     200000, 300000,   '2026-10-01', '2026-10-31 23:59:59', 200,  1,    0,   1),
(4, 'FREESHIP',     N'Hỗ trợ phí vận chuyển 30.000đ',            'SOTIEN',   30000,  NULL,   500000,   '2026-01-01', '2026-12-31 23:59:59', NULL, NULL, 2,   1),
(5, 'TETNGUYENDAN', N'Khuyến mãi Tết Nguyên Đán - giảm 8%',      'PHANTRAM', 8,      300000, 2000000,  '2026-02-01', '2026-02-28 23:59:59', 200,  1,    120, 0);
SET IDENTITY_INSERT KhuyenMai OFF;
 
-- Gói trả góp 0% (giá trị mẫu, tự chỉnh theo quy định của đề tài)
-- Tiền trả mỗi tháng = (Giá - Tiền trả trước) * (1 + PhiChuyenDoi%) / SoThang
INSERT INTO GoiTraGop (SoThang, TraTruocToiThieuPT, PhiChuyenDoiPT) VALUES (3, 0, 0), (6, 0, 0), (9, 20, 0), (12, 30, 0);
 
-- Giỏ hàng đang có sẵn của 2 khách (khách vãng lai dùng SessionToken, tạo từ ứng dụng)
INSERT INTO GioHang (NguoiDungId) VALUES (4), (6);
INSERT INTO GioHangChiTiet (GioHangId, BienTheId, SoLuong)
SELECT g.Id, x.BienTheId, x.SoLuong
FROM (VALUES (4,21,1),(4,50,1),(6,13,1),(6,42,2)) x(NguoiDungId, BienTheId, SoLuong)
JOIN GioHang g ON g.NguoiDungId = x.NguoiDungId;
 
-- Đơn hàng (17 đơn, từ tháng 8/2025 đến 10/2026). Tổng tiền hàng được tính lại từ chi tiết bên dưới
SET IDENTITY_INSERT DonHang ON;
INSERT INTO DonHang (Id, MaDonHang, NguoiDungId, TenNguoiNhan, SoDienThoai, Email, DiaChiGiao, TinhThanh, KhuyenMaiId, TongTienHang, PhiShip, TienGiam, PhuongThucThanhToan, TrangThaiThanhToan, TrangThai, NgayDat)
SELECT x.Id, x.Ma, x.UserId, u.HoTen, u.SoDienThoai, u.Email,
       dc.DiaChiChiTiet + N', ' + dc.PhuongXa + N', ' + dc.TinhThanh, dc.TinhThanh,
       x.KM, 0, pv.PhiShip, x.Giam, x.PT, x.TTTT, x.TT, x.Ngay
FROM (VALUES
 (1,  'DH2605121001', 3, NULL, 0,      'QR',  'DaThanhToan',  'HoanTat',  '2026-05-12 09:15:00'),
 (2,  'DH2605281002', 4, 1,    500000, 'COD', 'DaThanhToan',  'HoanTat',  '2026-05-28 20:40:00'),
 (3,  'DH2606091003', 5, NULL, 0,      'COD', 'DaThanhToan',  'HoanTat',  '2026-06-09 14:05:00'),
 (4,  'DH2606221004', 6, 1,    500000, 'QR',  'DaThanhToan',  'HoanTat',  '2026-06-22 11:30:00'),
 (5,  'DH2607081005', 7, NULL, 0,      'COD', 'DaThanhToan',  'HoanTat',  '2026-07-08 19:20:00'),
 (6,  'DH2607191006', 3, 4,    30000,  'QR',  'DaThanhToan',  'HoanTat',  '2026-07-19 10:10:00'),
 (7,  'DH2608051007', 8, NULL, 0,      'QR',  'DaThanhToan',  'HoanTat',  '2026-08-05 21:00:00'),
 (8,  'DH2608211008', 4, NULL, 0,      'QR',  'DaThanhToan',  'HoanTat',  '2026-08-21 13:45:00'),
 (9,  'DH2609031009', 5, NULL, 0,      'COD', 'DaThanhToan',  'HoanTat',  '2026-09-03 08:50:00'),
 (10, 'DH2609141010', 6, 2,    500000, 'QR',  'DaThanhToan',  'HoanTat',  '2026-09-14 16:25:00'),
 (11, 'DH2609271011', 7, 2,    500000, 'COD', 'DaThanhToan',  'HoanTat',  '2026-09-27 12:00:00'),
 (12, 'DH2610021012', 3, 4,    30000,  'QR',  'DaThanhToan',  'DangGiao', '2026-10-02 15:30:00'),
 (13, 'DH2610061013', 4, 2,    500000, 'QR',  'DaThanhToan',  'DaXacNhan','2026-10-06 09:40:00'),
 (14, 'DH2610091014', 5, NULL, 0,      'COD', 'ChuaThanhToan','ChoXacNhan','2026-10-09 18:15:00'),
 (15, 'DH2610101015', 6, NULL, 0,      'QR',  'ChuaThanhToan','ChoXacNhan','2026-10-10 10:45:00'),
 (16, 'DH2610041016', 8, NULL, 0,      'QR',  'ChuaThanhToan','DaHuy',    '2026-10-04 22:10:00'),
 (17, 'DH2508151017', 5, NULL, 0,      'COD', 'DaThanhToan',  'HoanTat',  '2025-08-15 10:00:00')
) x(Id, Ma, UserId, KM, Giam, PT, TTTT, TT, Ngay)
JOIN NguoiDung u ON u.Id = x.UserId
JOIN DiaChiGiaoHang dc ON dc.NguoiDungId = x.UserId AND dc.MacDinh = 1
JOIN PhiVanChuyen pv ON pv.TinhThanh = dc.TinhThanh;
SET IDENTITY_INSERT DonHang OFF;
 
-- Chi tiết đơn: tên và giá lấy từ biến thể (DonHangId, BienTheId, SoLuong)
INSERT INTO ChiTietDonHang (DonHangId, BienTheId, TenSanPham, SoLuong, DonGia)
SELECT x.DonHangId, x.BienTheId,
       sp.TenSanPham + ISNULL(N' ' + b.DungLuong, N'') + N' - ' + b.MauSac,
       x.SoLuong, b.GiaBan
FROM (VALUES
 (1,15,1),(1,41,1),(2,18,1),(3,31,1),(3,43,1),(4,8,1),(4,38,1),(5,28,1),(5,45,1),
 (6,39,1),(7,23,1),(8,1,1),(8,47,1),(9,34,1),(10,22,1),(10,49,1),(11,11,1),(11,42,1),
 (12,36,1),(13,4,1),(13,48,1),(14,30,1),(15,17,1),(15,41,1),(16,12,1),(17,14,1)
) x(DonHangId, BienTheId, SoLuong)
JOIN BienTheSanPham b ON b.Id = x.BienTheId
JOIN SanPham sp ON sp.Id = b.SanPhamId;
 
UPDATE d SET d.TongTienHang = s.Tong
FROM DonHang d
JOIN (SELECT DonHangId, SUM(ThanhTien) AS Tong FROM ChiTietDonHang GROUP BY DonHangId) s ON s.DonHangId = d.Id;
 
UPDATE DonHang SET NgayGiaoXong = DATEADD(DAY, 2, NgayDat) WHERE TrangThai = 'HoanTat';
UPDATE DonHang
SET NgayThanhToan = CASE WHEN PhuongThucThanhToan = 'QR' THEN DATEADD(MINUTE, 20, NgayDat) ELSE NgayGiaoXong END
WHERE TrangThaiThanhToan = 'DaThanhToan';
 
-- Lượt dùng mã khuyến mãi của từng khách (không tính đơn đã hủy)
INSERT INTO KhuyenMaiSuDung (KhuyenMaiId, NguoiDungId, DonHangId, NgayDung)
SELECT KhuyenMaiId, NguoiDungId, Id, NgayDat
FROM DonHang WHERE KhuyenMaiId IS NOT NULL AND TrangThai <> 'DaHuy';
 
-- IMEI (số giả để demo tra cứu bảo hành; đơn 17 từ 2025 nên đã hết bảo hành)
INSERT INTO ImeiSanPham (Imei, ChiTietDonHangId, NgayBan, NgayHetBaoHanh)
SELECT x.Imei, c.Id, CAST(d.NgayGiaoXong AS DATE), DATEADD(MONTH, sp.BaoHanhThang, CAST(d.NgayGiaoXong AS DATE))
FROM (VALUES
 ('356938110245871', 1, 15), ('351745092368142', 2, 18), ('865320047781905', 3, 31),
 ('357823104459620', 4, 8),  ('862917053342218', 5, 28), ('352614087730459', 7, 23),
 ('356091125938744', 8, 1),  ('868254061177392', 9, 34), ('351276093804516', 10, 22),
 ('357402118695033', 11, 11),('359871046620287', 17, 14)
) x(Imei, DonHangId, BienTheId)
JOIN ChiTietDonHang c ON c.DonHangId = x.DonHangId AND c.BienTheId = x.BienTheId
JOIN DonHang d ON d.Id = c.DonHangId
JOIN BienTheSanPham b ON b.Id = c.BienTheId
JOIN SanPham sp ON sp.Id = b.SanPhamId;
 
-- Giao dịch thanh toán (QR có mã giao dịch ngân hàng, COD ghi nhận khi giao xong)
INSERT INTO ThanhToanGiaoDich (DonHangId, PhuongThuc, SoTien, MaGiaoDich, NoiDungCK, NgayThanhToan, NguoiXacNhanId)
SELECT d.Id, d.PhuongThucThanhToan, d.TongThanhToan,
       CASE WHEN d.PhuongThucThanhToan = 'QR' THEN 'FT26' + RIGHT('000000' + CAST(d.Id AS VARCHAR(6)), 6) END,
       CASE WHEN d.PhuongThucThanhToan = 'QR' THEN d.MaDonHang END,
       d.NgayThanhToan, 2
FROM DonHang d WHERE d.TrangThaiThanhToan = 'DaThanhToan';
 
-- Timeline trạng thái đơn hàng
INSERT INTO DonHangLichSu (DonHangId, TrangThai, GhiChu, NguoiCapNhatId, NgayCapNhat)
SELECT d.Id, s.TrangThai, s.GhiChu, CASE WHEN s.Thu = 1 THEN d.NguoiDungId ELSE 2 END, DATEADD(MINUTE, s.Phut, d.NgayDat)
FROM DonHang d
JOIN (VALUES (1, 'ChoXacNhan', N'Khách đặt hàng thành công', 0),
             (2, 'DaXacNhan',  N'Cửa hàng đã xác nhận đơn', 30),
             (3, 'DangGiao',   N'Đã bàn giao cho đơn vị vận chuyển', 360),
             (4, 'HoanTat',    N'Giao hàng thành công', 2880)) s(Thu, TrangThai, GhiChu, Phut)
  ON s.Thu <= CASE d.TrangThai WHEN 'ChoXacNhan' THEN 1 WHEN 'DaXacNhan' THEN 2
                               WHEN 'DangGiao' THEN 3 WHEN 'HoanTat' THEN 4 ELSE 1 END;
INSERT INTO DonHangLichSu (DonHangId, TrangThai, GhiChu, NguoiCapNhatId, NgayCapNhat)
SELECT d.Id, 'DaHuy', N'Khách hủy đơn do chưa thanh toán', d.NguoiDungId, DATEADD(HOUR, 3, d.NgayDat)
FROM DonHang d WHERE d.TrangThai = 'DaHuy';
 
-- Lịch sử tồn kho: bán hàng, hoàn kho do hủy, rồi dòng "nhập kho ban đầu" để tổng lịch sử khớp đúng SoLuongTon hiện tại
INSERT INTO LichSuTonKho (BienTheId, SoLuongThayDoi, LyDo, DonHangId, NgayTao)
SELECT ct.BienTheId, -ct.SoLuong, N'Bán hàng - đơn ' + d.MaDonHang, d.Id, d.NgayDat
FROM ChiTietDonHang ct JOIN DonHang d ON d.Id = ct.DonHangId;
INSERT INTO LichSuTonKho (BienTheId, SoLuongThayDoi, LyDo, DonHangId, NgayTao)
SELECT ct.BienTheId, ct.SoLuong, N'Hoàn kho do hủy đơn ' + d.MaDonHang, d.Id, DATEADD(HOUR, 3, d.NgayDat)
FROM ChiTietDonHang ct JOIN DonHang d ON d.Id = ct.DonHangId WHERE d.TrangThai = 'DaHuy';
INSERT INTO LichSuTonKho (BienTheId, SoLuongThayDoi, LyDo, DonHangId, NgayTao)
SELECT bt.Id, bt.SoLuongTon - ISNULL(h.Tong, 0), N'Nhập kho ban đầu', NULL, '2025-07-01'
FROM BienTheSanPham bt
LEFT JOIN (SELECT BienTheId, SUM(SoLuongThayDoi) AS Tong FROM LichSuTonKho GROUP BY BienTheId) h ON h.BienTheId = bt.Id
WHERE bt.SoLuongTon - ISNULL(h.Tong, 0) <> 0;
 
-- Đánh giá (chỉ từ khách đã mua và nhận hàng)
INSERT INTO DanhGia (SanPhamId, NguoiDungId, SoSao, NoiDung, NgayDanhGia) VALUES
(3, 3, 5, N'Máy chạy mượt, pin trâu, giao hàng nhanh trong 2 ngày. Shop đóng gói cẩn thận.', '2026-05-20'),
(4, 4, 5, N'Camera 200MP chụp rất đẹp, màn hình sáng rõ ngoài nắng. Máy hơi nặng nhưng đáng tiền.', '2026-06-05'),
(7, 5, 4, N'Giá tầm trung mà màn hình AMOLED đẹp, pin dùng cả ngày. Hơi nóng khi chơi game nặng.', '2026-06-15'),
(2, 6, 5, N'Chụp ảnh đẹp, iOS mượt. Nút Camera Control lúc đầu hơi lạ tay.', '2026-06-30'),
(10, 6, 5, N'Chống ồn chủ động rất tốt, đeo thoải mái cả buổi.', '2026-07-02'),
(6, 7, 4, N'Màn hình đẹp, pin tốt. Sạc hơi chậm so với đối thủ cùng tầm giá.', '2026-07-16'),
(11, 3, 4, N'Chống ồn tốt, kết nối nhanh với điện thoại Samsung. Dùng với iPhone thì thiếu vài tính năng.', '2026-07-26'),
(5, 8, 5, N'Gập gọn bỏ túi, màn hình ngoài xem thông báo rất tiện. Nếp gấp gần như không thấy.', '2026-08-12'),
(1, 4, 5, N'Khung Titan sang trọng, camera zoom 5x chụp xa rất nét.', '2026-08-30'),
(15, 4, 5, N'Ốp ôm máy chắc, không ố vàng, bấm phím mượt.', '2026-09-01'),
(8, 5, 4, N'Thiết kế đẹp, sạc nhanh, camera selfie 32MP ổn. Có vài ứng dụng cài sẵn.', '2026-09-10'),
(4, 6, 5, N'Dùng S Pen và Galaxy AI rất tiện cho công việc.', '2026-09-20'),
(2, 7, 4, N'Máy đẹp, giá ổn sau khi dùng mã giảm giá. Sạc hơi chậm.', '2026-10-05');
 
-- Cấu hình (đổi số tài khoản thành của bạn để quét QR thử)
INSERT INTO CauHinhHeThong (Khoa, GiaTri, MoTa) VALUES
('TenCuaHang', N'PhoneStore', N'Tên hiển thị trên website và email'),
('Hotline', N'1900 1234', N'Số hotline hiển thị ở footer'),
('NganHang_Ma', N'VCB', N'Mã ngân hàng dùng để tạo QR VietQR'),
('NganHang_SoTaiKhoan', N'0123456789', N'Số tài khoản nhận tiền'),
('NganHang_TenChuTK', N'CUA HANG PHONESTORE', N'Tên chủ tài khoản (không dấu)'),
('BaoHanh_MacDinh_Thang', N'12', N'Số tháng bảo hành mặc định'),
('QR_HetHan_Phut', N'30', N'Số phút chờ thanh toán QR trước khi tự hủy đơn');
 
-- Nhật ký email xác nhận đơn
INSERT INTO NhatKyEmail (DonHangId, EmailNhan, TieuDe, TrangThai, NgayGui)
SELECT d.Id, d.Email, N'[PhoneStore] Xác nhận đơn hàng ' + d.MaDonHang, 'ThanhCong', DATEADD(SECOND, 5, d.NgayDat)
FROM DonHang d;
GO
-- ============ PHẦN 3: VIEW, FUNCTION, PROCEDURE, TRIGGER ============
USE QL_DienThoai;
GO
 
-- Danh sách sản phẩm: giá thấp nhất/cao nhất, tồn, sao trung bình, số lượng đã bán, lượt xem, RAM (số)
CREATE VIEW vw_SanPhamHienThi AS
SELECT sp.Id, sp.TenSanPham, sp.LoaiId, l.TenLoai, sp.ThuongHieuId, th.TenThuongHieu,
       sp.AnhDaiDien, sp.NoiBat, sp.NgayTao, sp.LuotXem, sp.RamGB,
       p.GiaThapNhat, p.GiaCaoNhat, p.TongTon,
       ISNULL(r.SaoTrungBinh, 0) AS SaoTrungBinh, ISNULL(r.SoDanhGia, 0) AS SoDanhGia,
       ISNULL(b.DaBan, 0) AS DaBan
FROM SanPham sp
JOIN LoaiSanPham l ON l.Id = sp.LoaiId
JOIN ThuongHieu th ON th.Id = sp.ThuongHieuId
CROSS APPLY (SELECT MIN(GiaBan) AS GiaThapNhat, MAX(GiaBan) AS GiaCaoNhat, SUM(SoLuongTon) AS TongTon
             FROM BienTheSanPham WHERE SanPhamId = sp.Id AND HienThi = 1) p
OUTER APPLY (SELECT CAST(AVG(CAST(SoSao AS DECIMAL(3,1))) AS DECIMAL(3,1)) AS SaoTrungBinh, COUNT(*) AS SoDanhGia
             FROM DanhGia WHERE SanPhamId = sp.Id AND DaDuyet = 1) r
OUTER APPLY (SELECT SUM(ct.SoLuong) AS DaBan
             FROM ChiTietDonHang ct
             JOIN DonHang d ON d.Id = ct.DonHangId
             JOIN BienTheSanPham bt ON bt.Id = ct.BienTheId
             WHERE bt.SanPhamId = sp.Id AND d.TrangThai = 'HoanTat') b
WHERE sp.HienThi = 1;
GO
 
-- Cảnh báo sắp hết hàng
CREATE VIEW vw_CanhBaoTonKho AS
SELECT bt.Id AS BienTheId, sp.Id AS SanPhamId, sp.TenSanPham, bt.MauSac, bt.DungLuong,
       bt.SKU, bt.SoLuongTon, bt.NguongCanhBao
FROM BienTheSanPham bt
JOIN SanPham sp ON sp.Id = bt.SanPhamId
WHERE bt.SoLuongTon <= bt.NguongCanhBao AND bt.HienThi = 1 AND sp.HienThi = 1;
GO
 
-- Top bán chạy (dùng: SELECT TOP 5 * FROM vw_TopBanChay ORDER BY SoLuongBan DESC)
CREATE VIEW vw_TopBanChay AS
SELECT sp.Id, sp.TenSanPham, SUM(ct.SoLuong) AS SoLuongBan, SUM(ct.ThanhTien) AS DoanhThu
FROM ChiTietDonHang ct
JOIN DonHang d ON d.Id = ct.DonHangId
JOIN BienTheSanPham bt ON bt.Id = ct.BienTheId
JOIN SanPham sp ON sp.Id = bt.SanPhamId
WHERE d.TrangThai = 'HoanTat'
GROUP BY sp.Id, sp.TenSanPham;
GO
 
-- Kiểm tra quyền đánh giá: chỉ khách đã nhận hàng mới được đánh giá sản phẩm
CREATE VIEW vw_NguoiDaMuaSanPham AS
SELECT DISTINCT d.NguoiDungId, bt.SanPhamId
FROM DonHang d
JOIN ChiTietDonHang ct ON ct.DonHangId = d.Id
JOIN BienTheSanPham bt ON bt.Id = ct.BienTheId
WHERE d.TrangThai = 'HoanTat';
GO
 
-- Tính tiền giảm của mã khuyến mãi (trả về 0 nếu mã không hợp lệ). Giới hạn số lần mỗi người được kiểm tra trong sp_DatHang
CREATE FUNCTION dbo.fn_TinhTienGiam (@MaCode VARCHAR(30), @TongTien DECIMAL(18,0))
RETURNS DECIMAL(18,0)
AS
BEGIN
    DECLARE @Loai VARCHAR(10), @GiaTri DECIMAL(18,2), @ToiDa DECIMAL(18,0), @Giam DECIMAL(18,0) = 0;
 
    SELECT @Loai = LoaiGiam, @GiaTri = GiaTriGiam, @ToiDa = GiamToiDa
    FROM KhuyenMai
    WHERE MaCode = @MaCode AND TrangThai = 1
      AND SYSDATETIME() BETWEEN NgayBatDau AND NgayKetThuc
      AND @TongTien >= DonToiThieu
      AND (SoLuong IS NULL OR DaDung < SoLuong);
 
    IF @Loai IS NULL RETURN 0;
 
    IF @Loai = 'PHANTRAM'
    BEGIN
        SET @Giam = ROUND(@TongTien * @GiaTri / 100, 0);
        IF @ToiDa IS NOT NULL AND @Giam > @ToiDa SET @Giam = @ToiDa;
    END
    ELSE
        SET @Giam = @GiaTri;
 
    IF @Giam > @TongTien SET @Giam = @TongTien;
    RETURN @Giam;
END
GO
 
-- Đặt hàng từ giỏ hàng: trừ tồn kho an toàn, áp mã (kiểm tra giới hạn mỗi người), tạo đơn, chi tiết, timeline
CREATE PROCEDURE sp_DatHang
    @NguoiDungId INT,
    @TenNguoiNhan NVARCHAR(100),
    @SoDienThoai VARCHAR(15),
    @Email NVARCHAR(150),
    @DiaChiGiao NVARCHAR(300),
    @TinhThanh NVARCHAR(50),
    @PhuongThucThanhToan VARCHAR(10),          -- 'COD' hoặc 'QR'
    @MaKhuyenMai VARCHAR(30) = NULL,
    @GhiChu NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
 
    DECLARE @GioHangId INT, @SoDong INT, @TongTien DECIMAL(18,0), @PhiShip DECIMAL(18,0),
            @TienGiam DECIMAL(18,0) = 0, @KhuyenMaiId INT = NULL, @GioiHan INT,
            @DonHangId INT, @MaDonHang VARCHAR(20), @So INT;
 
    IF NOT EXISTS (SELECT 1 FROM NguoiDung WHERE Id = @NguoiDungId AND TrangThai = 1)
        THROW 50005, N'Tài khoản không tồn tại hoặc đã bị khóa', 1;
 
    SELECT @GioHangId = Id FROM GioHang WHERE NguoiDungId = @NguoiDungId;
    SELECT @SoDong = COUNT(*) FROM GioHangChiTiet WHERE GioHangId = @GioHangId;
    IF @SoDong = 0 THROW 50001, N'Giỏ hàng trống', 1;
 
    BEGIN TRAN;
 
    -- Trừ tồn kho trong 1 câu lệnh; nếu có dòng không đủ hàng (hoặc sản phẩm đã ẩn) thì hủy toàn bộ
    UPDATE bt SET bt.SoLuongTon = bt.SoLuongTon - g.SoLuong
    FROM BienTheSanPham bt
    JOIN SanPham sp ON sp.Id = bt.SanPhamId
    JOIN GioHangChiTiet g ON g.BienTheId = bt.Id
    WHERE g.GioHangId = @GioHangId AND bt.HienThi = 1 AND sp.HienThi = 1 AND bt.SoLuongTon >= g.SoLuong;
    IF @@ROWCOUNT <> @SoDong THROW 50002, N'Có sản phẩm hết hàng, đã ngừng bán hoặc không đủ số lượng', 1;
 
    SELECT @TongTien = SUM(g.SoLuong * bt.GiaBan)
    FROM GioHangChiTiet g JOIN BienTheSanPham bt ON bt.Id = g.BienTheId
    WHERE g.GioHangId = @GioHangId;
 
    SELECT @PhiShip = PhiShip FROM PhiVanChuyen WHERE TinhThanh = @TinhThanh;
    IF @PhiShip IS NULL SELECT @PhiShip = PhiShip FROM PhiVanChuyen WHERE TinhThanh = N'Khác';
 
    IF @MaKhuyenMai IS NOT NULL AND @MaKhuyenMai <> ''
    BEGIN
        SET @TienGiam = dbo.fn_TinhTienGiam(@MaKhuyenMai, @TongTien);
        IF @TienGiam = 0 THROW 50003, N'Mã khuyến mãi không hợp lệ hoặc không đủ điều kiện', 1;
 
        SELECT @GioiHan = GioiHanMoiNguoi FROM KhuyenMai WHERE MaCode = @MaKhuyenMai;
        IF @GioiHan IS NOT NULL
           AND (SELECT COUNT(*) FROM KhuyenMaiSuDung su
                JOIN KhuyenMai k ON k.Id = su.KhuyenMaiId
                WHERE k.MaCode = @MaKhuyenMai AND su.NguoiDungId = @NguoiDungId) >= @GioiHan
            THROW 50006, N'Bạn đã dùng hết lượt của mã khuyến mãi này', 1;
 
        UPDATE KhuyenMai SET DaDung = DaDung + 1, @KhuyenMaiId = Id
        WHERE MaCode = @MaKhuyenMai AND (SoLuong IS NULL OR DaDung < SoLuong);
        IF @@ROWCOUNT = 0 THROW 50004, N'Mã khuyến mãi đã hết lượt sử dụng', 1;
    END
 
    SET @So = NEXT VALUE FOR dbo.seq_MaDonHang;
    SET @MaDonHang = 'DH' + FORMAT(SYSDATETIME(), 'yyMMdd') + CAST(@So AS VARCHAR(10));
 
    INSERT INTO DonHang (MaDonHang, NguoiDungId, TenNguoiNhan, SoDienThoai, Email, DiaChiGiao, TinhThanh,
                         GhiChu, KhuyenMaiId, TongTienHang, PhiShip, TienGiam, PhuongThucThanhToan)
    VALUES (@MaDonHang, @NguoiDungId, @TenNguoiNhan, @SoDienThoai, @Email, @DiaChiGiao, @TinhThanh,
            @GhiChu, @KhuyenMaiId, @TongTien, @PhiShip, @TienGiam, @PhuongThucThanhToan);
    SET @DonHangId = SCOPE_IDENTITY();
 
    IF @KhuyenMaiId IS NOT NULL
        INSERT INTO KhuyenMaiSuDung (KhuyenMaiId, NguoiDungId, DonHangId) VALUES (@KhuyenMaiId, @NguoiDungId, @DonHangId);
 
    INSERT INTO ChiTietDonHang (DonHangId, BienTheId, TenSanPham, SoLuong, DonGia)
    SELECT @DonHangId, bt.Id,
           sp.TenSanPham + ISNULL(N' ' + bt.DungLuong, N'') + N' - ' + bt.MauSac,
           g.SoLuong, bt.GiaBan
    FROM GioHangChiTiet g
    JOIN BienTheSanPham bt ON bt.Id = g.BienTheId
    JOIN SanPham sp ON sp.Id = bt.SanPhamId
    WHERE g.GioHangId = @GioHangId;
 
    INSERT INTO LichSuTonKho (BienTheId, SoLuongThayDoi, LyDo, DonHangId)
    SELECT g.BienTheId, -g.SoLuong, N'Bán hàng - đơn ' + @MaDonHang, @DonHangId
    FROM GioHangChiTiet g WHERE g.GioHangId = @GioHangId;
 
    INSERT INTO DonHangLichSu (DonHangId, TrangThai, GhiChu, NguoiCapNhatId)
    VALUES (@DonHangId, 'ChoXacNhan', N'Khách đặt hàng thành công', @NguoiDungId);
 
    DELETE FROM GioHangChiTiet WHERE GioHangId = @GioHangId;
    UPDATE GioHang SET NgayCapNhat = SYSDATETIME() WHERE Id = @GioHangId;
 
    COMMIT;
 
    SELECT @DonHangId AS DonHangId, @MaDonHang AS MaDonHang,
           @TongTien + @PhiShip - @TienGiam AS TongThanhToan;
END
GO
 
-- Gộp giỏ hàng khách vãng lai (SessionToken) vào giỏ của tài khoản ngay sau khi đăng nhập
CREATE PROCEDURE sp_GopGioHang @SessionToken VARCHAR(64), @NguoiDungId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @GuestId INT, @UserId INT;
 
    SELECT @GuestId = Id FROM GioHang WHERE SessionToken = @SessionToken AND NguoiDungId IS NULL;
    IF @GuestId IS NULL RETURN;
 
    BEGIN TRAN;
    SELECT @UserId = Id FROM GioHang WHERE NguoiDungId = @NguoiDungId;
    IF @UserId IS NULL
    BEGIN
        INSERT INTO GioHang (NguoiDungId) VALUES (@NguoiDungId);
        SET @UserId = SCOPE_IDENTITY();
    END
 
    -- trùng biến thể thì cộng dồn số lượng
    UPDATE t SET t.SoLuong = t.SoLuong + s.SoLuong
    FROM GioHangChiTiet t
    JOIN GioHangChiTiet s ON s.BienTheId = t.BienTheId
    WHERE t.GioHangId = @UserId AND s.GioHangId = @GuestId;
 
    INSERT INTO GioHangChiTiet (GioHangId, BienTheId, SoLuong)
    SELECT @UserId, s.BienTheId, s.SoLuong
    FROM GioHangChiTiet s
    WHERE s.GioHangId = @GuestId
      AND NOT EXISTS (SELECT 1 FROM GioHangChiTiet t WHERE t.GioHangId = @UserId AND t.BienTheId = s.BienTheId);
 
    DELETE FROM GioHang WHERE Id = @GuestId;   -- chi tiết giỏ khách tự xóa theo (ON DELETE CASCADE)
    UPDATE GioHang SET NgayCapNhat = SYSDATETIME() WHERE Id = @UserId;
    COMMIT;
END
GO
 
-- Admin bấm "Đã nhận tiền" cho đơn chuyển khoản QR
CREATE PROCEDURE sp_XacNhanThanhToan
    @DonHangId INT,
    @NguoiXacNhanId INT,
    @MaGiaoDich VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @TT VARCHAR(20), @TrangThai VARCHAR(20), @PT VARCHAR(10), @Tien DECIMAL(18,0), @Ma VARCHAR(20);
 
    BEGIN TRAN;
    SELECT @TT = TrangThaiThanhToan, @TrangThai = TrangThai, @PT = PhuongThucThanhToan,
           @Tien = TongThanhToan, @Ma = MaDonHang
    FROM DonHang WITH (UPDLOCK, ROWLOCK) WHERE Id = @DonHangId;
 
    IF @TT IS NULL THROW 50030, N'Không tìm thấy đơn hàng', 1;
    IF @PT <> 'QR' THROW 50032, N'Chỉ xác nhận thanh toán thủ công cho đơn chuyển khoản QR', 1;
    IF @TT <> 'ChuaThanhToan' OR @TrangThai = 'DaHuy'
        THROW 50031, N'Đơn hàng không ở trạng thái chờ thanh toán', 1;
 
    UPDATE DonHang SET TrangThaiThanhToan = 'DaThanhToan', NgayThanhToan = SYSDATETIME() WHERE Id = @DonHangId;
    INSERT INTO ThanhToanGiaoDich (DonHangId, PhuongThuc, SoTien, MaGiaoDich, NoiDungCK, NguoiXacNhanId)
    VALUES (@DonHangId, @PT, @Tien, @MaGiaoDich, @Ma, @NguoiXacNhanId);
    COMMIT;
END
GO
 
-- Đổi trạng thái đơn theo đúng quy trình, hủy đơn thì hoàn kho + hoàn lượt mã, giao xong thì tính bảo hành IMEI
CREATE PROCEDURE sp_CapNhatTrangThaiDon
    @DonHangId INT,
    @TrangThaiMoi VARCHAR(20),
    @NguoiCapNhatId INT,
    @GhiChu NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Cu VARCHAR(20), @PT VARCHAR(10), @TT VARCHAR(20), @KM INT, @Ma VARCHAR(20);
 
    BEGIN TRAN;
    SELECT @Cu = TrangThai, @PT = PhuongThucThanhToan, @TT = TrangThaiThanhToan,
           @KM = KhuyenMaiId, @Ma = MaDonHang
    FROM DonHang WITH (UPDLOCK, ROWLOCK) WHERE Id = @DonHangId;
 
    IF @Cu IS NULL THROW 50010, N'Không tìm thấy đơn hàng', 1;
    IF NOT ( (@Cu = 'ChoXacNhan' AND @TrangThaiMoi IN ('DaXacNhan','DaHuy'))
          OR (@Cu = 'DaXacNhan'  AND @TrangThaiMoi IN ('DangGiao','DaHuy'))
          OR (@Cu = 'DangGiao'   AND @TrangThaiMoi = 'HoanTat') )
        THROW 50011, N'Không thể chuyển sang trạng thái này', 1;
 
    IF @TrangThaiMoi = 'DaHuy'
    BEGIN
        UPDATE bt SET bt.SoLuongTon = bt.SoLuongTon + ct.SoLuong
        FROM BienTheSanPham bt JOIN ChiTietDonHang ct ON ct.BienTheId = bt.Id
        WHERE ct.DonHangId = @DonHangId;
 
        INSERT INTO LichSuTonKho (BienTheId, SoLuongThayDoi, LyDo, DonHangId)
        SELECT ct.BienTheId, ct.SoLuong, N'Hoàn kho do hủy đơn ' + @Ma, @DonHangId
        FROM ChiTietDonHang ct WHERE ct.DonHangId = @DonHangId;
 
        IF @KM IS NOT NULL
        BEGIN
            UPDATE KhuyenMai SET DaDung = DaDung - 1 WHERE Id = @KM;
            DELETE FROM KhuyenMaiSuDung WHERE DonHangId = @DonHangId;   -- trả lại lượt dùng cho khách
        END
 
        UPDATE DonHang
        SET TrangThai = 'DaHuy',
            TrangThaiThanhToan = CASE WHEN @TT = 'DaThanhToan' THEN 'HoanTien' ELSE @TT END
        WHERE Id = @DonHangId;
    END
    ELSE IF @TrangThaiMoi = 'HoanTat'
    BEGIN
        UPDATE DonHang
        SET TrangThai = 'HoanTat', NgayGiaoXong = SYSDATETIME(),
            TrangThaiThanhToan = CASE WHEN @PT = 'COD' THEN 'DaThanhToan' ELSE TrangThaiThanhToan END,
            NgayThanhToan = CASE WHEN @PT = 'COD' AND NgayThanhToan IS NULL THEN SYSDATETIME() ELSE NgayThanhToan END
        WHERE Id = @DonHangId;
 
        IF @PT = 'COD' AND @TT <> 'DaThanhToan'
            INSERT INTO ThanhToanGiaoDich (DonHangId, PhuongThuc, SoTien, NguoiXacNhanId)
            SELECT Id, 'COD', TongThanhToan, @NguoiCapNhatId FROM DonHang WHERE Id = @DonHangId;
 
        -- Bảo hành tính từ ngày giao xong (IMEI có thể đã được gán từ trước khi giao)
        UPDATE i
        SET i.NgayBan = CAST(SYSDATETIME() AS DATE),
            i.NgayHetBaoHanh = DATEADD(MONTH, sp.BaoHanhThang, CAST(SYSDATETIME() AS DATE))
        FROM ImeiSanPham i
        JOIN ChiTietDonHang ct ON ct.Id = i.ChiTietDonHangId
        JOIN BienTheSanPham bt ON bt.Id = ct.BienTheId
        JOIN SanPham sp ON sp.Id = bt.SanPhamId
        WHERE ct.DonHangId = @DonHangId;
    END
    ELSE
        UPDATE DonHang SET TrangThai = @TrangThaiMoi WHERE Id = @DonHangId;
 
    INSERT INTO DonHangLichSu (DonHangId, TrangThai, GhiChu, NguoiCapNhatId)
    VALUES (@DonHangId, @TrangThaiMoi, @GhiChu, @NguoiCapNhatId);
    COMMIT;
END
GO
 
-- Tự hủy đơn QR quá hạn thanh toán và trả lại tồn kho.
-- Cần gọi định kỳ (mỗi 5 phút) bằng SQL Server Agent, Hangfire hoặc Timer trong ứng dụng.
CREATE PROCEDURE sp_HuyDonQRQuaHan @Phut INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Id INT;
 
    IF @Phut IS NULL SELECT @Phut = TRY_CAST(GiaTri AS INT) FROM CauHinhHeThong WHERE Khoa = 'QR_HetHan_Phut';
    SET @Phut = ISNULL(@Phut, 30);
 
    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT Id FROM DonHang
        WHERE PhuongThucThanhToan = 'QR' AND TrangThaiThanhToan = 'ChuaThanhToan'
          AND TrangThai = 'ChoXacNhan' AND NgayDat < DATEADD(MINUTE, -@Phut, SYSDATETIME());
    OPEN cur;
    FETCH NEXT FROM cur INTO @Id;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            EXEC sp_CapNhatTrangThaiDon @Id, 'DaHuy', 1, N'Tự động hủy do quá hạn thanh toán QR';
        END TRY
        BEGIN CATCH
            -- bỏ qua đơn lỗi (ví dụ admin vừa đổi trạng thái), lần chạy sau xử lý tiếp
        END CATCH
        FETCH NEXT FROM cur INTO @Id;
    END
    CLOSE cur;
    DEALLOCATE cur;
END
GO
 
-- Nhân viên gán IMEI cho dòng đơn hàng (chỉ điện thoại). Bảo hành tính từ ngày giao; chưa giao thì tạm tính từ hôm nay
CREATE PROCEDURE sp_GanImei
    @ChiTietDonHangId INT,
    @Imei CHAR(15)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @SoLuong INT, @BaoHanh INT, @TrangThai VARCHAR(20), @LoaiId INT, @NgayGiao DATETIME2, @Ngay DATE;
 
    SELECT @SoLuong = ct.SoLuong, @BaoHanh = sp.BaoHanhThang, @TrangThai = d.TrangThai,
           @LoaiId = sp.LoaiId, @NgayGiao = d.NgayGiaoXong
    FROM ChiTietDonHang ct
    JOIN DonHang d ON d.Id = ct.DonHangId
    JOIN BienTheSanPham bt ON bt.Id = ct.BienTheId
    JOIN SanPham sp ON sp.Id = bt.SanPhamId
    WHERE ct.Id = @ChiTietDonHangId;
 
    IF @SoLuong IS NULL THROW 50020, N'Không tìm thấy chi tiết đơn hàng', 1;
    IF @LoaiId <> 1 THROW 50023, N'Chỉ gán IMEI cho điện thoại', 1;
    IF @TrangThai NOT IN ('DaXacNhan','DangGiao','HoanTat') THROW 50021, N'Đơn hàng chưa được xác nhận', 1;
 
    SET @Ngay = ISNULL(CAST(@NgayGiao AS DATE), CAST(SYSDATETIME() AS DATE));
 
    BEGIN TRAN;
    IF (SELECT COUNT(*) FROM ImeiSanPham WITH (UPDLOCK, HOLDLOCK) WHERE ChiTietDonHangId = @ChiTietDonHangId) >= @SoLuong
        THROW 50022, N'Đã gán đủ IMEI cho dòng này', 1;
 
    INSERT INTO ImeiSanPham (Imei, ChiTietDonHangId, NgayBan, NgayHetBaoHanh)
    VALUES (@Imei, @ChiTietDonHangId, @Ngay, DATEADD(MONTH, @BaoHanh, @Ngay));
    COMMIT;
END
GO
 
-- Khách tra cứu bảo hành bằng IMEI (không trả thông tin cá nhân)
CREATE PROCEDURE sp_TraCuuBaoHanh @Imei CHAR(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hom DATE = CAST(SYSDATETIME() AS DATE);
    SELECT i.Imei, ct.TenSanPham, d.MaDonHang, i.NgayBan, i.NgayHetBaoHanh,
           CASE WHEN i.NgayHetBaoHanh >= @Hom THEN N'Còn bảo hành' ELSE N'Hết bảo hành' END AS TinhTrang,
           CASE WHEN i.NgayHetBaoHanh >= @Hom THEN DATEDIFF(DAY, @Hom, i.NgayHetBaoHanh) ELSE 0 END AS SoNgayConLai
    FROM ImeiSanPham i
    JOIN ChiTietDonHang ct ON ct.Id = i.ChiTietDonHangId
    JOIN DonHang d ON d.Id = ct.DonHangId
    WHERE i.Imei = @Imei;
END
GO
 
-- Doanh thu theo tháng cho biểu đồ (đủ 12 tháng, tháng không có đơn trả về 0)
-- DoanhThu = tiền hàng sau giảm giá (không tính phí ship); TongThu = số tiền thực thu gồm cả phí ship
CREATE PROCEDURE sp_DoanhThuTheoThang @Nam INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.Thang,
           ISNULL(SUM(d.TongTienHang - d.TienGiam), 0) AS DoanhThu,
           ISNULL(SUM(d.TongThanhToan), 0) AS TongThu,
           COUNT(d.Id) AS SoDon
    FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) t(Thang)
    LEFT JOIN DonHang d ON MONTH(d.NgayGiaoXong) = t.Thang AND YEAR(d.NgayGiaoXong) = @Nam
                       AND d.TrangThai = 'HoanTat'
    GROUP BY t.Thang
    ORDER BY t.Thang;
END
GO
 
-- Gợi ý tìm kiếm khi gõ: không phân biệt hoa/thường và dấu, tên bắt đầu bằng từ khóa được ưu tiên
-- (chữ Đ/đ vẫn được coi là chữ riêng, khác D/d)
CREATE PROCEDURE sp_GoiYTimKiem @TuKhoa NVARCHAR(100), @Top INT = 8
AS
BEGIN
    SET NOCOUNT ON;
    SET @TuKhoa = LTRIM(RTRIM(@TuKhoa));
    IF @TuKhoa IS NULL OR LEN(@TuKhoa) = 0 RETURN;
    SET @TuKhoa = REPLACE(REPLACE(REPLACE(@TuKhoa, N'[', N'[[]'), N'%', N'[%]'), N'_', N'[_]');   -- escape ký tự đặc biệt của LIKE
 
    SELECT TOP (@Top) Id, TenSanPham, TenThuongHieu, AnhDaiDien, GiaThapNhat
    FROM vw_SanPhamHienThi
    WHERE TenSanPham COLLATE Latin1_General_100_CI_AI LIKE N'%' + @TuKhoa + N'%'
       OR TenThuongHieu COLLATE Latin1_General_100_CI_AI LIKE N'%' + @TuKhoa + N'%'
    ORDER BY CASE WHEN TenSanPham COLLATE Latin1_General_100_CI_AI LIKE @TuKhoa + N'%' THEN 0 ELSE 1 END,
             DaBan DESC, TenSanPham;
END
GO
 
-- Lọc nâng cao + sắp xếp + phân trang. Tham số nào để NULL thì bỏ qua.
-- @SapXep: 'gia_tang' | 'gia_giam' | 'banchay' | 'danhgia' | 'xemnhieu' | NULL (mới nhất)
CREATE PROCEDURE sp_LocSanPham
    @TuKhoa NVARCHAR(100) = NULL,
    @LoaiId INT = NULL,
    @ThuongHieuId INT = NULL,
    @GiaTu DECIMAL(18,0) = NULL,
    @GiaDen DECIMAL(18,0) = NULL,
    @RamGB TINYINT = NULL,
    @DungLuongGB INT = NULL,
    @ConHang BIT = 0,
    @SapXep VARCHAR(20) = NULL,
    @Trang INT = 1,
    @SoDong INT = 12
AS
BEGIN
    SET NOCOUNT ON;
    IF @Trang < 1 SET @Trang = 1;
    IF @SoDong < 1 SET @SoDong = 12;
    SET @TuKhoa = LTRIM(RTRIM(@TuKhoa));
    IF @TuKhoa = N'' SET @TuKhoa = NULL;
    IF @TuKhoa IS NOT NULL
        SET @TuKhoa = REPLACE(REPLACE(REPLACE(@TuKhoa, N'[', N'[[]'), N'%', N'[%]'), N'_', N'[_]');
 
    SELECT v.*, COUNT(*) OVER () AS TongSoDong
    FROM vw_SanPhamHienThi v
    WHERE (@LoaiId IS NULL OR v.LoaiId = @LoaiId)
      AND (@ThuongHieuId IS NULL OR v.ThuongHieuId = @ThuongHieuId)
      AND (@GiaTu IS NULL OR v.GiaCaoNhat >= @GiaTu)
      AND (@GiaDen IS NULL OR v.GiaThapNhat <= @GiaDen)
      AND (@RamGB IS NULL OR v.RamGB = @RamGB)
      AND (@DungLuongGB IS NULL OR EXISTS (SELECT 1 FROM BienTheSanPham b
                                           WHERE b.SanPhamId = v.Id AND b.HienThi = 1 AND b.DungLuongGB = @DungLuongGB))
      AND (@ConHang = 0 OR v.TongTon > 0)
      AND (@TuKhoa IS NULL OR v.TenSanPham COLLATE Latin1_General_100_CI_AI LIKE N'%' + @TuKhoa + N'%')
    ORDER BY
        CASE WHEN @SapXep = 'gia_tang'  THEN v.GiaThapNhat END ASC,
        CASE WHEN @SapXep = 'gia_giam'  THEN v.GiaThapNhat END DESC,
        CASE WHEN @SapXep = 'banchay'   THEN v.DaBan END DESC,
        CASE WHEN @SapXep = 'danhgia'   THEN v.SaoTrungBinh END DESC,
        CASE WHEN @SapXep = 'xemnhieu'  THEN v.LuotXem END DESC,
        v.NgayTao DESC, v.Id
    OFFSET (@Trang - 1) * @SoDong ROWS FETCH NEXT @SoDong ROWS ONLY
    OPTION (RECOMPILE);
END
GO
 
-- Tính trả góp theo cấu hình trong bảng GoiTraGop
CREATE PROCEDURE sp_TinhTraGop @BienTheId INT, @SoThang TINYINT, @TraTruocPT TINYINT = 0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Gia DECIMAL(18,0), @MinTraTruoc TINYINT, @Phi DECIMAL(4,1);
 
    SELECT @Gia = GiaBan FROM BienTheSanPham WHERE Id = @BienTheId AND HienThi = 1;
    IF @Gia IS NULL THROW 50050, N'Không tìm thấy sản phẩm', 1;
 
    SELECT @MinTraTruoc = TraTruocToiThieuPT, @Phi = PhiChuyenDoiPT FROM GoiTraGop WHERE SoThang = @SoThang AND HienThi = 1;
    IF @MinTraTruoc IS NULL THROW 50051, N'Kỳ hạn trả góp không hợp lệ', 1;
    IF @TraTruocPT < @MinTraTruoc OR @TraTruocPT > 90 THROW 50052, N'Tỷ lệ trả trước không hợp lệ cho kỳ hạn này', 1;
 
    DECLARE @TraTruoc DECIMAL(18,0) = ROUND(@Gia * @TraTruocPT / 100, 0);
    DECLARE @Vay DECIMAL(18,0) = @Gia - @TraTruoc;
    DECLARE @PhiTien DECIMAL(18,0) = ROUND(@Vay * @Phi / 100, 0);
 
    SELECT @Gia AS GiaBan, @SoThang AS SoThang, @TraTruoc AS TraTruoc, @Vay AS SoTienTraGop,
           @PhiTien AS PhiChuyenDoi, CEILING((@Vay + @PhiTien) / @SoThang) AS TraMoiThang,
           @TraTruoc + @Vay + @PhiTien AS TongPhaiTra;
END
GO
 
-- Thêm đánh giá: báo lỗi dễ hiểu nếu chưa mua hoặc đã đánh giá rồi
CREATE PROCEDURE sp_ThemDanhGia
    @SanPhamId INT, @NguoiDungId INT, @SoSao TINYINT, @NoiDung NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM vw_NguoiDaMuaSanPham WHERE NguoiDungId = @NguoiDungId AND SanPhamId = @SanPhamId)
        THROW 50040, N'Chỉ khách đã nhận hàng mới được đánh giá sản phẩm này', 1;
    IF EXISTS (SELECT 1 FROM DanhGia WHERE NguoiDungId = @NguoiDungId AND SanPhamId = @SanPhamId)
        THROW 50041, N'Bạn đã đánh giá sản phẩm này rồi', 1;
    INSERT INTO DanhGia (SanPhamId, NguoiDungId, SoSao, NoiDung) VALUES (@SanPhamId, @NguoiDungId, @SoSao, @NoiDung);
END
GO
 
-- Chốt chặn cuối ở tầng database: INSERT trực tiếp vào DanhGia mà chưa mua hàng cũng bị từ chối
CREATE TRIGGER tr_DanhGia_ChiNguoiDaMua ON DanhGia
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               WHERE NOT EXISTS (SELECT 1 FROM vw_NguoiDaMuaSanPham v
                                 WHERE v.NguoiDungId = i.NguoiDungId AND v.SanPhamId = i.SanPhamId))
        THROW 50040, N'Chỉ khách đã nhận hàng mới được đánh giá sản phẩm này', 1;
END
GO
 
-- ===== CHẠY THỬ (chỉ đọc dữ liệu) =====
SELECT TOP 5 * FROM vw_TopBanChay ORDER BY SoLuongBan DESC;
SELECT * FROM vw_CanhBaoTonKho;
EXEC sp_TraCuuBaoHanh '356938110245871';   -- còn bảo hành
EXEC sp_TraCuuBaoHanh '359871046620287';   -- hết bảo hành
EXEC sp_DoanhThuTheoThang 2026;
SELECT dbo.fn_TinhTienGiam('GIAM500K', 20000000) AS TienGiam;   -- 500000
EXEC sp_GoiYTimKiem N'iphone';
EXEC sp_LocSanPham @LoaiId = 1, @GiaTu = 10000000, @GiaDen = 30000000, @RamGB = 8, @SapXep = 'gia_tang';
EXEC sp_TinhTraGop @BienTheId = 18, @SoThang = 12, @TraTruocPT = 30;
-- Tổng lịch sử tồn kho phải khớp SoLuongTon: kết quả bên dưới phải bằng 0
SELECT COUNT(*) AS SoBienTheLech
FROM BienTheSanPham bt
WHERE bt.SoLuongTon <> (SELECT ISNULL(SUM(SoLuongThayDoi), 0) FROM LichSuTonKho WHERE BienTheId = bt.Id);
