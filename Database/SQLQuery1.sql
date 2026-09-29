-- ====================================================================
-- HỆ THỐNG BÁN VÉ TÀU HỎA TRỰC TUYẾN (TrainTicketApp)
-- ====================================================================

CREATE DATABASE db_TrainTicketApp;
GO

USE db_TrainTicketApp;
GO

-- 1. Bảng tblNguoiDung (Tài khoản & Phân quyền RBAC: Admin / NhanVien / KhachHang)
CREATE TABLE tblNguoiDung (
    user_id INT IDENTITY(1,1) PRIMARY KEY,
    sHoTen NVARCHAR(100) NOT NULL,
    sEmail VARCHAR(100) NOT NULL UNIQUE,
    sMatKhau VARCHAR(255) NOT NULL, -- Mật khẩu băm BCrypt / ASP.NET Identity
    sSdt VARCHAR(15),
    sCccd VARCHAR(20),
    sVaiTro NVARCHAR(20) NOT NULL DEFAULT N'KhachHang', -- Admin / NhanVien / KhachHang
    sTrangThai NVARCHAR(20) NOT NULL DEFAULT N'HoatDong', -- HoatDong / BiKhoa
    dNgayTao DATETIME DEFAULT GETDATE()
);

-- 2. Bảng tblNhaGa (Danh mục Nhà ga)
CREATE TABLE tblNhaGa (
    ga_id INT IDENTITY(1,1) PRIMARY KEY,
    sTenGa NVARCHAR(100) NOT NULL,
    sMaGa VARCHAR(20) NOT NULL UNIQUE,
    sDiaChi NVARCHAR(255),
    sThanhPho NVARCHAR(100),
    sTrangThai NVARCHAR(20) DEFAULT N'HoatDong'
);

-- 3. Bảng tblTuyenDuong (Tuyến đường kết nối các ga)
CREATE TABLE tblTuyenDuong (
    tuyen_id INT IDENTITY(1,1) PRIMARY KEY,
    sTenTuyen NVARCHAR(150) NOT NULL,
    fk_gaBatDauID INT NOT NULL,
    fk_gaKetThucID INT NOT NULL,
    fDoDaiKm FLOAT NOT NULL,
    sTrangThai NVARCHAR(20) DEFAULT N'HoatDong',
    CONSTRAINT FK_TuyenDuong_GaBatDau FOREIGN KEY (fk_gaBatDauID) REFERENCES tblNhaGa(ga_id),
    CONSTRAINT FK_TuyenDuong_GaKetThuc FOREIGN KEY (fk_gaKetThucID) REFERENCES tblNhaGa(ga_id)
);

-- 4. Bảng tblChuyenTau (Lịch trình chuyến tàu)
CREATE TABLE tblChuyenTau (
    chuyentau_id INT IDENTITY(1,1) PRIMARY KEY,
    sMacTau VARCHAR(20) NOT NULL, -- Mã hiệu tàu: SE1, SE3, TN1...
    fk_tuyenID INT NOT NULL,
    dGioKhoiHanh DATETIME NOT NULL,
    dGioDenDuKien DATETIME NOT NULL,
    fGiaVeCoSo DECIMAL(18,0) NOT NULL,
    iTongSoGhe INT NOT NULL DEFAULT 0,
    iSoGheTrong INT NOT NULL DEFAULT 0,
    sTrangThai NVARCHAR(20) DEFAULT N'HoatDong', -- HoatDong / TamNguung / Huy
    dNgayTao DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_ChuyenTau_TuyenDuong FOREIGN KEY (fk_tuyenID) REFERENCES tblTuyenDuong(tuyen_id)
);

-- 5. Bảng tblToaTau (Cơ cấu toa xe)
CREATE TABLE tblToaTau (
    toa_id INT IDENTITY(1,1) PRIMARY KEY,
    fk_chuyentauID INT NOT NULL,
    iSoToa INT NOT NULL, -- Số thứ tự toa (1, 2, 3...)
    sTenToa NVARCHAR(50),
    sLoaiCho NVARCHAR(50) NOT NULL, -- Ngồi mềm điều hòa, Giường nằm khoang 4, Giường nằm khoang 6
    iTongSoGhe INT NOT NULL,
    CONSTRAINT FK_ToaTau_ChuyenTau FOREIGN KEY (fk_chuyentauID) REFERENCES tblChuyenTau(chuyentau_id) ON DELETE CASCADE
);

