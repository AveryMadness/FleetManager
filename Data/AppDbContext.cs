using FleetManager.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Vehicle>          Vehicles          { get; set; }
    public DbSet<VehicleCategory>  VehicleCategories { get; set; }
    public DbSet<Customer>         Customers         { get; set; }
    public DbSet<Reservation>      Reservations      { get; set; }
    public DbSet<Invoice>          Invoices          { get; set; }
    public DbSet<AdditionalCharge> AdditionalCharges { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Invoice ↔ Reservation — 1-to-1
        builder.Entity<Invoice>()
            .HasOne(i => i.Reservation)
            .WithOne(r => r.Invoice)
            .HasForeignKey<Invoice>(i => i.ReservationId);

        // Reservation → HandledBy (nullable FK to ApplicationUser)
        builder.Entity<Reservation>()
            .HasOne(r => r.HandledBy)
            .WithMany(u => u.ManagedReservations)
            .HasForeignKey(r => r.HandledById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
