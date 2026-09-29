using System.ComponentModel.DataAnnotations;

namespace BTL_TrainTicketApp.Models;

public sealed class DbUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Cccd { get; set; }
    public string Role { get; set; } = "KhachHang";
    public string Status { get; set; } = "HoatDong";
    public DateTime? Created { get; set; }
    public ICollection<Seat> HeldSeats { get; set; } = [];
    public ICollection<TrainTicket> Tickets { get; set; } = [];
}

public sealed class Station
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string Status { get; set; } = "HoatDong";
}

public sealed class RailRoute
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int StartStationId { get; set; }
    public int EndStationId { get; set; }
    public double DistanceKm { get; set; }
    public string Status { get; set; } = "HoatDong";
    public Station? StartStation { get; set; }
    public Station? EndStation { get; set; }
    public ICollection<TrainTrip> Trips { get; set; } = [];
}

public sealed class TrainTrip
{
    public int Id { get; set; }
    public string TrainCode { get; set; } = string.Empty;
    public int RouteId { get; set; }
    public DateTime Departure { get; set; }
    public DateTime Arrival { get; set; }
    public decimal BaseFare { get; set; }
    public int SeatCount { get; set; }
    public int AvailableSeats { get; set; }
    public string Status { get; set; } = "HoatDong";
    public RailRoute? Route { get; set; }
    public ICollection<TrainCar> Cars { get; set; } = [];
    public ICollection<TrainTicket> Tickets { get; set; } = [];
}

public sealed class TrainCar
{
    public int Id { get; set; }
    public int TripId { get; set; }
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SeatType { get; set; } = string.Empty;
    public int SeatCount { get; set; }
    public TrainTrip? Trip { get; set; }
    public ICollection<Seat> Seats { get; set; } = [];
}

public sealed class Seat
{
    public int Id { get; set; }
    public int CarId { get; set; }
    public int Number { get; set; }
    public string Status { get; set; } = "Trong";
    public DateTime? HeldAt { get; set; }
    public int? HeldByUserId { get; set; }
    public TrainCar? Car { get; set; }
    public DbUser? HeldByUser { get; set; }
    public ICollection<TrainTicket> Tickets { get; set; } = [];
}

public sealed class TrainTicket
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? QrCode { get; set; }
    public int UserId { get; set; }
    public int TripId { get; set; }
    public int SeatId { get; set; }
    public string PassengerName { get; set; } = string.Empty;
    public string PassengerCccd { get; set; } = string.Empty;
    public decimal Fare { get; set; }
    public string PaymentStatus { get; set; } = "ChoThanhToan";
    public DateTime? BookedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public DbUser? User { get; set; }
    public DbUser? ConfirmedByUser { get; set; }
    public TrainTrip? Trip { get; set; }
    public Seat? Seat { get; set; }
}

public sealed class LoginInput
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterInput
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(15)]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 9)]
    public string Cccd { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public sealed class PassengerInput
{
    public int TripId { get; set; }
    public int SeatId { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 9)]
    public string Cccd { get; set; } = string.Empty;
}

public sealed class SearchInput
{
    public int? FromStationId { get; set; }
    public int? ToStationId { get; set; }
    public DateTime? Date { get; set; }
}

public sealed class AdminUserInput
{
    public int Id { get; set; }
    public string Role { get; set; } = "KhachHang";
    public string Status { get; set; } = "HoatDong";
}

public sealed class RouteInput
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int StartStationId { get; set; }

    [Range(1, int.MaxValue)]
    public int EndStationId { get; set; }

    [Range(1, 5000)]
    public double DistanceKm { get; set; }
}

public sealed class TripInput
{
    [Required, StringLength(20)]
    public string TrainCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int RouteId { get; set; }

    public DateTime Departure { get; set; }
    public DateTime Arrival { get; set; }

    [Range(typeof(decimal), "1", "100000000")]
    public decimal Fare { get; set; }

    [Range(1, 500)]
    public int SeatCount { get; set; } = 40;
}

public sealed class TicketLookupInput
{
    [Required]
    public string Query { get; set; } = string.Empty;
}