-- 6. Bảng tblGhe (Trạng thái ghế & Tạm giữ chỗ 15 phút)
CREATE TABLE tblGhe (
    ghe_id INT IDENTITY(1,1) PRIMARY KEY,
    fk_toaID INT NOT NULL,
    iSoGhe INT NOT NULL, -- Số ghế (1, 2, 3...)
    sTrangThai NVARCHAR(20) NOT NULL DEFAULT N'Trong', -- Trong / DangGiu / DaBan
    dThoiGianGiuGhe DATETIME NULL, -- Thời điểm thực hiện giữ chỗ (hết hạn sau 15 phút)
    fk_userGiuChoID INT NULL, -- Tài khoản người dùng đang giữ chỗ
    CONSTRAINT FK_Ghe_ToaTau FOREIGN KEY (fk_toaID) REFERENCES tblToaTau(toa_id) ON DELETE CASCADE,
    CONSTRAINT FK_Ghe_NguoiDung FOREIGN KEY (fk_userGiuChoID) REFERENCES tblNguoiDung(user_id)
);

-- 7. Bảng tblVeTau (Thông tin Vé điện tử & QR Code)
CREATE TABLE tblVeTau (
    ve_id INT IDENTITY(1,1) PRIMARY KEY,
    sMaVe VARCHAR(50) NOT NULL UNIQUE, -- Mã vé điện tử
    sMaQRCode VARCHAR(255), -- Chuỗi dữ liệu sinh mã QR Code
    fk_userID INT NOT NULL, -- Khách hàng mua vé
    fk_chuyentauID INT NOT NULL,
    fk_gheID INT NOT NULL,
    sHoTenHanhKhach NVARCHAR(100) NOT NULL,
    sCccdHanhKhach VARCHAR(20) NOT NULL,
    fGiaVe DECIMAL(18,0) NOT NULL,
    sTrangThaiThanhToan NVARCHAR(30) NOT NULL DEFAULT N'ChoThanhToan', -- ChoThanhToan / DaThanhToan / DaHuy
    dNgayDat DATETIME DEFAULT GETDATE(),
    dNgayThanhToan DATETIME NULL,
    fk_nhanVienXacNhanID INT NULL, -- Nhân viên hoặc Admin bấm xác nhận thanh toán
    CONSTRAINT FK_VeTau_NguoiDung FOREIGN KEY (fk_userID) REFERENCES tblNguoiDung(user_id),
    CONSTRAINT FK_VeTau_ChuyenTau FOREIGN KEY (fk_chuyentauID) REFERENCES tblChuyenTau(chuyentau_id),
    CONSTRAINT FK_VeTau_Ghe FOREIGN KEY (fk_gheID) REFERENCES tblGhe(ghe_id),
    CONSTRAINT FK_VeTau_NhanVien FOREIGN KEY (fk_nhanVienXacNhanID) REFERENCES tblNguoiDung(user_id)
);

-- 8. Bảng tblBaoCaoThongKe (Thống kê Doanh thu & Báo cáo)
CREATE TABLE tblBaoCaoThongKe (
    baocao_id INT IDENTITY(1,1) PRIMARY KEY,
    sTieuDe NVARCHAR(200) NOT NULL,
    sLoaiBaoCao NVARCHAR(50), -- TheoTuan / TheoThang / TheoTuyen
    dTungay DATE NOT NULL,
    dDenngay DATE NOT NULL,
    fk_adminLapID INT NOT NULL,
    iTongVeBan INT DEFAULT 0,
    iTongVeHuy INT DEFAULT 0,
    fTongDoanhThu DECIMAL(18,0) DEFAULT 0,
    fTyLeLapDay FLOAT DEFAULT 0,
    sTepBaoCao VARCHAR(255),
    dNgayLap DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_BaoCao_Admin FOREIGN KEY (fk_adminLapID) REFERENCES tblNguoiDung(user_id)
);


-- ====================================================================
-- CHÈN DỮ LIỆU MẪU (SEED DATA)
-- ====================================================================

