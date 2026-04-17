using Microsoft.AspNetCore.Identity;

namespace FleetManager.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Reservation> ManagedReservations { get; set; } = new List<Reservation>();
}
