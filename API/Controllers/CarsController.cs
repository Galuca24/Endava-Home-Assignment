using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api")]
public class CarsController(CarService service) : ControllerBase
{
    private readonly CarService _service = service;

    [HttpGet("cars")]
    public async Task<ActionResult<List<CarDto>>> GetCars()
        => Ok(await _service.ListCarsAsync());

    [HttpGet("cars/{carId:long}/insurance-valid")]
    public async Task<ActionResult<InsuranceValidityResponse>> IsInsuranceValid(long carId, [FromQuery] string date)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
        {
            return BadRequest("Invalid date format. Use YYYY-MM-DD.");
        }

        var result = await _service.CheckInsuranceValidityAsync(carId, parsedDate);

        if (!result.CarFound)
        {
            return NotFound($"Car with ID {carId} not found.");
        }

        var response = new InsuranceValidityResponse(carId, parsedDate.ToString("yyyy-MM-dd"), result.IsValid);
        return Ok(response);
    }

    [HttpPost("cars/{carId:long}/claims")]
    public async Task<ActionResult<ClaimDto>> RegisterClaim(long carId, [FromBody] CreateClaimDto claimDto)
    {
        var createdClaim = await _service.RegisterClaimAsync(carId, claimDto);
        if (createdClaim is null)
        {
            return NotFound($"Car with ID {carId} not found.");
        }
        return CreatedAtAction(nameof(RegisterClaim), new { carId = createdClaim.CarId, claimId = createdClaim.Id }, createdClaim);
    }

    [HttpGet("cars/{carId:long}/history")]
    public async Task<ActionResult<List<HistoryItemDto>>> GetCarHistory(long carId)
    {
        var history = await _service.GetCarHistoryAsync(carId);
        if (history is null)
        {
            return NotFound($"Car with ID {carId} not found.");
        }
        return Ok(history);
    }

    [HttpPost("cars")]
    public async Task<ActionResult<CarDto>> CreateCar([FromBody] CreateCarDto carDto)
    {
        var result = await _service.CreateCarAsync(carDto);

        if (result.VinConflict)
        {
            return Conflict($"A car with VIN '{carDto.Vin}' already exists.");
        }

        if (result.OwnerNotFound)
        {
            return BadRequest($"Owner with ID {carDto.OwnerId} not found.");
        }

        var createdCar = result.CreatedCar;
        return CreatedAtAction(nameof(GetCars), new { id = createdCar.Id }, createdCar);
    }


    [HttpPost("cars/{carId:long}/policies")]
    public async Task<ActionResult<InsurancePolicyDto>> CreatePolicy(long carId, [FromBody] CreatePolicyDto policyDto)
    {
        if (policyDto.StartDate >= policyDto.EndDate)
        {
            return BadRequest("Policy StartDate must be before EndDate.");
        }

        var result = await _service.CreatePolicyAsync(carId, policyDto);

        if (result.CarNotFound)
        {
            return NotFound($"Car with ID {carId} not found.");
        }

        if (result.OverlapConflict)
        {
            return Conflict("The provided policy dates overlap with an existing policy for this car.");
        }

        var createdPolicy = result.CreatedPolicy;
        var actionName = nameof(GetCars); 
        return CreatedAtAction(actionName, new { id = createdPolicy.Id }, createdPolicy);
    }
}
