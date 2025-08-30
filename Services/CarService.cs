using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Services;

public class CarService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public async Task<List<CarDto>> ListCarsAsync()
    {
        return await _db.Cars.Include(c => c.Owner)
            .Select(c => new CarDto(c.Id, c.Vin, c.Make, c.Model, c.YearOfManufacture,
                                    c.OwnerId, c.Owner.Name, c.Owner.Email))
            .ToListAsync();
    }

    public async Task<bool> IsInsuranceValidAsync(long carId, DateOnly date)
    {
        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists) throw new KeyNotFoundException($"Car {carId} not found");

        return await _db.Policies.AnyAsync(p =>
            p.CarId == carId &&
            p.StartDate <= date &&
            p.EndDate >= date
        );
    }

    //To register a claim
    public async Task<ClaimDto?> RegisterClaimAsync(long carId, CreateClaimDto createClaimDto)
    {
        var car = await _db.Cars.FindAsync(carId);
        if (car is null)
        {
            return null; 
        }

        var newClaim = new Claim
        {
            CarId = carId,
            ClaimDate = createClaimDto.ClaimDate,
            Description = createClaimDto.Description,
            Amount = createClaimDto.Amount
        };

        _db.Claims.Add(newClaim);
        await _db.SaveChangesAsync();

        return new ClaimDto(newClaim.Id, newClaim.CarId, newClaim.ClaimDate, newClaim.Description, newClaim.Amount);
    }

    // Metod? pentru a ob?ine istoricul unei ma?ini
    public async Task<List<HistoryItemDto>?> GetCarHistoryAsync(long carId)
    {
        var car = await _db.Cars
            .Include(c => c.Policies)
            .Include(c => c.Claims)
            .FirstOrDefaultAsync(c => c.Id == carId);

        if (car is null)
        {
            return null; 
        }

        var history = new List<HistoryItemDto>();

        foreach (var policy in car.Policies)
        {
            history.Add(new HistoryItemDto(policy.StartDate, "Policy Start", $"Provider: {policy.Provider}, Valid until: {policy.EndDate:yyyy-MM-dd}"));
            history.Add(new HistoryItemDto(policy.EndDate, "Policy End", $"Provider: {policy.Provider}"));
        }

        foreach (var claim in car.Claims)
        {
            history.Add(new HistoryItemDto(claim.ClaimDate, "Claim", $"Description: {claim.Description}, Amount: {claim.Amount:C}"));
        }

        return history.OrderBy(item => item.Date).ToList();
    }


}
