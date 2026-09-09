using Clinic.Application.Abstractions.Appointments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clinic.Infrastructure.Appointments;

public sealed partial class SuspendedAppointmentRevalidationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SuspendedAppointmentRevalidationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RevalidateAsync(stoppingToken);
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RevalidateAsync(stoppingToken);
        }
    }

    private async Task RevalidateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IAppointmentService service = scope.ServiceProvider
                .GetRequiredService<IAppointmentService>();
            await service.RevalidateSuspendedAsync(null, null, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogRevalidationFailure(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Suspended appointment revalidation failed.")]
    private static partial void LogRevalidationFailure(ILogger logger, Exception exception);
}
