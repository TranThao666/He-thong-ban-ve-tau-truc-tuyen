using BTL_TrainTicketApp.Data;
using BTL_TrainTicketApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_TrainTicketApp.Controllers;

public sealed class HomeController(TrainTicketDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(SearchInput search)
    {
        ViewBag.Stations = await db.Stations.AsNoTracking()
            .Where(station => station.Status == "HoatDong")
            .OrderBy(station => station.Name)
            .ToListAsync();

        var query = db.Trips.AsNoTracking()
            .Include(trip => trip.Route)!
                .ThenInclude(route => route!.StartStation)
            .Include(trip => trip.Route)!
                .ThenInclude(route => route!.EndStation)
            .Where(trip => trip.Status == "HoatDong" && trip.AvailableSeats > 0);

        if (search.FromStationId.HasValue)
        {
            query = query.Where(trip => trip.Route!.StartStationId == search.FromStationId.Value);
        }

        if (search.ToStationId.HasValue)
        {
            query = query.Where(trip => trip.Route!.EndStationId == search.ToStationId.Value);
        }

        if (search.Date.HasValue)
        {
            var date = search.Date.Value.Date;
            query = query.Where(trip => trip.Departure.Date == date);
        }

        ViewBag.Search = search;
        return View(await query.OrderBy(trip => trip.Departure).ToListAsync());
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult ValidateRequired([FromBody] Dictionary<string, string?> fields)
    {
        var errors = fields
            .Where(field => string.IsNullOrWhiteSpace(field.Value))
            .ToDictionary(field => field.Key, _ => "Vui lòng nhập thông tin này.");

        return Json(new { valid = errors.Count == 0, errors });
    }

    [HttpGet]
    public IActionResult Error() => View();
}
