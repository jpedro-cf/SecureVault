using EncryptionApp.Api.Entities;
using EncryptionApp.Api.Infra.Storage;
using EncryptionApp.Config;
using Microsoft.EntityFrameworkCore;
using File = EncryptionApp.Api.Entities.File;

namespace EncryptionApp.Api.Workers;

public class DeletionBackgroundTask(
    BackgroundTaskQueue queue, 
    IServiceScopeFactory serviceScopeFactory,
    ILogger<DeletionBackgroundTask> logger,
    AmazonS3 amazonS3)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var task = await queue.Dequeue(stoppingToken);
            using (var scope = serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var handlers = new Dictionary<BackgroundTaskType, Func<AppDbContext, Guid, Task>>
                {
                    {BackgroundTaskType.Folder, HandleFolder},
                    {BackgroundTaskType.User, HandleUser},
                    {BackgroundTaskType.File, HandleFile},
                };

                await handlers[task.Type](dbContext, task.Id);
            }
        }
    }

    private async Task HandleUser(AppDbContext ctx, Guid userId)
    {
        await using var transaction = await ctx.Database.BeginTransactionAsync();
        try
        {
            logger.LogInformation($"Starting user '{userId}' deletion at {DateTime.UtcNow}");
            
            // not using cascade because the Amazon S3 deletion can fail.
            // file will be deleted on HandleFile cleanup task
            await ctx.Files
                .Where(f => f.OwnerId == userId)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(f => f.Status, f => FileStatus.Deleted));
            
            // cascade folders, shared links, storage usage, etc...
            await ctx.Users
                .Where(u => u.Id == userId)
                .ExecuteDeleteAsync();
            
            await ctx.SaveChangesAsync();
            await transaction.CommitAsync();
            
            logger.LogInformation($"Finished user '{userId}' deletion at {DateTime.UtcNow}");
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            logger.LogError($"User deletion failed for user '{userId}' at {DateTime.UtcNow}");
        }
    }
    
    private async Task HandleFolder(AppDbContext ctx, Guid folderId)
    {
        await using var transaction = await ctx.Database.BeginTransactionAsync();
        try
        {
            logger.LogInformation($"Starting deletion of folder '{folderId}' at {DateTime.UtcNow}");
            
            var folderIds = await ctx.Folders
                .FromSqlInterpolated($@"
                    WITH RECURSIVE RecursiveFolders AS (
                        SELECT ""Id"" FROM ""Folders"" WHERE ""Id"" = {folderId}
                        UNION ALL
                        SELECT f.""Id"" FROM ""Folders"" f
                        INNER JOIN RecursiveFolders rf ON f.""ParentFolderId"" = rf.""Id""
                    )
                    SELECT * FROM RecursiveFolders
                ")
                .Select(f => f.Id)
                .ToListAsync();
            
            // not using cascade because the Amazon S3 deletion can fail.
            // file will be deleted on HandleFile cleanup task
            await ctx.Files
                .Where(f => f.ParentFolderId.HasValue && folderIds.Contains(f.ParentFolderId.Value))
                .ExecuteUpdateAsync<File>(u =>
                    u.SetProperty(f => f.Status, FileStatus.Deleted));

            await ctx.Folders
                .Where(f => f.Id == folderId)
                .ExecuteDeleteAsync();
            
            await ctx.SaveChangesAsync();
            await transaction.CommitAsync();
            
            logger.LogInformation($"Deletion of folder '{folderId}' completed at {DateTime.UtcNow}");
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            logger.LogError($"Failed deletion of folder '{folderId}' at {DateTime.UtcNow}");
        }
    }
    
    private async Task HandleFile(AppDbContext ctx, Guid fileId)
    {
        await using var transaction = await ctx.Database.BeginTransactionAsync();
        try
        {
            logger.LogInformation($"Starting deletion of file '{fileId}' at {DateTime.UtcNow}");
            var file = await ctx.Files.FirstOrDefaultAsync(f => f.Id == fileId);
            if (file == null)
            {
                logger.LogInformation($"File '{fileId}' not found. Finishing task...");
                return;
            }
            
            ctx.Files.Remove(file);
            await ctx.SaveChangesAsync();

            if (file.Status == FileStatus.Pending)
            {
                await amazonS3.AbortMultiPartUpload(file.StorageKey, file.UploadId);
            }
            else
            {
                await amazonS3.DeleteObject(file.StorageKey);
            }
            
            await transaction.CommitAsync();
            
            logger.LogInformation($"Deletion of file '{fileId}' completed at {DateTime.UtcNow}");
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            logger.LogError($"Failed deletion of file '{fileId}' at {DateTime.UtcNow}");
        }
    }
}