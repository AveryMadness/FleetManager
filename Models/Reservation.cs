using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FleetManager.Models;

public enum ReservationStatus { Pending, Active, Completed, Cancelled }

public class Reservation
{
    public int Id { get; set; }

    [Required]
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [Required]
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public string? HandledById { get; set; }
    public ApplicationUser? HandledBy { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "Pick-up Date")]
    public DateTime StartDate { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "Return Date")]
    public DateTime EndDate { get; set; }

    [StringLength(200)]
    [Display(Name = "Pick-up Location")]
    public string? PickupLocation { get; set; }

    [StringLength(200)]
    [Display(Name = "Return Location")]
    public string? ReturnLocation { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Invoice? Invoice { get; set; }

    public int TotalDays => Math.Max(1, (EndDate - StartDate).Days);
}

public enum PaymentStatus { Unpaid, Paid, Overdue, Refunded }

public class Invoice
{
    public int Id { get; set; }

    [Required]
    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Base Amount")]
    public decimal BaseAmount { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Tax Amount")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Total Amount")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Discount")]
    public decimal Discount { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    public string? StripePaymentIntentId { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public ICollection<AdditionalCharge> AdditionalCharges { get; set; } = new List<AdditionalCharge>();

    public decimal TaxRate => 0.13m;  // 13% — change per jurisdiction

    public void Recalculate()
    {
        var addons = AdditionalCharges.Sum(c => c.Amount);
        BaseAmount  = (Reservation?.Vehicle?.DailyRate ?? 0) * (Reservation?.TotalDays ?? 1);
        TaxAmount   = (BaseAmount + addons - Discount) * TaxRate;
        TotalAmount = BaseAmount + addons - Discount + TaxAmount;
    }
}

public class AdditionalCharge
{
    public int Id { get; set; }

    [Required]
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    [Required, StringLength(120)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }
}
