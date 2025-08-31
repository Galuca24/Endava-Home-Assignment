using CarInsurance.Api.Data;
using CarInsurance.Api.Models;
using CarInsurance.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarInsuranceTest
{
    public class InsurancePolicyServiceTests
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
        public async Task ProcessExpiredPolicies_ShouldOnlyUpdate_NotNotifiedAndExpiredYesterdayPolicies()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterday = today.AddDays(-1);

            var carForPolicyToExpire = new Car { Id = 1, Vin = "VIN_TO_PROCESS" };
            var carWithAlreadyNotifiedPolicy = new Car { Id = 2, Vin = "VIN_ALREADY_NOTIEFIED" };
            var carWithFuturePolicy = new Car { Id = 3, Vin = "VIN_FUTURE" };


            var policyToExpire = new InsurancePolicy { Id = 101, CarId = carForPolicyToExpire.Id, EndDate = yesterday, ExpirationNotified = false };
            var alreadyNotifiedPolicy = new InsurancePolicy { Id = 102, CarId = carWithAlreadyNotifiedPolicy.Id, EndDate = yesterday, ExpirationNotified = true };
            var futurePolicy = new InsurancePolicy { Id = 103, CarId = carWithFuturePolicy.Id, EndDate = today.AddDays(1), ExpirationNotified = false };

            await dbContext.Cars.AddRangeAsync(carForPolicyToExpire, carWithAlreadyNotifiedPolicy, carWithFuturePolicy);
            await dbContext.Policies.AddRangeAsync(policyToExpire, alreadyNotifiedPolicy, futurePolicy);
            await dbContext.SaveChangesAsync();

            var logger = NullLogger<InsurancePolicyService>.Instance;
            var service = new InsurancePolicyService(null!, logger);


            // Act
            await service.ProcessExpiredPoliciesAsync(dbContext);


            // Assert

            var processedPolicy = await dbContext.Policies.FindAsync(101L);
            Assert.NotNull(processedPolicy);
            Assert.True(processedPolicy.ExpirationNotified, "The policy that expired yesterday should be marked as notified.");

            var ignoredDuplicatePolicy = await dbContext.Policies.FindAsync(102L);
            Assert.NotNull(ignoredDuplicatePolicy);
            Assert.True(ignoredDuplicatePolicy.ExpirationNotified, "The already notified policy should remain notified.");

            var ignoredFuturePolicy = await dbContext.Policies.FindAsync(103L);
            Assert.NotNull(ignoredFuturePolicy);
            Assert.False(ignoredFuturePolicy.ExpirationNotified, "The future policy should not be marked as notified.");
        }
    }
}
