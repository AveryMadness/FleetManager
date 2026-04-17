using FleetManager.Data;
using FleetManager.Models;
using FleetManager.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var now       = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var vm = new DashboardViewModel
        {
            TotalVehicles      = await _db.Vehicles.CountAsync(),
            AvailableVehicles  = await _db.Vehicles.CountAsync(v => v.Status == VehicleStatus.Available),
            ActiveReservations = await _db.Reservations.CountAsync(r => r.Status == ReservationStatus.Active),
            MonthlyRevenue     = await _db.Invoices
                                    .Where(i => i.IssuedAt >= monthStart && i.PaymentStatus == PaymentStatus.Paid)
                                    .SumAsync(i => (decimal?)i.TotalAmount) ?? 0,
            OutstandingDebt    = await _db.Invoices
                                    .Where(i => i.PaymentStatus == PaymentStatus.Unpaid || i.PaymentStatus == PaymentStatus.Overdue)
                                    .SumAsync(i => (decimal?)i.TotalAmount) ?? 0,
            RecentReservations = await _db.Reservations
                                    .Include(r => r.Customer)
                                    .Include(r => r.Vehicle)
                                    .OrderByDescending(r => r.CreatedAt)
                                    .Take(8)
                                    .ToListAsync()
        };

        // Last 7 days — reservations per day
        for (int i = 6; i >= 0; i--)
        {
            var day   = now.AddDays(-i).Date;
            var count = await _db.Reservations.CountAsync(r => r.CreatedAt.Date == day);
            vm.WeeklyStats.Add(new DailyStatPoint { Day = day.ToString("ddd"), Count = count });
        }

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
