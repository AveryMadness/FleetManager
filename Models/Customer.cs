using System.ComponentModel.DataAnnotations;

namespace FleetManager.Models;

public enum CustomerStatus { Active, Inactive, Blacklisted }

public class Customer
{
    public int Id { get; set; }

    [Required, StringLength(60)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(60)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Phone]
    public string? Phone { get; set; }

    [Required, StringLength(30)]
    [Display(Name = "Driver's License #")]
    public string LicenseNumber { get; set; } = string.Empty;

    public string? Address { get; set; }

    [Display(Name = "Date of Birth")]
    public DateTime? DateOfBirth { get; set; }

    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public string FullName => $"{FirstName} {LastName}";
}
