using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BTL_TrainTicketApp.Data;
using BTL_TrainTicketApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_TrainTicketApp.Controllers;

[Authorize(Roles = "Admin,NhanVien")]
public sealed class StaffController(TrainTicketDbContext db) : Controller
{
    [HttpGet]
    public IActionResult Lookup() => View(new TicketLookupInput());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lookup(TicketLookupInput input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var query = input.Query.Trim();
        var ticket = await TicketDetails().FirstOrDefaultAsync(item =>
            item.Code == query || item.PassengerCccd == query || item.QrCode == query);

        if (ticket is null)
        {
            ModelState.AddModelError(nameof(input.Query), "Không tìm thấy vé khớp với mã vé, mã QR hoặc CCCD.");
        }

        ViewBag.Ticket = ticket;
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id)
    {
        var ticket = await db.Tickets
            .Include(item => item.Seat)
            .Include(item => item.Trip)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (ticket is null)
        {
            return NotFound();
        }

        if (ticket.PaymentStatus == "ChoThanhToan" && ticket.Seat is not null)
        {
            ticket.PaymentStatus = "DaThanhToan";
            ticket.PaidAt = DateTime.Now;
            ticket.ConfirmedByUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            ticket.Seat.Status = "DaBan";
            ticket.Seat.HeldAt = null;
            ticket.Seat.HeldByUserId = null;
            if (ticket.Trip is not null)
            {
                ticket.Trip.AvailableSeats = Math.Max(0, ticket.Trip.AvailableSeats - 1);
            }

            await db.SaveChangesAsync();
            TempData["Success"] = $"Đã xác nhận thanh toán cho vé {ticket.Code}.";
        }

        return RedirectToAction(nameof(Lookup));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckInBoarding(int id)
    {
        var ticket = await db.Tickets
            .Include(t => t.Trip)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket is null)
        {
            return NotFound();
        }

        if (ticket.PaymentStatus != "DaThanhToan")
        {
            TempData["Error"] = "Vé chưa thanh toán hoặc đã bị hủy, không thể xác nhận lên tàu.";
            return RedirectToAction(nameof(Lookup));
        }

        if (ticket.BoardingStatus == "DaLenTau")
        {
            TempData["Error"] = $"Vé {ticket.Code} đã được soát lên tàu trước đó lúc {ticket.BoardedAt:HH:mm dd/MM/yyyy}.";
            return RedirectToAction(nameof(Lookup));
        }

        ticket.BoardingStatus = "DaLenTau";
        ticket.BoardedAt = DateTime.Now;
        ticket.ConfirmedByUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        await db.SaveChangesAsync();
        TempData["Success"] = $"Soát vé thành công! Hành khách {ticket.PassengerName} (Vé {ticket.Code}) đã xác nhận lên tàu.";
        return RedirectToAction(nameof(Lookup));
    }

