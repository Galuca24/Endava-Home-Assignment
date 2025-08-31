namespace CarInsurance.Api.Dtos;

public record CarDto(long Id, string Vin, string? Make, string? Model, int Year, long OwnerId, string OwnerName, string? OwnerEmail);
public record CreateCarDto(string Vin, string Make, string Model, int YearOfManufacture, long OwnerId);

public record CreatePolicyDto(string Provider, DateOnly StartDate, DateOnly EndDate);
public record InsurancePolicyDto(long Id, long CarId, string Provider, DateOnly StartDate, DateOnly EndDate);
public record InsuranceValidityResponse(long CarId, string Date, bool Valid);


public record CreateClaimDto(DateOnly ClaimDate, string Description, decimal Amount);
public record ClaimDto(long Id, long CarId, DateOnly ClaimDate, string Description, decimal Amount);
public record HistoryItemDto(DateOnly Date, string EventType, string Description);
