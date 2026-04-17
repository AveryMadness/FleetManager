using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FleetManager.Models;

public class VehicleCategory
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal BaseDailyRate { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}

public enum VehicleStatus { Available, Rented, Maintenance, Retired }

public class Vehicle
{
    public int Id { get; set; }

    [Required]
    public int CategoryId { get; set; }
    public VehicleCategory? Category { get; set; }

    [Required, StringLength(60)]
    public string Make { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string Model { get; set; } = string.Empty;

    [Range(1990, 2030)]
    public int Year { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "License Plate")]
    public string LicensePlate { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Daily Rate ($)")]
    public decimal DailyRate { get; set; }

    public VehicleStatus Status { get; set; } = VehicleStatus.Available;

    public int Mileage { get; set; }

    [Display(Name = "Image URL")]
    public string? ImageUrl { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
