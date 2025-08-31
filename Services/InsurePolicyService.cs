using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Services
{
    public class InsurancePolicyService(IServiceProvider serviceProvider, ILogger<InsurancePolicyService> logger) : IHostedService, IDisposable
    {
        private Timer? _timer;
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ILogger<InsurancePolicyService> _logger = logger;



        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Insurance Policy Background Service is starting.");
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            return Task.CompletedTask;
        }

        public async Task ProcessExpiredPoliciesAsync(AppDbContext dbContext)
        {
            var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

            var expiredPolicies = await dbContext.Policies
                .Where(p => p.EndDate == yesterday && !p.ExpirationNotified)
                .Include(p => p.Car)
                .ToListAsync();

            if (!expiredPolicies.Any())
            {
                _logger.LogInformation("No new policies expired.");
                return;
            }

            foreach (var policy in expiredPolicies)
            {
                _logger.LogWarning($"[EXPIRATION] Policy for car VIN '{policy.Car.Vin}' (Provider: {policy.Provider}) expired on {policy.EndDate:yyyy-MM-dd}.");
                policy.ExpirationNotified = true;
            }

            await dbContext.SaveChangesAsync();
            _logger.LogInformation($"Processed and marked {expiredPolicies.Count} expired policies as notified.");
        }

        private void DoWork(object? state)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            ProcessExpiredPoliciesAsync(dbContext).GetAwaiter().GetResult();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Insurance Policy Background Service is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
