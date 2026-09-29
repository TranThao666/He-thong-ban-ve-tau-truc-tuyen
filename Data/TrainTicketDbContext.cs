using BTL_TrainTicketApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BTL_TrainTicketApp.Data;

public sealed class TrainTicketDbContext(DbContextOptions<TrainTicketDbContext> options) : DbContext(options)
{
    public DbSet<DbUser> Users => Set<DbUser>();
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<RailRoute> Routes => Set<RailRoute>();
    public DbSet<TrainTrip> Trips => Set<TrainTrip>();
    public DbSet<TrainCar> Cars => Set<TrainCar>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<TrainTicket> Tickets => Set<TrainTicket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbUser>(entity =>
        {
            entity.ToTable("tblNguoiDung");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasColumnName("user_id");
            entity.Property(user => user.Name).HasColumnName("sHoTen").HasMaxLength(100).IsRequired();
            entity.Property(user => user.Email).HasColumnName("sEmail").HasMaxLength(100).IsRequired();
            entity.Property(user => user.Password).HasColumnName("sMatKhau").HasMaxLength(255).IsRequired();
            entity.Property(user => user.Phone).HasColumnName("sSdt").HasMaxLength(15);
            entity.Property(user => user.Cccd).HasColumnName("sCccd").HasMaxLength(20);
            entity.Property(user => user.Role).HasColumnName("sVaiTro").HasMaxLength(20).IsRequired();
            entity.Property(user => user.Status).HasColumnName("sTrangThai").HasMaxLength(20).IsRequired();
            entity.Property(user => user.Created).HasColumnName("dNgayTao");
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<Station>(entity =>
        {
            entity.ToTable("tblNhaGa");
            entity.HasKey(station => station.Id);
            entity.Property(station => station.Id).HasColumnName("ga_id");
            entity.Property(station => station.Name).HasColumnName("sTenGa").HasMaxLength(100).IsRequired();
            entity.Property(station => station.Code).HasColumnName("sMaGa").HasMaxLength(20).IsRequired();
            entity.Property(station => station.Address).HasColumnName("sDiaChi").HasMaxLength(255);
            entity.Property(station => station.City).HasColumnName("sThanhPho").HasMaxLength(100);
            entity.Property(station => station.Status).HasColumnName("sTrangThai").HasMaxLength(20);
        });

        modelBuilder.Entity<RailRoute>(entity =>
        {
            entity.ToTable("tblTuyenDuong");
            entity.HasKey(route => route.Id);
            entity.Property(route => route.Id).HasColumnName("tuyen_id");
            entity.Property(route => route.Name).HasColumnName("sTenTuyen").HasMaxLength(150).IsRequired();
            entity.Property(route => route.StartStationId).HasColumnName("fk_gaBatDauID");
            entity.Property(route => route.EndStationId).HasColumnName("fk_gaKetThucID");
            entity.Property(route => route.DistanceKm).HasColumnName("fDoDaiKm");
            entity.Property(route => route.Status).HasColumnName("sTrangThai").HasMaxLength(20);
            entity.HasOne(route => route.StartStation).WithMany().HasForeignKey(route => route.StartStationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(route => route.EndStation).WithMany().HasForeignKey(route => route.EndStationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TrainTrip>(entity =>
        {
            entity.ToTable("tblChuyenTau");
            entity.HasKey(trip => trip.Id);
            entity.Property(trip => trip.Id).HasColumnName("chuyentau_id");
            entity.Property(trip => trip.TrainCode).HasColumnName("sMacTau").HasMaxLength(20).IsRequired();
            entity.Property(trip => trip.RouteId).HasColumnName("fk_tuyenID");
            entity.Property(trip => trip.Departure).HasColumnName("dGioKhoiHanh");
            entity.Property(trip => trip.Arrival).HasColumnName("dGioDenDuKien");
            entity.Property(trip => trip.BaseFare).HasColumnName("fGiaVeCoSo").HasColumnType("decimal(18,0)");
            entity.Property(trip => trip.SeatCount).HasColumnName("iTongSoGhe");
            entity.Property(trip => trip.AvailableSeats).HasColumnName("iSoGheTrong");
            entity.Property(trip => trip.Status).HasColumnName("sTrangThai").HasMaxLength(20);
            entity.HasOne(trip => trip.Route).WithMany(route => route.Trips).HasForeignKey(trip => trip.RouteId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TrainCar>(entity =>
        {
            entity.ToTable("tblToaTau");
            entity.HasKey(car => car.Id);
            entity.Property(car => car.Id).HasColumnName("toa_id");
            entity.Property(car => car.TripId).HasColumnName("fk_chuyentauID");
            entity.Property(car => car.Number).HasColumnName("iSoToa");
            entity.Property(car => car.Name).HasColumnName("sTenToa").HasMaxLength(50);
            entity.Property(car => car.SeatType).HasColumnName("sLoaiCho").HasMaxLength(50).IsRequired();
            entity.Property(car => car.SeatCount).HasColumnName("iTongSoGhe");
            entity.HasOne(car => car.Trip).WithMany(trip => trip.Cars).HasForeignKey(car => car.TripId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Seat>(entity =>
        {
            entity.ToTable("tblGhe");
            entity.HasKey(seat => seat.Id);
            entity.Property(seat => seat.Id).HasColumnName("ghe_id");
            entity.Property(seat => seat.CarId).HasColumnName("fk_toaID");
            entity.Property(seat => seat.Number).HasColumnName("iSoGhe");
            entity.Property(seat => seat.Status).HasColumnName("sTrangThai").HasMaxLength(20).IsRequired();
            entity.Property(seat => seat.HeldAt).HasColumnName("dThoiGianGiuGhe");
            entity.Property(seat => seat.HeldByUserId).HasColumnName("fk_userGiuChoID");
            entity.HasOne(seat => seat.Car).WithMany(car => car.Seats).HasForeignKey(seat => seat.CarId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(seat => seat.HeldByUser).WithMany(user => user.HeldSeats).HasForeignKey(seat => seat.HeldByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TrainTicket>(entity =>
        {
            entity.ToTable("tblVeTau");
            entity.HasKey(ticket => ticket.Id);
            entity.Property(ticket => ticket.Id).HasColumnName("ve_id");
            entity.Property(ticket => ticket.Code).HasColumnName("sMaVe").HasMaxLength(50).IsRequired();
            entity.Property(ticket => ticket.QrCode).HasColumnName("sMaQRCode").HasMaxLength(255);
            entity.Property(ticket => ticket.UserId).HasColumnName("fk_userID");
            entity.Property(ticket => ticket.TripId).HasColumnName("fk_chuyentauID");
            entity.Property(ticket => ticket.SeatId).HasColumnName("fk_gheID");
            entity.Property(ticket => ticket.PassengerName).HasColumnName("sHoTenHanhKhach").HasMaxLength(100).IsRequired();
            entity.Property(ticket => ticket.PassengerCccd).HasColumnName("sCccdHanhKhach").HasMaxLength(20).IsRequired();
            entity.Property(ticket => ticket.Fare).HasColumnName("fGiaVe").HasColumnType("decimal(18,0)");
            entity.Property(ticket => ticket.PaymentStatus).HasColumnName("sTrangThaiThanhToan").HasMaxLength(30).IsRequired();
            entity.Property(ticket => ticket.BookedAt).HasColumnName("dNgayDat");
            entity.Property(ticket => ticket.PaidAt).HasColumnName("dNgayThanhToan");
            entity.Property(ticket => ticket.ConfirmedByUserId).HasColumnName("fk_nhanVienXacNhanID");
            entity.HasIndex(ticket => ticket.Code).IsUnique();
            entity.HasOne(ticket => ticket.User).WithMany(user => user.Tickets).HasForeignKey(ticket => ticket.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(ticket => ticket.Trip).WithMany(trip => trip.Tickets).HasForeignKey(ticket => ticket.TripId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(ticket => ticket.Seat).WithMany(seat => seat.Tickets).HasForeignKey(ticket => ticket.SeatId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(ticket => ticket.ConfirmedByUser).WithMany().HasForeignKey(ticket => ticket.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
