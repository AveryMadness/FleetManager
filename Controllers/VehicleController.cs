using FleetManager.Data;
using FleetManager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Controllers;

[Authorize]
public class VehicleController : Controller
{
    private readonly AppDbContext _db;

    public VehicleController(AppDbContext db) => _db = db;

    // GET /Vehicle
    public async Task<IActionResult> Index(string? search, int? categoryId, VehicleStatus? status)
    {
        var query = _db.Vehicles.Include(v => v.Category).AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(v => v.Make.Contains(search) || v.Model.Contains(search) || v.LicensePlate.Contains(search));

        if (categoryId.HasValue)
            query = query.Where(v => v.CategoryId == categoryId);

        if (status.HasValue)
            query = query.Where(v => v.Status == status);

        ViewBag.Categories = new SelectList(await _db.VehicleCategories.ToListAsync(), "Id", "Name", categoryId);
        ViewBag.Search      = search;
        ViewBag.StatusFilter = status;

        return View(await query.OrderBy(v => v.Make).ToListAsync());
    }

    // GET /Vehicle/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var vehicle = await _db.Vehicles
            .Include(v => v.Category)
            .Include(v => v.Reservations).ThenInclude(r => r.Customer)
            .FirstOrDefaultAsync(v => v.Id == id);

        return vehicle is null ? NotFound() : View(vehicle);
    }

    // GET /Vehicle/Create
    [Authorize(Policy = "Staff")]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View(new Vehicle());
    }

    // POST /Vehicle/Create
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Staff")]
    public async Task<IActionResult> Create(Vehicle vehicle)
    {
        if (!ModelState.IsValid) { await PopulateDropdowns(); return View(vehicle); }

        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Vehicle added successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET /Vehicle/Edit/5
    [Authorize(Policy = "Staff")]
    public async Task<IActionResult> Edit(int id)
    {
        var vehicle = await _db.Vehicles.FindAsync(id);
        if (vehicle is null) return NotFound();
        await PopulateDropdowns(vehicle.CategoryId);
        return View(vehicle);
    }

    // POST /Vehicle/Edit/5
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Staff")]
    public async Task<IActionResult> Edit(int id, Vehicle vehicle)
    {
        if (id != vehicle.Id) return BadRequest();
        if (!ModelState.IsValid) { await PopulateDropdowns(vehicle.CategoryId); return View(vehicle); }

        try
        {
            _db.Update(vehicle);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Vehicle updated.";
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _db.Vehicles.AnyAsync(v => v.Id == id)) return NotFound();
            throw;
        }

        return RedirectToAction(nameof(Index));
    }

    // POST /Vehicle/Delete/5
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(int id)
    {
        var vehicle = await _db.Vehicles.FindAsync(id);
        if (vehicle is null) return NotFound();

        vehicle.Status = VehicleStatus.Retired;  // soft-delete via status
        await _db.SaveChangesAsync();
        TempData["Success"] = "Vehicle removed from fleet.";
        return RedirectToAction(nameof(Index));
    }

    // POST /Vehicle/UpdateStatus  (AJAX)
    [HttpPost, Authorize(Policy = "Staff")]
    public async Task<IActionResult> UpdateStatus(int id, VehicleStatus status)
    {
        var vehicle = await _db.Vehicles.FindAsync(id);
        if (vehicle is null) return NotFound();
        vehicle.Status = status;
        await _db.SaveChangesAsync();
        return Ok();
    }

    private async Task PopulateDropdowns(int? selectedCategory = null)
    {
        ViewBag.Categories = new SelectList(
            await _db.VehicleCategories.ToListAsync(), "Id", "Name", selectedCategory);
    }
}
