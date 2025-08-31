using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Models;
using CarInsurance.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CarInsuranceTest
{
    public class CarServiceTests
    {
   
        private AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var dbContext = new AppDbContext(options);
            return dbContext;
        }


        [Fact]
        public async Task CheckInsuranceValidityAsync_WhenCarNotFound_ShouldReturnCarFoundFalse()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var service = new CarService(dbContext);
            var nonExistentCarId = 999L;
            var testDate = new DateOnly(2025, 1, 1);

            // Act
            var result = await service.CheckInsuranceValidityAsync(nonExistentCarId, testDate);

            // Assert
            Assert.False(result.CarFound);
        }

        [Fact]
        public async Task CheckInsuranceValidityAsync_WhenDateIsOutsidePolicyRange_ShouldReturnIsValidFalse()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var car = new Car { Id = 1, Vin = "TESTVIN" };
            var policy = new InsurancePolicy { CarId = 1, StartDate = new DateOnly(2025, 1, 1), EndDate = new DateOnly(2025, 1, 31) };
            await dbContext.Cars.AddAsync(car);
            await dbContext.Policies.AddAsync(policy);
            await dbContext.SaveChangesAsync();

            var service = new CarService(dbContext);
            var testDate = new DateOnly(2025, 2, 1); 

            // Act
            var result = await service.CheckInsuranceValidityAsync(1, testDate);

            // Assert
            Assert.True(result.CarFound);
            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task CheckInsuranceValidityAsync_WhenDateIsOnPolicyEndDate_ShouldReturnIsValidTrue()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var car = new Car { Id = 1, Vin = "TESTVIN" };
            var policy = new InsurancePolicy { CarId = 1, StartDate = new DateOnly(2025, 1, 1), EndDate = new DateOnly(2025, 1, 31) };
            await dbContext.Cars.AddAsync(car);
            await dbContext.Policies.AddAsync(policy);
            await dbContext.SaveChangesAsync();

            var service = new CarService(dbContext);
            var testDate = new DateOnly(2025, 1, 31); 

            // Act
            var result = await service.CheckInsuranceValidityAsync(1, testDate);

            // Assert
            Assert.True(result.CarFound);
            Assert.True(result.IsValid);
        }



        [Fact]
        public async Task RegisterClaimAsync_WhenCarExists_ShouldCreateAndReturnClaim()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var car = new Car { Id = 1, Vin = "TESTVIN" };
            await dbContext.Cars.AddAsync(car);
            await dbContext.SaveChangesAsync();

            var service = new CarService(dbContext);
            var createClaimDto = new CreateClaimDto(new DateOnly(2025, 5, 10), "Minor accident", 1500.50m);

            // Act
            var result = await service.RegisterClaimAsync(1, createClaimDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.CarId);
            Assert.Equal("Minor accident", result.Description);
            Assert.Equal(1500.50m, result.Amount);

            var claimInDb = await dbContext.Claims.FirstOrDefaultAsync();
            Assert.NotNull(claimInDb);
            Assert.Equal("Minor accident", claimInDb.Description);
        }

        [Fact]
        public async Task RegisterClaimAsync_WhenCarDoesNotExist_ShouldReturnNull()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var service = new CarService(dbContext);
            var nonExistentCarId = 999L;
            var createClaimDto = new CreateClaimDto(new DateOnly(2025, 5, 10), "Accident", 1000m);

            // Act
            var result = await service.RegisterClaimAsync(nonExistentCarId, createClaimDto);

            // Assert
            Assert.Null(result);
        }



        [Fact]
        public async Task GetCarHistoryAsync_WhenCarNotFound_ShouldReturnNull()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var service = new CarService(dbContext);
            var nonExistentCarId = 999L;

            // Act
            var result = await service.GetCarHistoryAsync(nonExistentCarId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetCarHistoryAsync_WhenCarHasNoHistory_ShouldReturnEmptyList()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var car = new Car { Id = 1, Vin = "TESTVIN_NO_HISTORY" };
            await dbContext.Cars.AddAsync(car);
            await dbContext.SaveChangesAsync();
            var service = new CarService(dbContext);

            // Act
            var result = await service.GetCarHistoryAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetCarHistoryAsync_WhenCarHasHistory_ShouldReturnSortedItems()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var car = new Car { Id = 1, Vin = "TESTVIN_HISTORY", Owner = new Owner { Name = "Test Owner" } };

            var policy = new InsurancePolicy { CarId = 1, Provider = "TestProvider", StartDate = new DateOnly(2025, 1, 15), EndDate = new DateOnly(2025, 12, 31) };
            var claim = new Claim { CarId = 1, Description = "Damage test", Amount = 500, ClaimDate = new DateOnly(2025, 6, 1) };

            await dbContext.Cars.AddAsync(car);
            await dbContext.Policies.AddAsync(policy);
            await dbContext.Claims.AddAsync(claim);
            await dbContext.SaveChangesAsync();

            var service = new CarService(dbContext);

            // Act
            var result = await service.GetCarHistoryAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count); 

            Assert.Equal("Policy Start", result[0].EventType);
            Assert.Equal(new DateOnly(2025, 1, 15), result[0].Date);

            Assert.Equal("Claim", result[1].EventType);
            Assert.Equal(new DateOnly(2025, 6, 1), result[1].Date);

            Assert.Equal("Policy End", result[2].EventType);
            Assert.Equal(new DateOnly(2025, 12, 31), result[2].Date);
        }


        [Fact]
        public async Task CreateCarAsync_WhenVinIsUniqueAndOwnerExists_ShouldCreateCar()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var owner = new Owner { Id = 1, Name = "Test Owner" };
            await dbContext.Owners.AddAsync(owner);
            await dbContext.SaveChangesAsync();

            var service = new CarService(dbContext);
            var carDto = new CreateCarDto("VIN123", "Dacia", "Logan", 2023, 1);

            // Act
            var result = await service.CreateCarAsync(carDto);

            // Assert
            Assert.NotNull(result.CreatedCar);
            Assert.False(result.VinConflict);
            Assert.False(result.OwnerNotFound);
            Assert.Equal("VIN123", result.CreatedCar.Vin);

            Assert.Equal(1, await dbContext.Cars.CountAsync());
        }

        [Fact]
        public async Task CreateCarAsync_WhenVinAlreadyExists_ShouldReturnVinConflict()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var owner = new Owner { Id = 1, Name = "Test Owner" };
            var existingCar = new Car { Id = 1, Vin = "VIN123", OwnerId = 1 };
            await dbContext.Owners.AddAsync(owner);
            await dbContext.Cars.AddAsync(existingCar);
            await dbContext.SaveChangesAsync();

            var service = new CarService(dbContext);
            var carDto = new CreateCarDto("VIN123", "VW", "Golf", 2024, 1);

            // Act
            var result = await service.CreateCarAsync(carDto);

            // Assert
            Assert.Null(result.CreatedCar);
            Assert.True(result.VinConflict);
            Assert.False(result.OwnerNotFound);

            Assert.Equal(1, await dbContext.Cars.CountAsync());
        }


        [Fact]
        public async Task CreatePolicyAsync_WhenCarExistsAndNoOverlap_ShouldCreatePolicy()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var car = new Car { Id = 1, Vin = "TESTVIN" };
            await dbContext.Cars.AddAsync(car);
            await dbContext.SaveChangesAsync();

            var service = new CarService(dbContext);
            var policyDto = new CreatePolicyDto("Allianz", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));

            // Act
            var result = await service.CreatePolicyAsync(1, policyDto);

            // Assert
            Assert.NotNull(result.CreatedPolicy);
            Assert.False(result.OverlapConflict);
            Assert.False(result.CarNotFound);
            Assert.Equal("Allianz", result.CreatedPolicy.Provider);

            Assert.Equal(1, await dbContext.Policies.CountAsync());
        }

        [Fact]
        public async Task CreatePolicyAsync_WhenPolicyOverlaps_ShouldReturnOverlapConflict()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var car = new Car { Id = 1, Vin = "TESTVIN" };
            var existingPolicy = new InsurancePolicy
            {
                CarId = 1,
                Provider = "Existing",
                StartDate = new DateOnly(2025, 3, 1),
                EndDate = new DateOnly(2025, 9, 30)
            };
            await dbContext.Cars.AddAsync(car);
            await dbContext.Policies.AddAsync(existingPolicy);
            await dbContext.SaveChangesAsync();

            var service = new CarService(dbContext);
            var overlappingPolicyDto = new CreatePolicyDto("Overlap", new DateOnly(2025, 9, 15), new DateOnly(2025, 10, 15));

            // Act
            var result = await service.CreatePolicyAsync(1, overlappingPolicyDto);

            // Assert
            Assert.Null(result.CreatedPolicy);
            Assert.True(result.OverlapConflict);
            Assert.False(result.CarNotFound);

            Assert.Equal(1, await dbContext.Policies.CountAsync());
        }
    }
}
