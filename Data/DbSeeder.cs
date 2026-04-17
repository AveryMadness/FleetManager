using FleetManager.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db          = services.GetRequiredService<AppDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.EnsureCreatedAsync();
        
        foreach (var role in new[] { "Admin", "Staff" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        const string adminEmail = "admin@fleetmanager.com";
        const string adminPass  = "Admin@1234";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email    = adminEmail,
                FullName = "System Administrator",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, adminPass);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }
        
        if (!await db.VehicleCategories.AnyAsync())
        {
            db.VehicleCategories.AddRange(
                new VehicleCategory { Name = "Economy", BaseDailyRate = 45,  Description = "Compact & fuel-efficient" },
                new VehicleCategory { Name = "Sedan",   BaseDailyRate = 65,  Description = "Comfortable mid-size" },
                new VehicleCategory { Name = "SUV",     BaseDailyRate = 95,  Description = "Spacious sport utility" },
                new VehicleCategory { Name = "Luxury",  BaseDailyRate = 150, Description = "Premium vehicles" },
                new VehicleCategory { Name = "Van",     BaseDailyRate = 90,  Description = "Family & cargo vans" }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Vehicles.AnyAsync())
        {
            var sedan  = (await db.VehicleCategories.FirstAsync(c => c.Name == "Sedan")).Id;
            var suv    = (await db.VehicleCategories.FirstAsync(c => c.Name == "SUV")).Id;
            var luxury = (await db.VehicleCategories.FirstAsync(c => c.Name == "Luxury")).Id;
            var van    = (await db.VehicleCategories.FirstAsync(c => c.Name == "Van")).Id;

            db.Vehicles.AddRange(
                new Vehicle { CategoryId = sedan,  Make = "Toyota",   Model = "Camry",    Year = 2023, LicensePlate = "TK-204", DailyRate = 62,  Mileage = 8400  },
                new Vehicle { CategoryId = suv,    Make = "Ford",     Model = "Explorer", Year = 2022, LicensePlate = "FE-118", DailyRate = 104, Mileage = 21000 },
                new Vehicle { CategoryId = sedan,  Make = "Honda",    Model = "Civic",    Year = 2023, LicensePlate = "HC-307", DailyRate = 55,  Mileage = 5100  },
                new Vehicle { CategoryId = luxury, Make = "BMW",      Model = "3 Series", Year = 2024, LicensePlate = "BM-091", DailyRate = 140, Mileage = 3200  },
                new Vehicle { CategoryId = van,    Make = "Chrysler", Model = "Pacifica", Year = 2022, LicensePlate = "CP-055", DailyRate = 95,  Mileage = 18000 },
                new Vehicle { CategoryId = suv,    Make = "Kia",      Model = "Sportage", Year = 2023, LicensePlate = "KS-412", DailyRate = 88,  Mileage = 9600  }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Customers.AnyAsync())
        {
            db.Customers.AddRange(
                new Customer { FirstName="Alice",  LastName="Johnson", Email="alice@example.com",  Phone="555-101-2020", LicenseNumber="DL-448821" },
                new Customer { FirstName="Bob",    LastName="Martinez",Email="bob@example.com",    Phone="555-202-3030", LicenseNumber="DL-339902" },
                new Customer { FirstName="Clara",  LastName="Ngo",     Email="clara@example.com",  Phone="555-303-4040", LicenseNumber="DL-271134" },
                new Customer { FirstName="David",  LastName="Chen",    Email="david@example.com",  Phone="555-404-5050", LicenseNumber="DL-182209" }
            );
            await db.SaveChangesAsync();
        }
    }
}
