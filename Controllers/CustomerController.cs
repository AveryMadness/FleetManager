using FleetManager.Data;
using FleetManager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Controllers;

[Authorize]
public class CustomerController : Controller
{
    private readonly AppDbContext _db;
    public CustomerController(AppDbContext db) => _db = db;

    // GET /Customer
    public async Task<IActionResult> Index(string? search, CustomerStatus? status)
    {
        var query = _db.Customers.AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(c =>
                c.FirstName.Contains(search) ||
                c.LastName.Contains(search)  ||
                c.Email.Contains(search)     ||
                c.LicenseNumber.Contains(search));

        if (status.HasValue)
            query = query.Where(c => c.Status == status);

        ViewBag.Search = search;
        ViewBag.StatusFilter = status;
        return View(await query.OrderBy(c => c.LastName).ToListAsync());
    }

    // GET /Customer/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var customer = await _db.Customers
            .Include(c => c.Reservations)
                .ThenInclude(r => r.Vehicle)
            .Include(c => c.Reservations)
                .ThenInclude(r => r.Invoice)
            .FirstOrDefaultAsync(c => c.Id == id);

        return customer is null ? NotFound() : View(customer);
    }

    // GET /Customer/Create
    [Authorize(Policy = "Staff")]
    public IActionResult Create() => View(new Customer());

    // POST /Customer/Create
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Staff")]
    public async Task<IActionResult> Create(Customer customer)
    {
        if (await _db.Customers.AnyAsync(c => c.Email == customer.Email))
            ModelState.AddModelError("Email", "A customer with this email already exists.");

        if (!ModelState.IsValid) return View(customer);

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Customer added.";
        return RedirectToAction(nameof(Index));
    }

    // GET /Customer/Edit/5
    [Authorize(Policy = "Staff")]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        return customer is null ? NotFound() : View(customer);
    }

    // POST /Customer/Edit/5
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Staff")]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.Id) return BadRequest();
        if (!ModelState.IsValid) return View(customer);

        try
        {
            _db.Update(customer);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Customer updated.";
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _db.Customers.AnyAsync(c => c.Id == id)) return NotFound();
            throw;
        }

        return RedirectToAction(nameof(Index));
    }

    // POST /Customer/Delete/5
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();

        var hasActive = await _db.Reservations
            .AnyAsync(r => r.CustomerId == id && r.Status == ReservationStatus.Active);

        if (hasActive)
        {
            TempData["Error"] = "Cannot delete a customer with active reservations.";
            return RedirectToAction(nameof(Index));
        }

        customer.Status = CustomerStatus.Inactive;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Customer deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // GET /Customer/Search  (AJAX autocomplete)
    public async Task<IActionResult> Search(string term)
    {
        var results = await _db.Customers
            .Where(c => c.Status == CustomerStatus.Active &&
                       (c.FirstName.Contains(term) || c.LastName.Contains(term) || c.Email.Contains(term)))
            .Select(c => new { c.Id, Name = c.FirstName + " " + c.LastName, c.Email })
            .Take(10)
            .ToListAsync();

        return Json(results);
    }
}
