using System.Security.Claims;
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

        var ticket = await TicketDetails().FirstOrDefaultAsync(item =>
            item.Code == input.Query || item.PassengerCccd == input.Query);
        if (ticket is null)
        {
            ModelState.AddModelError(nameof(input.Query), "Không tìm thấy vé khớp với mã vé hoặc CCCD.");
        }

        ViewBag.Ticket = ticket;
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id)
    {
        var ticket = await db.Tickets.Include(item => item.Seat)
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
        }

        return RedirectToAction(nameof(Lookup));
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
    public async Task<IActionResult> Dashboard()
    {
        var paid = await db.Tickets.CountAsync(ticket => ticket.PaymentStatus == "DaThanhToan");
        var cancelled = await db.Tickets.CountAsync(ticket => ticket.PaymentStatus == "DaHuy");
        ViewBag.Users = await db.Users.CountAsync();
        ViewBag.Trips = await db.Trips.CountAsync();
        ViewBag.Paid = paid;
        ViewBag.Cancelled = cancelled;
        ViewBag.Revenue = await db.Tickets
            .Where(ticket => ticket.PaymentStatus == "DaThanhToan")
            .SumAsync(ticket => (decimal?)ticket.Fare) ?? 0;
        ViewBag.RecentTrips = await db.Trips.AsNoTracking()
            .Include(trip => trip.Route)!
                .ThenInclude(route => route!.StartStation)
            .Include(trip => trip.Route)!
                .ThenInclude(route => route!.EndStation)
            .OrderBy(trip => trip.Departure)
            .Take(8)
            .ToListAsync();
        return View();
    }

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
            .OrderBy(trip => trip.Departure)
            .ToListAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRoute(RouteInput input)
    {
        if (input.StartStationId == input.EndStationId)
        {
            ModelState.AddModelError(nameof(input.EndStationId), "Ga đến phải khác ga đi.");
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
            ModelState.AddModelError(nameof(input.Arrival), "Giờ đến phải sau giờ khởi hành.");
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