    [HttpGet]
    public async Task<IActionResult> CounterSale(int? tripId)
    {
        var activeTrips = await db.Trips.AsNoTracking()
            .Include(t => t.Route)!
                .ThenInclude(r => r!.StartStation)
            .Include(t => t.Route)!
                .ThenInclude(r => r!.EndStation)
            .Where(t => t.Status == "HoatDong" && t.AvailableSeats > 0 && t.Departure > DateTime.Now)
            .OrderBy(t => t.Departure)
            .ToListAsync();

        ViewBag.Trips = activeTrips;

        var selectedTripId = tripId ?? activeTrips.FirstOrDefault()?.Id;
        if (selectedTripId.HasValue)
        {
            ViewBag.SelectedTrip = activeTrips.FirstOrDefault(t => t.Id == selectedTripId.Value);
            ViewBag.Cars = await db.Cars.AsNoTracking()
                .Where(c => c.TripId == selectedTripId.Value)
                .Include(c => c.Seats)
                .OrderBy(c => c.Number)
                .ToListAsync();
        }

        return View(new CounterBookingInput { TripId = selectedTripId ?? 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CounterSale(CounterBookingInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vui lòng điền đầy đủ thông tin hành khách và chọn ghế.";
            return RedirectToAction(nameof(CounterSale), new { tripId = input.TripId });
        }

        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == input.TripId && t.Status == "HoatDong");
        var seat = await db.Seats.Include(s => s.Car)
            .FirstOrDefaultAsync(s => s.Id == input.SeatId && s.Car!.TripId == input.TripId);

        if (trip is null || seat is null || trip.Departure <= DateTime.Now)
        {
            TempData["Error"] = "Chuyến tàu hoặc ghế không hợp lệ.";
            return RedirectToAction(nameof(CounterSale), new { tripId = input.TripId });
        }

        var isSeatLocked = seat.Status == "DaBan" ||
                           (seat.Status == "DangGiu" && seat.HeldAt >= DateTime.Now.AddMinutes(-15));

        if (isSeatLocked)
        {
            TempData["Error"] = "Ghế đã có người đặt hoặc đang được giữ. Vui lòng chọn ghế trống khác.";
            return RedirectToAction(nameof(CounterSale), new { tripId = input.TripId });
        }

        var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var now = DateTime.Now;

        var ticket = new TrainTicket
        {
            Code = $"VE-{trip.TrainCode}-{now:yyMMddHHmmss}-{RandomNumberGenerator.GetInt32(100, 999)}",
            QrCode = $"BTL:{trip.Id}:{seat.Id}:{staffId}",
            UserId = staffId,
            TripId = trip.Id,
            SeatId = seat.Id,
            PassengerName = input.PassengerName.Trim(),
            PassengerCccd = input.PassengerCccd.Trim(),
            Fare = trip.BaseFare,
            PaymentStatus = "DaThanhToan",
            BoardingStatus = "ChuaSoat",
            BookedAt = now,
            PaidAt = now,
            ConfirmedByUserId = staffId
        };

        seat.Status = "DaBan";
        seat.HeldAt = null;
        seat.HeldByUserId = null;
        trip.AvailableSeats = Math.Max(0, trip.AvailableSeats - 1);

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        TempData["Success"] = $"Xuất vé tại quầy thành công! Mã vé: {ticket.Code} ({ticket.PassengerName}).";
        return RedirectToAction(nameof(CounterSale), new { tripId = input.TripId });
    }

    private IQueryable<TrainTicket> TicketDetails() => db.Tickets.AsNoTracking()
        .Include(ticket => ticket.User)
        .Include(ticket => ticket.Trip)!
            .ThenInclude(trip => trip!.Route)!
                .ThenInclude(route => route!.StartStation)
        .Include(ticket => ticket.Trip)!
            .ThenInclude(trip => trip!.Route)!
                .ThenInclude(route => route!.EndStation)
        .Include(ticket => ticket.Seat)!
            .ThenInclude(seat => seat!.Car);
}

[Authorize(Roles = "Admin")]
public sealed class AdminController(TrainTicketDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Dashboard(DateTime? fromDate, DateTime? toDate)
    {
        var start = (fromDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
        var end = (toDate ?? DateTime.Today).Date;
        var endInclusive = end.AddDays(1).AddTicks(-1);

        var filteredTickets = db.Tickets.AsNoTracking()
            .Where(t => t.BookedAt >= start && t.BookedAt <= endInclusive);

        var paid = await filteredTickets.CountAsync(ticket => ticket.PaymentStatus == "DaThanhToan");
        var cancelled = await filteredTickets.CountAsync(ticket => ticket.PaymentStatus == "DaHuy");
        var revenue = await filteredTickets
            .Where(ticket => ticket.PaymentStatus == "DaThanhToan")
            .SumAsync(ticket => (decimal?)ticket.Fare) ?? 0;

        var totalSeatsInPeriod = await db.Trips.AsNoTracking()
            .Where(t => t.Departure >= start && t.Departure <= endInclusive)
            .SumAsync(t => (int?)t.SeatCount) ?? 0;

        var occupancyRate = totalSeatsInPeriod > 0
            ? Math.Round((double)paid / totalSeatsInPeriod * 100, 2)
            : 0;

        ViewBag.FromDate = start;
        ViewBag.ToDate = end;
        ViewBag.Users = await db.Users.CountAsync();
        ViewBag.Trips = await db.Trips.CountAsync(t => t.Departure >= start && t.Departure <= endInclusive);
        ViewBag.Paid = paid;
        ViewBag.Cancelled = cancelled;
        ViewBag.Revenue = revenue;
        ViewBag.OccupancyRate = occupancyRate;

        ViewBag.RecentTrips = await db.Trips.AsNoTracking()
            .Include(trip => trip.Route)!
                .ThenInclude(route => route!.StartStation)
            .Include(trip => trip.Route)!
                .ThenInclude(route => route!.EndStation)
            .OrderByDescending(trip => trip.Departure)
            .Take(8)
            .ToListAsync();

        ViewBag.SavedReports = await db.Reports.AsNoTracking()
            .Include(r => r.Admin)
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .ToListAsync();

        return View();
    }

// --- LƯU & XUẤT FILE BÁO CÁO VÀO tblBaoCaoThongKe ---
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAndExportReport(string title, string reportType, DateTime fromDate, DateTime toDate)
    {
        var start = fromDate.Date;
        var end = toDate.Date;
        if (end < start)
        {
            TempData["Error"] = "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.";
            return RedirectToAction(nameof(Dashboard));
        }

        var endInclusive = end.AddDays(1).AddTicks(-1);
        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var tickets = await db.Tickets.AsNoTracking()
            .Include(t => t.Trip)!
                .ThenInclude(tr => tr!.Route)
            .Where(t => t.BookedAt >= start && t.BookedAt <= endInclusive)
            .ToListAsync();

        var totalSold = tickets.Count(t => t.PaymentStatus == "DaThanhToan");
        var totalCancelled = tickets.Count(t => t.PaymentStatus == "DaHuy");
        var totalRevenue = tickets.Where(t => t.PaymentStatus == "DaThanhToan").Sum(t => t.Fare);

        var totalSeats = await db.Trips.AsNoTracking()
            .Where(t => t.Departure >= start && t.Departure <= endInclusive)
            .SumAsync(t => (int?)t.SeatCount) ?? 0;

        var occupancyRate = totalSeats > 0 ? Math.Round((double)totalSold / totalSeats * 100, 2) : 0;
        var fileName = $"baocao_{start:yyyyMMdd}_{end:yyyyMMdd}_{DateTime.Now:HHmmss}.csv";

        var report = new StatisticalReport
        {
            Title = string.IsNullOrWhiteSpace(title) ? $"Báo cáo doanh thu {start:dd/MM/yyyy} - {end:dd/MM/yyyy}" : title.Trim(),
            ReportType = string.IsNullOrWhiteSpace(reportType) ? "TuyChon" : reportType,
            FromDate = start,
            ToDate = end,
            AdminId = adminId,
            TotalSold = totalSold,
            TotalCancelled = totalCancelled,
            TotalRevenue = totalRevenue,
            OccupancyRate = occupancyRate,
            FileName = fileName,
            CreatedAt = DateTime.Now
        };

        db.Reports.Add(report);
        await db.SaveChangesAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Tieu de bao cao,Loai bao cao,Tu ngay,Den ngay,Tong ve ban,Tong ve huy,Tong doanh thu (VND),Ty le lap day (%)");
        sb.AppendLine($"\"{report.Title}\",{report.ReportType},{start:yyyy-MM-dd},{end:yyyy-MM-dd},{totalSold},{totalCancelled},{totalRevenue},{occupancyRate}");
        sb.AppendLine();
        sb.AppendLine("Ma ve,Mac tau,Tuyen duong,Hanh khach,CCCD,Gia ve,Trang thai thanh toan,Ngay dat");
        foreach (var t in tickets)
        {
            sb.AppendLine($"{t.Code},{t.Trip?.TrainCode},\"{t.Trip?.Route?.Name}\",\"{t.PassengerName}\",{t.PassengerCccd},{t.Fare},{t.PaymentStatus},{t.BookedAt:yyyy-MM-dd HH:mm}");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

// --- 2. QUẢN TRỊ NHÀ GA, TUYẾN ĐƯỜNG & CHUYẾN TÀU ---
    [HttpGet]
    public async Task<IActionResult> RoutesAndTrips()
    {
        ViewBag.Stations = await db.Stations.AsNoTracking().OrderBy(station => station.Name).ToListAsync();
        ViewBag.Routes = await db.Routes.AsNoTracking()
            .Include(route => route.StartStation)
            .Include(route => route.EndStation)
            .OrderBy(route => route.Name)
            .ToListAsync();
        ViewBag.Trips = await db.Trips.AsNoTracking()
            .Include(trip => trip.Route)!
                .ThenInclude(route => route!.StartStation)
            .Include(trip => trip.Route)!
                .ThenInclude(route => route!.EndStation)
            .OrderByDescending(trip => trip.Departure)
            .ToListAsync();
        return View();
    }

// --- THÊM / SỬA / KHÓA NHÀ GA ---
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStation(StationInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vui lòng nhập đầy đủ Tên ga và Mã ga.";
            return RedirectToAction(nameof(RoutesAndTrips));
        }

        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.Stations.AnyAsync(s => s.Code == code))
        {
            TempData["Error"] = $"Mã ga '{code}' đã tồn tại.";
            return RedirectToAction(nameof(RoutesAndTrips));
        }

        db.Stations.Add(new Station
        {
            Name = input.Name.Trim(),
            Code = code,
            Address = input.Address?.Trim(),
            City = input.City?.Trim(),
            Status = "HoatDong"
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã thêm nhà ga mới.";
        return RedirectToAction(nameof(RoutesAndTrips));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStation(StationInput input)
    {
        var station = await db.Stations.FindAsync(input.Id);
        if (station is null || !ModelState.IsValid)
        {
            TempData["Error"] = "Không tìm thấy nhà ga hoặc dữ liệu không hợp lệ.";
            return RedirectToAction(nameof(RoutesAndTrips));
        }

        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.Stations.AnyAsync(s => s.Code == code && s.Id != input.Id))
        {
            TempData["Error"] = $"Mã ga '{code}' đã được sử dụng bởi nhà ga khác.";
            return RedirectToAction(nameof(RoutesAndTrips));
        }

        station.Name = input.Name.Trim();
        station.Code = code;
        station.Address = input.Address?.Trim();
        station.City = input.City?.Trim();

        await db.SaveChangesAsync();
        TempData["Success"] = $"Đã cập nhật thông tin {station.Name}.";
        return RedirectToAction(nameof(RoutesAndTrips));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStationStatus(int id)
    {
        var station = await db.Stations.FindAsync(id);
        if (station is null)
        {
            return NotFound();
        }

        station.Status = station.Status == "HoatDong" ? "BiKhoa" : "HoatDong";
        await db.SaveChangesAsync();

        TempData["Success"] = station.Status == "HoatDong"
            ? $"Đã mở khóa {station.Name}."
            : $"Đã khóa {station.Name}.";
        return RedirectToAction(nameof(RoutesAndTrips));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRoute(RouteInput input)
    {
        if (input.StartStationId == input.EndStationId)
        {
            TempData["Error"] = "Ga đến phải khác ga đi.";
            return RedirectToAction(nameof(RoutesAndTrips));
        }

        if (ModelState.IsValid)
        {
            db.Routes.Add(new RailRoute
            {
                Name = input.Name.Trim(),
                StartStationId = input.StartStationId,
                EndStationId = input.EndStationId,
                DistanceKm = input.DistanceKm,
                Status = "HoatDong"
            });
            await db.SaveChangesAsync();
            TempData["Success"] = "Đã tạo tuyến tàu.";
        }

        return RedirectToAction(nameof(RoutesAndTrips));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTrip(TripInput input)
    {
        if (input.Arrival <= input.Departure)
        {
            TempData["Error"] = "Giờ đến phải sau giờ khởi hành.";
            return RedirectToAction(nameof(RoutesAndTrips));
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vui lòng kiểm tra đầy đủ thông tin chuyến tàu.";
            return RedirectToAction(nameof(RoutesAndTrips));
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var trip = new TrainTrip
        {
            TrainCode = input.TrainCode.Trim().ToUpperInvariant(),
            RouteId = input.RouteId,
            Departure = input.Departure,
            Arrival = input.Arrival,
            BaseFare = input.Fare,
            SeatCount = input.SeatCount,
            AvailableSeats = input.SeatCount,
            Status = "HoatDong"
        };
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        var firstCarSeats = (input.SeatCount + 1) / 2;
        var carDefinitions = new[]
        {
            (Number: 1, Name: "Toa 1 - Ngồi mềm", Type: "Ngồi mềm điều hòa", Count: firstCarSeats),
            (Number: 2, Name: "Toa 2 - Giường nằm", Type: "Giường nằm khoang 4", Count: input.SeatCount - firstCarSeats)
        }.Where(car => car.Count > 0).ToArray();

        foreach (var definition in carDefinitions)
        {
            var car = new TrainCar
            {
                TripId = trip.Id,
                Number = definition.Number,
                Name = definition.Name,
                SeatType = definition.Type,
                SeatCount = definition.Count
            };
            db.Cars.Add(car);
            await db.SaveChangesAsync();
            db.Seats.AddRange(Enumerable.Range(1, definition.Count).Select(number => new Seat
            {
                CarId = car.Id,
                Number = number,
                Status = "Trong"
            }));
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Đã tạo chuyến tàu và sơ đồ ghế.";
        return RedirectToAction(nameof(RoutesAndTrips));
    }

// --- SỬA & TẠM DỪNG CHUYẾN TÀU ---
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTrip(EditTripInput input)
    {
        var trip = await db.Trips.FindAsync(input.Id);
        if (trip is null || !ModelState.IsValid || input.Arrival <= input.Departure)
        {
            TempData["Error"] = "Thông tin cập nhật chuyến tàu không hợp lệ (giờ đến phải sau giờ đi).";
            return RedirectToAction(nameof(RoutesAndTrips));
        }

        trip.TrainCode = input.TrainCode.Trim().ToUpperInvariant();
        trip.RouteId = input.RouteId;
        trip.Departure = input.Departure;
        trip.Arrival = input.Arrival;
        trip.BaseFare = input.Fare;

        var validStatuses = new[] { "HoatDong", "TamNgung", "Huy" };
        if (validStatuses.Contains(input.Status))
        {
            trip.Status = input.Status;
        }

        await db.SaveChangesAsync();
        TempData["Success"] = $"Đã cập nhật chuyến tàu {trip.TrainCode}.";
        return RedirectToAction(nameof(RoutesAndTrips));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTripStatus(int id)
    {
        var trip = await db.Trips.FindAsync(id);
        if (trip is null)
        {
            return NotFound();
        }

        trip.Status = trip.Status == "HoatDong" ? "TamNgung" : "HoatDong";
        await db.SaveChangesAsync();

        TempData["Success"] = trip.Status == "HoatDong"
            ? $"Đã mở bán lại chuyến {trip.TrainCode}."
            : $"Đã tạm dừng chuyến {trip.TrainCode}.";
        return RedirectToAction(nameof(RoutesAndTrips));
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        return View(await db.Users.AsNoTracking().OrderBy(user => user.Id).ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUser(AdminUserInput input)
    {
        var user = await db.Users.FindAsync(input.Id);
        var validRoles = new[] { "Admin", "NhanVien", "KhachHang" };
        var validStatuses = new[] { "HoatDong", "BiKhoa" };
        if (user is not null && validRoles.Contains(input.Role) && validStatuses.Contains(input.Status))
        {
            user.Role = input.Role;
            user.Status = input.Status;
            await db.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật tài khoản.";
        }

        return RedirectToAction(nameof(Users));
    }
}