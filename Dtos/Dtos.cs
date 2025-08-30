namespace CarInsurance.Api.Dtos;

public record CarDto(long Id, string Vin, string? Make, string? Model, int Year, long OwnerId, string OwnerName, string? OwnerEmail);
public record InsuranceValidityResponse(long CarId, string Date, bool Valid);

// DTO for creating a new claim
public record CreateClaimDto(DateOnly ClaimDate, string Description, decimal Amount);


// DTO for returning claim details
public record ClaimDto(long Id, long CarId, DateOnly ClaimDate, string Description, decimal Amount);

// DTO for returning owner details
public record HistoryItemDto(DateOnly Date, string EventType, string Description);
