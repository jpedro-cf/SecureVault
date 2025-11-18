using EncryptionApp.Api.Entities;
using EncryptionApp.Config;
using Microsoft.EntityFrameworkCore;

namespace EncryptionApp.Api.Workers;

public class CleanupBackgroundTask(
    ILogger<CleanupBackgroundTask> logger,
    IServiceScopeFactory serviceScopeFactory,
    BackgroundTaskQueue taskQueue) : BackgroundService
{
    private readonly TimeSpan _period = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_period);

        await DoWork(stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DoWork(stoppingToken);
        }
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        using (var scope = serviceScopeFactory.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            logger.LogInformation($"Executing periodic background task at {DateTime.UtcNow}");

            var tasks = new List<BackgroundTask>();
            
            var files = await ctx.Files
                .Where(f =>
                    (f.Status == FileStatus.Pending && DateTime.UtcNow.AddHours(-3) >= f.CreatedAt)
                    || f.Status == FileStatus.Deleted)
                .OrderBy(x => x.CreatedAt)
                .Take(200)
                .Select(f => new BackgroundTask(f.Id, BackgroundTaskType.File))
                .ToListAsync(stoppingToken);

            var folders = await ctx.Folders
                .Where(f => f.Status == FolderStatus.Deleted)
                .OrderBy(x => x.CreatedAt)
                .Take(200)
                .Select(f => new BackgroundTask(f.Id, BackgroundTaskType.Folder))
                .ToListAsync(stoppingToken);

            var users = await ctx.Users
                .Where(u => u.EmailConfirmed == false)
                .OrderBy(x => x.Id)
                .Take(200)
                .Select(f => new BackgroundTask(f.Id, BackgroundTaskType.User))
                .ToListAsync(stoppingToken);
            
            tasks.AddRange(files);
            tasks.AddRange(folders);
            tasks.AddRange(users);
                
            foreach (var task in tasks)
            {
                taskQueue.Enqueue(task);
            }
        }
    }
}