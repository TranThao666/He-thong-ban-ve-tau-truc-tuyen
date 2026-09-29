# BTL_TrainTicketApp

Ứng dụng đặt vé tàu xây dựng bằng ASP.NET Core MVC, Entity Framework Core và SQL Server.

## Chạy ứng dụng

1. Mở `Database/SQLQuery1.sql` bằng SQL Server Management Studio và chạy một lần để tạo database `db_TrainTicketApp` cùng dữ liệu ban đầu.
2. Nếu SQL Server của bạn dùng instance khác, sửa `ConnectionStrings:TrainTicketDb` trong `appsettings.json`.
3. Mở file `BTL_TrainTicketApp.csproj` bằng Visual Studio hoặc chạy `dotnet run` trong thư mục này.

Tài khoản mẫu được lấy từ bảng `tblNguoiDung` trong script SQL:

| Vai trò | Email | Mật khẩu |
| --- | --- | --- |
| Admin | admin@vetau.vn | Admin@123 |
| Nhân viên | hue.staff@vetau.vn | Staff@123 |
| Khách hàng | maianh@gmail.com | User@123 |

Các tài khoản mẫu dùng mật khẩu dạng chữ thường theo dữ liệu SQL. Mật khẩu đăng ký mới được lưu dạng băm.

## Chức năng

- Tìm chuyến theo ga đi, ga đến và ngày khởi hành.
- Đăng ký, đăng nhập, cập nhật hồ sơ và phân quyền theo bảng `tblNguoiDung`.
- Chọn ghế, giữ chỗ trong 15 phút, nhập thông tin hành khách và thanh toán mô phỏng.
- Xem vé điện tử và lịch sử vé.
- Nhân viên tra cứu vé bằng mã vé hoặc CCCD, sau đó xác nhận thanh toán.
- Admin xem tổng quan, quản lý tuyến, tạo chuyến/sơ đồ ghế và cập nhật vai trò/trạng thái người dùng.
- Các biểu mẫu bắt buộc được kiểm tra bằng AJAX trước khi gửi và được xác thực lại ở server.

Khi ứng dụng khởi động, sơ đồ ghế còn thiếu trong các toa có sẵn sẽ được bổ sung dựa trên số ghế khai báo trong SQL.
