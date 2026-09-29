using System.Security.Claims;
using System.Security.Cryptography;
using BTL_TrainTicketApp.Data;
using BTL_TrainTicketApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_TrainTicketApp.Controllers;

[Authorize(Roles = "KhachHang,Admin,NhanVien")]
public sealed class BookingController(TrainTicketDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Seats(int tripId)
    {
        var trip = await TripQuery().FirstOrDefaultAsync(item => item.Id == tripId);
        if (trip is null)
        {
            return NotFound();
        }

        ViewBag.Trip = trip;
        ViewBag.Cars = await db.Cars.AsNoTracking()
            .Where(car => car.TripId == tripId)
            .Include(car => car.Seats)
            .OrderBy(car => car.Number)
            .ToListAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hold(int tripId, int seatId)
    {
        var userId = CurrentUserId();
        var seat = await db.Seats.Include(item => item.Car)
            .FirstOrDefaultAsync(item => item.Id == seatId && item.Car!.TripId == tripId);
        if (seat is null)
        {
            return NotFound();
        }

        var expired = seat.Status == "DangGiu" &&
                      (!seat.HeldAt.HasValue || seat.HeldAt < DateTime.Now.AddMinutes(-15));
        if (seat.Status == "DaBan" || (seat.Status == "DangGiu" && !expired && seat.HeldByUserId != userId))
        {
            TempData["Error"] = "Ghế này vừa được người khác chọn. Vui lòng chọn ghế khác.";
            return RedirectToAction(nameof(Seats), new { tripId });
        }

        seat.Status = "DangGiu";
        seat.HeldAt = DateTime.Now;
        seat.HeldByUserId = userId;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Passenger), new { tripId, seatId });
    }

    [HttpGet]
    public async Task<IActionResult> Passenger(int tripId, int seatId)
    {
        if (!await IsSeatHeldByUser(tripId, seatId))
        {
            return RedirectToAction(nameof(Seats), new { tripId });
        }

        var user = await db.Users.FindAsync(CurrentUserId());
        ViewBag.Trip = await TripQuery().FirstOrDefaultAsync(trip => trip.Id == tripId);
        ViewBag.Seat = await SeatQuery().FirstOrDefaultAsync(seat => seat.Id == seatId);
        return View(new PassengerInput
        {
            TripId = tripId,
            SeatId = seatId,
            Name = user?.Name ?? string.Empty,
            Cccd = user?.Cccd ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Passenger(PassengerInput input)
    {
        var seat = await db.Seats.Include(item => item.Car)
            .FirstOrDefaultAsync(item => item.Id == input.SeatId && item.Car!.TripId == input.TripId);
        if (seat is null || seat.Status != "DangGiu" || seat.HeldByUserId != CurrentUserId() ||
            !seat.HeldAt.HasValue || seat.HeldAt < DateTime.Now.AddMinutes(-15))
        {
            ModelState.AddModelError(string.Empty, "Thời gian giữ ghế đã hết. Hãy chọn lại ghế.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Trip = await TripQuery().FirstOrDefaultAsync(trip => trip.Id == input.TripId);
            ViewBag.Seat = await SeatQuery().FirstOrDefaultAsync(item => item.Id == input.SeatId);
            return View(input);
        }

        var trip = await db.Trips.FindAsync(input.TripId);
        if (trip is null)
        {
            return NotFound();
        }

        var ticket = new TrainTicket
        {
            Code = $"VE-{trip.TrainCode}-{DateTime.Now:yyMMddHHmmss}-{RandomNumberGenerator.GetInt32(100, 999)}",
            QrCode = $"BTL:{trip.Id}:{input.SeatId}:{CurrentUserId()}",
            UserId = CurrentUserId(),
            TripId = input.TripId,
            SeatId = input.SeatId,
            PassengerName = input.Name.Trim(),
            PassengerCccd = input.Cccd.Trim(),
            Fare = trip.BaseFare,
            PaymentStatus = "ChoThanhToan",
            BookedAt = DateTime.Now
        };

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Payment), new { id = ticket.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Payment(int id)
    {
        var ticket = await TicketQuery().FirstOrDefaultAsync(item => item.Id == id && item.UserId == CurrentUserId());
        return ticket is null ? NotFound() : View(ticket);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id, string method)
    {
        var ticket = await db.Tickets
            .Include(item => item.Seat)
            .Include(item => item.Trip)
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == CurrentUserId());
        if (ticket is null || ticket.PaymentStatus != "ChoThanhToan")
        {
            return NotFound();
        }

        if (ticket.Seat is null || ticket.Seat.Status != "DangGiu" || ticket.Seat.HeldByUserId != CurrentUserId() ||
            !ticket.Seat.HeldAt.HasValue || ticket.Seat.HeldAt < DateTime.Now.AddMinutes(-15))
        {
            TempData["Error"] = "Thời gian giữ ghế đã hết. Vui lòng đặt lại.";
            return RedirectToAction(nameof(Seats), new { tripId = ticket.TripId });
        }

        ticket.PaymentStatus = "DaThanhToan";
        ticket.PaidAt = DateTime.Now;
        ticket.Seat.Status = "DaBan";
        ticket.Seat.HeldAt = null;
        ticket.Seat.HeldByUserId = null;
        if (ticket.Trip is not null)
        {
            ticket.Trip.AvailableSeats = Math.Max(0, ticket.Trip.AvailableSeats - 1);
        }

        await db.SaveChangesAsync();
        TempData["PaymentMethod"] = method;
        return RedirectToAction(nameof(ElectronicTicket), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> ElectronicTicket(int id)
    {
        var ticket = await TicketQuery().FirstOrDefaultAsync(item => item.Id == id && item.UserId == CurrentUserId());
        return ticket is null ? NotFound() : View(ticket);
    }

    [HttpGet]
    public async Task<IActionResult> MyTickets()
    {
        var tickets = await TicketQuery()
            .Where(ticket => ticket.UserId == CurrentUserId())
            .OrderByDescending(ticket => ticket.BookedAt)
            .ToListAsync();
        return View(tickets);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelTicket(int id)
    {
        var ticket = await db.Tickets
            .Include(item => item.Seat)
            .Include(item => item.Trip)
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == CurrentUserId());
        if (ticket is null || ticket.PaymentStatus == "DaHuy")
        {
            return NotFound();
        }

        ticket.PaymentStatus = "DaHuy";
        if (ticket.Seat is not null)
        {
            ticket.Seat.Status = "Trong";
            ticket.Seat.HeldAt = null;
            ticket.Seat.HeldByUserId = null;
        }

        if (ticket.Trip is not null)
        {
            ticket.Trip.AvailableSeats = Math.Min(ticket.Trip.SeatCount, ticket.Trip.AvailableSeats + 1);
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "Vé đã được hủy.";
        return RedirectToAction(nameof(MyTickets));
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private Task<bool> IsSeatHeldByUser(int tripId, int seatId) => db.Seats
        .AnyAsync(seat => seat.Id == seatId && seat.Car!.TripId == tripId && seat.Status == "DangGiu" &&
                          seat.HeldByUserId == CurrentUserId() && seat.HeldAt.HasValue &&
                          seat.HeldAt >= DateTime.Now.AddMinutes(-15));

    private IQueryable<TrainTrip> TripQuery() => db.Trips.AsNoTracking()
        .Include(trip => trip.Route)!
            .ThenInclude(route => route!.StartStation)
        .Include(trip => trip.Route)!
            .ThenInclude(route => route!.EndStation);

    private IQueryable<Seat> SeatQuery() => db.Seats.AsNoTracking().Include(seat => seat.Car);

    private IQueryable<TrainTicket> TicketQuery() => db.Tickets.AsNoTracking()
        .Include(ticket => ticket.Trip)!
            .ThenInclude(trip => trip!.Route)!
                .ThenInclude(route => route!.StartStation)
        .Include(ticket => ticket.Trip)!
            .ThenInclude(trip => trip!.Route)!
                .ThenInclude(route => route!.EndStation)
        .Include(ticket => ticket.Seat)!
            .ThenInclude(seat => seat!.Car);
}
