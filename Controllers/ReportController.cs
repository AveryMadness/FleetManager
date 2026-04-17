using FleetManager.Data;
using FleetManager.Models;
using FleetManager.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace FleetManager.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly AppDbContext _db;
    public ReportController(AppDbContext db) => _db = db;

    // GET /Report
    public IActionResult Index() => View(new ReportFilterViewModel
    {
        StartDate  = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
        EndDate    = DateTime.Today,
        ReportType = "ReservationSummary"
    });

    // POST /Report
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ReportFilterViewModel filter)
    {
        var vm = new ReportResultViewModel { Filter = filter };

        var start = filter.StartDate ?? DateTime.MinValue;
        var end   = (filter.EndDate ?? DateTime.Today).AddDays(1);

        vm.Rows = filter.ReportType switch
        {
            "ReservationSummary" => await ReservationSummary(start, end),
            "Revenue"            => await RevenueReport(start, end),
            "FleetUtilization"   => await FleetUtilization(start, end),
            "CustomerActivity"   => await CustomerActivity(start, end),
            "BillingTax"         => await BillingTax(start, end),
            "LateReturns"        => await LateReturns(start, end),
            _                    => new List<ReportRow>()
        };

        vm.Total = vm.Rows.Sum(r => r.Amount);
        return View("Result", vm);
    }

    // GET /Report/Export?type=Revenue&start=...&end=...
    public async Task<IActionResult> Export(string type, DateTime? start, DateTime? end)
    {
        var s = start ?? DateTime.MinValue;
        var e = (end ?? DateTime.Today).AddDays(1);

        var rows = type switch
        {
            "ReservationSummary" => await ReservationSummary(s, e),
            "Revenue"            => await RevenueReport(s, e),
            "FleetUtilization"   => await FleetUtilization(s, e),
            "CustomerActivity"   => await CustomerActivity(s, e),
            "BillingTax"         => await BillingTax(s, e),
            "LateReturns"        => await LateReturns(s, e),
            _                    => new List<ReportRow>()
        };

        var csv = new StringBuilder();
        csv.AppendLine("Label,Count,Amount");
        foreach (var r in rows)
            csv.AppendLine($"\"{r.Label}\",{r.Count},{r.Amount:F2}");

        return File(Encoding.UTF8.GetBytes(csv.ToString()),
                    "text/csv",
                    $"{type}_{DateTime.Today:yyyyMMdd}.csv");
    }

    private async Task<List<ReportRow>> ReservationSummary(DateTime s, DateTime e)
    {
        var reservations = await _db.Reservations
            .Where(r => r.CreatedAt >= s && r.CreatedAt < e)
            .ToListAsync();

        return reservations
            .GroupBy(r => r.Status.ToString())
            .Select(g => new ReportRow { Label = g.Key, Count = g.Count(), Amount = 0 })
            .ToList();
    }

    private async Task<List<ReportRow>> RevenueReport(DateTime s, DateTime e)
    {
        var invoices = await _db.Invoices
            .Where(i => i.IssuedAt >= s && i.IssuedAt < e && i.PaymentStatus == PaymentStatus.Paid)
            .ToListAsync();

        return invoices
            .GroupBy(i => i.PaidAt?.ToString("yyyy-MM") ?? i.IssuedAt.ToString("yyyy-MM"))
            .Select(g => new ReportRow { Label = g.Key, Count = g.Count(), Amount = g.Sum(i => i.TotalAmount) })
            .OrderBy(r => r.Label)
            .ToList();
    }

    private async Task<List<ReportRow>> FleetUtilization(DateTime s, DateTime e)
    {
        var vehicles = await _db.Vehicles
            .Include(v => v.Reservations)
            .Include(v => v.Category)
            .ToListAsync();

        return vehicles.Select(v =>
        {
            var rentedDays = v.Reservations
                .Where(r => r.Status == ReservationStatus.Completed &&
                            r.StartDate >= s && r.EndDate < e)
                .Sum(r => r.TotalDays);
            return new ReportRow
            {
                Label  = $"{v.Year} {v.Make} {v.Model} ({v.LicensePlate})",
                Count  = rentedDays,
                Amount = rentedDays * v.DailyRate
            };
        }).OrderByDescending(r => r.Count).ToList();
    }

    private async Task<List<ReportRow>> CustomerActivity(DateTime s, DateTime e)
    {
        return await _db.Customers
            .Select(c => new ReportRow
            {
                Label  = c.FirstName + " " + c.LastName,
                Count  = c.Reservations.Count(r => r.CreatedAt >= s && r.CreatedAt < e),
                Amount = c.Reservations
                    .Where(r => r.CreatedAt >= s && r.CreatedAt < e && r.Invoice != null)
                    .Sum(r => r.Invoice!.TotalAmount)
            })
            .OrderByDescending(r => r.Count)
            .ToListAsync();
    }

    private async Task<List<ReportRow>> BillingTax(DateTime s, DateTime e)
    {
        var invoices = await _db.Invoices
            .Where(i => i.IssuedAt >= s && i.IssuedAt < e)
            .ToListAsync();

        return new List<ReportRow>
        {
            new() { Label = "Base revenue",       Count = invoices.Count, Amount = invoices.Sum(i => i.BaseAmount) },
            new() { Label = "Additional charges", Count = 0,              Amount = 0 },
            new() { Label = "Discounts",          Count = 0,              Amount = -invoices.Sum(i => i.Discount) },
            new() { Label = "Tax collected",      Count = 0,              Amount = invoices.Sum(i => i.TaxAmount) },
            new() { Label = "Total invoiced",     Count = invoices.Count, Amount = invoices.Sum(i => i.TotalAmount) },
        };
    }

    private async Task<List<ReportRow>> LateReturns(DateTime s, DateTime e)
    {
        var late = await _db.Reservations
            .Include(r => r.Customer)
            .Include(r => r.Vehicle)
            .Where(r => r.EndDate < DateTime.Today &&
                        r.Status == ReservationStatus.Active &&
                        r.EndDate >= s && r.EndDate < e)
            .ToListAsync();

        return late.Select(r => new ReportRow
        {
            Label  = $"Res #{r.Id} – {r.Customer?.FullName} – {r.Vehicle?.Make} {r.Vehicle?.Model}",
            Count  = (int)(DateTime.Today - r.EndDate).TotalDays,
            Amount = (DateTime.Today - r.EndDate).Days * (r.Vehicle?.DailyRate ?? 0)
        }).ToList();
    }
}
