using FleetManager.Data;
using FleetManager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Controllers;

[Authorize]
public class ReservationController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public ReservationController(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db    = db;
        _users = users;
    }

    // GET /Reservation
    public async Task<IActionResult> Index(
        string? search, ReservationStatus? status,
        DateTime? startDate, DateTime? endDate)
    {
        var query = _db.Reservations
            .Include(r => r.Customer)
            .Include(r => r.Vehicle)
            .Include(r => r.Invoice)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(r =>
                r.Customer!.FirstName.Contains(search) ||
                r.Customer!.LastName.Contains(search)  ||
                r.Vehicle!.LicensePlate.Contains(search));

        if (status.HasValue)
            query = query.Where(r => r.Status == status);

        if (startDate.HasValue)
            query = query.Where(r => r.StartDate >= startDate);

        if (endDate.HasValue)
            query = query.Where(r => r.EndDate <= endDate);

        ViewBag.Search      = search;
        ViewBag.StatusFilter = status;
        ViewBag.StartDate   = startDate?.ToString("yyyy-MM-dd");
        ViewBag.EndDate     = endDate?.ToString("yyyy-MM-dd");

        return View(await query.OrderByDescending(r => r.CreatedAt).ToListAsync());
    }

    // GET /Reservation/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Customer)
            .Include(r => r.Vehicle).ThenInclude(v => v!.Category)
            .Include(r => r.Invoice).ThenInclude(i => i!.AdditionalCharges)
            .Include(r => r.HandledBy)
            .FirstOrDefaultAsync(r => r.Id == id);

        return reservation is null ? NotFound() : View(reservation);
    }

    // GET /Reservation/Create
    [Authorize(Policy = "Staff")]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View(new Reservation { StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(1) });
    }

    // POST /Reservation/Create
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Staff")]
    public async Task<IActionResult> Create(Reservation reservation)
    {
        if (reservation.EndDate <= reservation.StartDate)
            ModelState.AddModelError("EndDate", "Return date must be after pick-up date.");

        if (!await IsVehicleAvailable(reservation.VehicleId, reservation.StartDate, reservation.EndDate))
            ModelState.AddModelError("VehicleId", "This vehicle is not available for the selected dates.");

        if (!ModelState.IsValid) { await PopulateDropdowns(); return View(reservation); }

        reservation.HandledById = _users.GetUserId(User);
        reservation.Status      = ReservationStatus.Active;

        var vehicle = await _db.Vehicles.FindAsync(reservation.VehicleId);
        if (vehicle is not null)
            vehicle.Status = VehicleStatus.Rented;

        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();

        var dailyRate = vehicle?.DailyRate ?? 0;
        var days      = reservation.TotalDays;
        var base_     = dailyRate * days;
        var tax       = base_ * 0.13m;

        var invoice = new Invoice
        {
            ReservationId = reservation.Id,
            BaseAmount    = base_,
            TaxAmount     = tax,
            TotalAmount   = base_ + tax,
            Discount      = 0
        };
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Reservation #{reservation.Id} created. Invoice generated.";
        return RedirectToAction(nameof(Details), new { id = reservation.Id });
    }

    // GET /Reservation/Edit/5
    [Authorize(Policy = "Staff")]
    public async Task<IActionResult> Edit(int id)
    {
        var reservation = await _db.Reservations.FindAsync(id);
        if (reservation is null) return NotFound();
        await PopulateDropdowns(reservation.CustomerId, reservation.VehicleId);
        return View(reservation);
    }

    // POST /Reservation/Edit/5
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Staff")]
    public async Task<IActionResult> Edit(int id, Reservation reservation)
    {
        if (id != reservation.Id) return BadRequest();

        if (reservation.EndDate <= reservation.StartDate)
            ModelState.AddModelError("EndDate", "Return date must be after pick-up date.");

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns(reservation.CustomerId, reservation.VehicleId);
            return View(reservation);
        }

        try
        {
            _db.Update(reservation);
            await _db.SaveChangesAsync();

            // Recalculate invoice
            var invoice = await _db.Invoices
                .Include(i => i.AdditionalCharges)
                .Include(i => i.Reservation).ThenInclude(r => r!.Vehicle)
                .FirstOrDefaultAsync(i => i.ReservationId == id);

            if (invoice is not null)
            {
                invoice.Recalculate();
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Reservation updated.";
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _db.Reservations.AnyAsync(r => r.Id == id)) return NotFound();
            throw;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /Reservation/Cancel/5
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Staff")]
    public async Task<IActionResult> Cancel(int id)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation is null) return NotFound();

        reservation.Status = ReservationStatus.Cancelled;

        if (reservation.Vehicle is not null)
            reservation.Vehicle.Status = VehicleStatus.Available;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Reservation cancelled.";
        return RedirectToAction(nameof(Index));
    }

    // POST /Reservation/Complete/5
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Staff")]
    public async Task<IActionResult> Complete(int id, int mileageReturned)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation is null) return NotFound();

        reservation.Status = ReservationStatus.Completed;

        if (reservation.Vehicle is not null)
        {
            reservation.Vehicle.Status  = VehicleStatus.Available;
            reservation.Vehicle.Mileage = mileageReturned;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Reservation marked as completed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET /Reservation/CheckAvailability (AJAX)
    public async Task<IActionResult> CheckAvailability(int vehicleId, DateTime start, DateTime end)
    {
        var available = await IsVehicleAvailable(vehicleId, start, end);
        return Json(new { available });
    }

    private async Task<bool> IsVehicleAvailable(int vehicleId, DateTime start, DateTime end, int? excludeId = null)
    {
        var query = _db.Reservations.Where(r =>
            r.VehicleId == vehicleId &&
            r.Status    != ReservationStatus.Cancelled &&
            r.StartDate <  end &&
            r.EndDate   >  start);

        if (excludeId.HasValue)
            query = query.Where(r => r.Id != excludeId);

        return !await query.AnyAsync();
    }

    private async Task PopulateDropdowns(int? customerId = null, int? vehicleId = null)
    {
        ViewBag.Customers = new SelectList(
            await _db.Customers
                .Where(c => c.Status == CustomerStatus.Active)
                .OrderBy(c => c.LastName)
                .ToListAsync(),
            "Id", "FullName", customerId);

        ViewBag.Vehicles = new SelectList(
            await _db.Vehicles
                .Where(v => v.Status == VehicleStatus.Available || v.Id == vehicleId)
                .Include(v => v.Category)
                .OrderBy(v => v.Make)
                .Select(v => new { v.Id, Label = $"{v.Year} {v.Make} {v.Model} ({v.LicensePlate}) — ${v.DailyRate}/day" })
                .ToListAsync(),
            "Id", "Label", vehicleId);
    }
}
