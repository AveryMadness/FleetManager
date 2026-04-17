using FleetManager.Data;
using FleetManager.Models;
using FleetManager.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace FleetManager.Controllers;

[Authorize]
public class BillingController : Controller
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public BillingController(AppDbContext db, IConfiguration config)
    {
        _db     = db;
        _config = config;
    }

    // GET /Billing
    public async Task<IActionResult> Index(PaymentStatus? status)
    {
        var query = _db.Invoices
            .Include(i => i.Reservation).ThenInclude(r => r!.Customer)
            .Include(i => i.Reservation).ThenInclude(r => r!.Vehicle)
            .Include(i => i.AdditionalCharges)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(i => i.PaymentStatus == status);

        var invoices = await query.OrderByDescending(i => i.IssuedAt).ToListAsync();
        var now      = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var vm = new BillingViewModel
        {
            Invoices         = invoices,
            OutstandingTotal = invoices
                .Where(i => i.PaymentStatus is PaymentStatus.Unpaid or PaymentStatus.Overdue)
                .Sum(i => i.TotalAmount),
            CollectedMonth   = invoices
                .Where(i => i.PaymentStatus == PaymentStatus.Paid && i.PaidAt >= monthStart)
                .Sum(i => i.TotalAmount),
            AverageInvoice   = invoices.Count > 0 ? invoices.Average(i => i.TotalAmount) : 0
        };

        ViewBag.StatusFilter = status;
        return View(vm);
    }

    // GET /Billing/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Reservation).ThenInclude(r => r!.Customer)
            .Include(i => i.Reservation).ThenInclude(r => r!.Vehicle)
            .Include(i => i.AdditionalCharges)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice is null) return NotFound();

        ViewBag.StripePublishableKey = _config["Stripe:PublishableKey"];
        return View(invoice);
    }

    // POST /Billing/AddCharge
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Staff")]
    public async Task<IActionResult> AddCharge(int invoiceId, string description, decimal amount)
    {
        var invoice = await _db.Invoices
            .Include(i => i.AdditionalCharges)
            .Include(i => i.Reservation).ThenInclude(r => r!.Vehicle)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice is null) return NotFound();

        invoice.AdditionalCharges.Add(new AdditionalCharge { Description = description, Amount = amount });
        invoice.Recalculate();
        await _db.SaveChangesAsync();

        TempData["Success"] = "Charge added.";
        return RedirectToAction(nameof(Details), new { id = invoiceId });
    }

    // POST /Billing/RemoveCharge
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Staff")]
    public async Task<IActionResult> RemoveCharge(int chargeId, int invoiceId)
    {
        var charge  = await _db.AdditionalCharges.FindAsync(chargeId);
        if (charge is not null)
        {
            _db.AdditionalCharges.Remove(charge);
            await _db.SaveChangesAsync();

            var invoice = await _db.Invoices
                .Include(i => i.AdditionalCharges)
                .Include(i => i.Reservation).ThenInclude(r => r!.Vehicle)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice is not null) { invoice.Recalculate(); await _db.SaveChangesAsync(); }
        }

        TempData["Success"] = "Charge removed.";
        return RedirectToAction(nameof(Details), new { id = invoiceId });
    }

    // POST /Billing/MarkPaid
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Staff")]
    public async Task<IActionResult> MarkPaid(int id)
    {
        var invoice = await _db.Invoices.FindAsync(id);
        if (invoice is null) return NotFound();

        invoice.PaymentStatus = PaymentStatus.Paid;
        invoice.PaidAt        = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Invoice marked as paid.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public class PaymentIntentRequest
    {
        public int InvoiceId { get; set; }
    }

    [HttpPost]
    [Route("Billing/CreateStripePaymentIntent")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreateStripePaymentIntent([FromBody] PaymentIntentRequest request)
    {
        var invoice = await _db.Invoices.FindAsync(request.InvoiceId);
        if (invoice is null) return NotFound();

        var options = new PaymentIntentCreateOptions
        {
            Amount   = (long)(invoice.TotalAmount * 100),  // cents
            Currency = "usd",
            Metadata = new Dictionary<string, string> { { "invoiceId", request.InvoiceId.ToString() } }
        };

        var service = new PaymentIntentService();
        var intent  = await service.CreateAsync(options);

        invoice.StripePaymentIntentId = intent.Id;
        await _db.SaveChangesAsync();

        return Json(new { clientSecret = intent.ClientSecret });
    }

    // POST /Billing/StripeWebhook  (Stripe calls this)
    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> StripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                _config["Stripe:WebhookSecret"]);

            if (stripeEvent.Type == Events.PaymentIntentSucceeded)
            {
                var intent  = (PaymentIntent)stripeEvent.Data.Object;
                var invoiceId = int.Parse(intent.Metadata["invoiceId"]);
                var invoice   = await _db.Invoices.FindAsync(invoiceId);
                if (invoice is not null)
                {
                    invoice.PaymentStatus = PaymentStatus.Paid;
                    invoice.PaidAt        = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }
        }
        catch (StripeException) { return BadRequest(); }

        return Ok();
    }
}
