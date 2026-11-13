using BTL_TrainTicketApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BTL_TrainTicketApp.Services;

public sealed class SeatReleaseBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<SeatReleaseBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReleaseExpiredSeatsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi tự động nhả ghế giữ quá hạn.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ReleaseExpiredSeatsAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrainTicketDbContext>();
        var threshold = DateTime.Now.AddMinutes(-15);

        var expiredSeats = await db.Seats
            .Where(s => s.Status == "DangGiu" && (!s.HeldAt.HasValue || s.HeldAt < threshold))
            .ToListAsync(ct);

        if (expiredSeats.Count == 0) return;

        var expiredSeatIds = expiredSeats.Select(s => s.Id).ToList();

        // Hủy luôn các vé đang ChoThanhToan gắn với ghế hết hạn giữ
        var unpaidTickets = await db.Tickets
            .Where(t => expiredSeatIds.Contains(t.SeatId) && t.PaymentStatus == "ChoThanhToan")
            .ToListAsync(ct);

        foreach (var ticket in unpaidTickets)
        {
            ticket.PaymentStatus = "DaHuy";
        }

        foreach (var seat in expiredSeats)
        {
            seat.Status = "Trong";
            seat.HeldAt = null;
            seat.HeldByUserId = null;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã tự động nhả {Count} ghế giữ quá 15 phút.", expiredSeats.Count);
    }
}