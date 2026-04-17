using System.ComponentModel.DataAnnotations;

namespace FleetManager.Models.ViewModels;

// auth
public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    [Required, StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string Role { get; set; } = "Staff";
}

// dashboard
public class DashboardViewModel
{
    public int TotalVehicles       { get; set; }
    public int AvailableVehicles   { get; set; }
    public int ActiveReservations  { get; set; }
    public decimal MonthlyRevenue  { get; set; }
    public decimal OutstandingDebt { get; set; }

    public List<Reservation> RecentReservations { get; set; } = new();
    public List<DailyStatPoint> WeeklyStats     { get; set; } = new();
}

public class DailyStatPoint
{
    public string Day   { get; set; } = string.Empty;
    public int    Count { get; set; }
}

// billing
public class BillingViewModel
{
    public decimal OutstandingTotal { get; set; }
    public decimal CollectedMonth   { get; set; }
    public decimal AverageInvoice   { get; set; }
    public List<Invoice> Invoices   { get; set; } = new();
}

// reports
public class ReportFilterViewModel
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate   { get; set; }
    public string    ReportType { get; set; } = "ReservationSummary";
}

public class ReportResultViewModel
{
    public ReportFilterViewModel Filter  { get; set; } = new();
    public List<ReportRow>       Rows    { get; set; } = new();
    public decimal               Total   { get; set; }
}

public class ReportRow
{
    public string Label  { get; set; } = string.Empty;
    public int    Count  { get; set; }
    public decimal Amount { get; set; }
}
