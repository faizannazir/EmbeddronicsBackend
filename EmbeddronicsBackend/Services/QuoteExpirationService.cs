using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EmbeddronicsBackend.Services
{
    /// <summary>
    /// Background service that periodically checks for and updates expired quotes
    /// </summary>
    public class QuoteExpirationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<QuoteExpirationService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check every hour

        public QuoteExpirationService(
            IServiceProvider serviceProvider,
            ILogger<QuoteExpirationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Quote Expiration Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredQuotes();
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing expired quotes");
                    // Continue running even if there's an error
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait 5 minutes before retrying
                }
            }

            _logger.LogInformation("Quote Expiration Service stopped");
        }

        private async Task ProcessExpiredQuotes()
        {
            using var scope = _serviceProvider.CreateScope();
            var quoteService = scope.ServiceProvider.GetRequiredService<IQuoteService>();

            try
            {
                var updatedCount = await quoteService.UpdateExpiredQuotesAsync();
                
                if (updatedCount > 0)
                {
                    _logger.LogInformation("Updated {Count} expired quotes", updatedCount);
                }
                else
                {
                    _logger.LogDebug("No expired quotes found to update");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update expired quotes");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Quote Expiration Service is stopping");
            await base.StopAsync(stoppingToken);
        }
    }
}