-- 1. Chèn Tài khoản (Tên Admin mặc định: Trần Thị Thảo)
INSERT INTO tblNguoiDung (sHoTen, sEmail, sMatKhau, sSdt, sCccd, sVaiTro, sTrangThai)
VALUES 
(N'Trần Thị Thảo', 'admin@vetau.vn', 'Admin@123', '0911222333', '001199000001', N'Admin', N'HoatDong'),
(N'Lê Thị Huế', 'hue.staff@vetau.vn', 'Staff@123', '0944555666', '001199000002', N'NhanVien', N'HoatDong'),
(N'Bùi Thị Mai Anh', 'maianh@gmail.com', 'User@123', '0977888999', '001199000003', N'KhachHang', N'HoatDong');

-- 2. Chèn Danh mục Nhà ga
INSERT INTO tblNhaGa (sTenGa, sMaGa, sDiaChi, sThanhPho)
VALUES 
(N'Ga Hà Nội', 'HAN', N'120 Lê Duẩn, Hoàn Kiếm', N'Hà Nội'),
(N'Ga Nam Định', 'NDI', N'Trần Đăng Ninh', N'Nam Định'),
(N'Ga Đà Nẵng', 'DNG', N'202 Hải Phòng, Thanh Khê', N'Đà Nẵng'),
(N'Ga Sài Gòn', 'SGO', N'1 Nguyễn Thông, Phường 9, Quận 3', N'TP. Hồ Chí Minh');

-- 3. Chèn Tuyến đường
INSERT INTO tblTuyenDuong (sTenTuyen, fk_gaBatDauID, fk_gaKetThucID, fDoDaiKm)
VALUES 
(N'Hà Nội - Sài Gòn', 1, 4, 1726),
(N'Hà Nội - Đà Nẵng', 1, 3, 791);

-- 4. Chèn Chuyến tàu Lịch trình
INSERT INTO tblChuyenTau (sMacTau, fk_tuyenID, dGioKhoiHanh, dGioDenDuKien, fGiaVeCoSo, iTongSoGhe, iSoGheTrong)
VALUES 
('SE1', 1, '2026-10-01 22:15:00', '2026-10-03 05:45:00', 950000, 40, 38),
('SE3', 2, '2026-10-02 19:25:00', '2026-10-03 11:30:00', 650000, 40, 40);

-- 5. Chèn Toa tàu
INSERT INTO tblToaTau (fk_chuyentauID, iSoToa, sTenToa, sLoaiCho, iTongSoGhe)
VALUES 
(1, 1, N'Toa 1 - Ngồi mềm', N'Ngồi mềm điều hòa', 20),
(1, 2, N'Toa 2 - Giường nằm', N'Giường nằm khoang 4', 20);

-- 6. Chèn Vị trí ghế
INSERT INTO tblGhe (fk_toaID, iSoGhe, sTrangThai)
VALUES 
(1, 1, N'DaBan'),
(1, 2, N'DangGiu'),
(1, 3, N'Trong'),
(1, 4, N'Trong');

-- 7. Chèn Vé tàu mẫu
INSERT INTO tblVeTau (sMaVe, sMaQRCode, fk_userID, fk_chuyentauID, fk_gheID, sHoTenHanhKhach, sCccdHanhKhach, fGiaVe, sTrangThaiThanhToan, dNgayThanhToan, fk_nhanVienXacNhanID)
VALUES 
('VE-SE1-001', 'QR-VE-SE1-001-9988', 3, 1, 1, N'Bùi Thị Mai Anh', '001199000003', 950000, N'DaThanhToan', GETDATE(), 2);

-- 8. Chèn Báo cáo thống kê
INSERT INTO tblBaoCaoThongKe (sTieuDe, sLoaiBaoCao, dTungay, dDenngay, fk_adminLapID, iTongVeBan, iTongVeHuy, fTongDoanhThu, fTyLeLapDay, sTepBaoCao)
VALUES 
(N'Báo cáo doanh thu tuyến Hà Nội - Sài Gòn tháng 09/2026', N'TheoThang', '2026-09-01', '2026-09-30', 1, 450, 15, 427500000, 88.5, 'bc_doanhthu_thang9_2026.xlsx');

-- Truy vấn kiểm tra dữ liệu
SELECT * FROM tblNguoiDung;
SELECT * FROM tblNhaGa;
SELECT * FROM tblTuyenDuong;
SELECT * FROM tblChuyenTau;
SELECT * FROM tblToaTau;
SELECT * FROM tblGhe;
SELECT * FROM tblVeTau;
SELECT * FROM tblBaoCaoThongKe;