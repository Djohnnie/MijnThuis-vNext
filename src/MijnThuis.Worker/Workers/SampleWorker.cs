namespace MijnThuis.Worker.Workers;

public class SampleWorker : BackgroundService
{
    private readonly ILogger<SampleWorker> _logger;

    public SampleWorker(ILogger<SampleWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("SampleWorker running at: {time}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
