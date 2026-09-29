using BTL_TrainTicketApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BTL_TrainTicketApp.Data;

public static class SeatMapInitializer
{
    public static async Task InitializeAsync(TrainTicketDbContext db)
    {
        var trips = await db.Trips.Include(trip => trip.Cars).ToListAsync();
        var changed = false;

        foreach (var trip in trips)
        {
            if (trip.Cars.Count == 0)
            {
                var firstCarSeats = (trip.SeatCount + 1) / 2;
                var firstCar = new TrainCar
                {
                    TripId = trip.Id,
                    Number = 1,
                    Name = "Toa 1 - Ngồi mềm",
                    SeatType = "Ngồi mềm điều hòa",
                    SeatCount = firstCarSeats
                };
                var secondCar = new TrainCar
                {
                    TripId = trip.Id,
                    Number = 2,
                    Name = "Toa 2 - Giường nằm",
                    SeatType = "Giường nằm khoang 4",
                    SeatCount = trip.SeatCount - firstCarSeats
                };

                db.Cars.Add(firstCar);
                if (secondCar.SeatCount > 0)
                {
                    db.Cars.Add(secondCar);
                }

                await db.SaveChangesAsync();
                changed = true;
            }
        }

        var cars = await db.Cars.Include(car => car.Seats).ToListAsync();
        foreach (var car in cars)
        {
            var existingNumbers = car.Seats.Select(seat => seat.Number).ToHashSet();
            for (var number = 1; number <= car.SeatCount; number++)
            {
                if (!existingNumbers.Contains(number))
                {
                    db.Seats.Add(new Seat
                    {
                        CarId = car.Id,
                        Number = number,
                        Status = "Trong"
                    });
                    changed = true;
                }
            }

            foreach (var seat in car.Seats.Where(seat =>
                         seat.Status == "DangGiu" &&
                         (!seat.HeldAt.HasValue || seat.HeldAt < DateTime.Now.AddMinutes(-15))))
            {
                seat.Status = "Trong";
                seat.HeldAt = null;
                seat.HeldByUserId = null;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }
}
