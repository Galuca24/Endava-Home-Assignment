using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Services;

public class CarService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public async Task<(CarDto? CreatedCar, bool VinConflict, bool OwnerNotFound)> CreateCarAsync(CreateCarDto carDto)
    {
        var ownerExists = await _db.Owners.AnyAsync(o => o.Id == carDto.OwnerId);
        if (!ownerExists)
        {
            return (null, false, true); 
        }

        var vinExists = await _db.Cars.AnyAsync(c => c.Vin.ToUpper() == carDto.Vin.ToUpper());
        if (vinExists)
        {
            return (null, true, false); 
        }

        var newCar = new Car
        {
            Vin = carDto.Vin,
            Make = carDto.Make,
            Model = carDto.Model,
            YearOfManufacture = carDto.YearOfManufacture,
            OwnerId = carDto.OwnerId
        };

        _db.Cars.Add(newCar);
        await _db.SaveChangesAsync();
        await _db.Entry(newCar).Reference(c => c.Owner).LoadAsync();

        var carResultDto = new CarDto(newCar.Id, newCar.Vin, newCar.Make, newCar.Model, newCar.YearOfManufacture,
            newCar.OwnerId, newCar.Owner.Name, newCar.Owner.Email);

        return (carResultDto, false, false); 
    }

    public async Task<List<CarDto>> ListCarsAsync()
    {
        return await _db.Cars.Include(c => c.Owner)
            .Select(c => new CarDto(c.Id, c.Vin, c.Make, c.Model, c.YearOfManufacture,
                                    c.OwnerId, c.Owner.Name, c.Owner.Email))
            .ToListAsync();
    }

    public async Task<(InsurancePolicyDto? CreatedPolicy, bool OverlapConflict, bool CarNotFound)> CreatePolicyAsync(long carId, CreatePolicyDto policyDto)
    {
        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists)
        {
            return (null, false, true); 
        }

        var hasOverlappingPolicy = await _db.Policies
            .AnyAsync(p => p.CarId == carId &&
                           policyDto.StartDate <= p.EndDate && 
                           policyDto.EndDate >= p.StartDate);  

        if (hasOverlappingPolicy)
        {
            return (null, true, false); 
        }

        var newPolicy = new InsurancePolicy
        {
            CarId = carId,
            Provider = policyDto.Provider,
            StartDate = policyDto.StartDate,
            EndDate = policyDto.EndDate
        };

        _db.Policies.Add(newPolicy);
        await _db.SaveChangesAsync();

        var policyResultDto = new InsurancePolicyDto(newPolicy.Id, newPolicy.CarId, newPolicy.Provider,
            newPolicy.StartDate, newPolicy.EndDate);

        return (policyResultDto, false, false); 
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

    public async Task<(bool CarFound, bool IsValid)> CheckInsuranceValidityAsync(long carId, DateOnly date)
    {
        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists)
        {
            return (CarFound: false, IsValid: false);
        }

        var isValid = await _db.Policies.AnyAsync(p =>
            p.CarId == carId &&
            p.StartDate <= date &&
            p.EndDate >= date
        );

        return (CarFound: true, IsValid: isValid);
    }


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
