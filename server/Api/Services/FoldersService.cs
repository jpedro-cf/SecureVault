using EncryptionApp.Api.Dtos.Folders;
using EncryptionApp.Api.Entities;
using EncryptionApp.Api.Global;
using EncryptionApp.Api.Global.Errors;
using EncryptionApp.Api.Global.Helpers;
using EncryptionApp.Api.Workers;
using EncryptionApp.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace EncryptionApp.Api.Services;

public class FoldersService(AppDbContext ctx, BackgroundTaskQueue backgroundTaskQueue)
{
    public async Task<Result<FolderResponse>> Create(Guid userId, CreateFolderRequest data)
    {
        if (data.ParentId is null)
        {
            var folder = Folder.CreateRoot(data.Name, userId, data.EncryptedKey, data.KeyEncryptedByRoot);

            ctx.Folders.Add(folder);
            await ctx.SaveChangesAsync();

            return Result<FolderResponse>.Success(FolderResponse.From(folder, true));
        }

        var parent = await ctx.Folders.FirstOrDefaultAsync(f => f.Id == data.ParentId);
        if (parent == null)
        {
            return Result<FolderResponse>.Failure(
                new NotFoundError("Parent folder not found."));
        }        

        var subFolder = Folder.CreateSubFolder(
            data.Name,
            userId,
            parent,
            data.EncryptedKey,
            data.KeyEncryptedByRoot);

        ctx.Folders.Add(subFolder);
        await ctx.SaveChangesAsync();

        return Result<FolderResponse>.Success(FolderResponse.From(subFolder, true));
    }

    public async Task<Result<FolderResponse>> GetFolder(Guid folderId, Guid? userId, GetFolderRequest data)
    {
        if (!string.IsNullOrEmpty(data.ShareId))
        {
            // This could be optimized by performing background tasks, caching or a pre-computed table
            var sharedFolder = await ctx.Folders
                .FromSqlInterpolated($@"
                    WITH RECURSIVE RecursiveFolders AS (
                        SELECT f.""Id"" FROM ""Folders"" f
                        JOIN ""SharedLinks"" s ON s.""ItemId"" = f.""Id"" 
                            AND s.""Id"" = {Guid.Parse(data.ShareId!)}
                        WHERE f.""Status"" = {nameof(FolderStatus.Active)}

                        UNION ALL

                        SELECT f.""Id"" FROM ""Folders"" f
                        JOIN RecursiveFolders rf ON f.""ParentFolderId"" = rf.""Id""
                        WHERE f.""Status"" = {nameof(FolderStatus.Active)}
                    )
                    SELECT f.* FROM ""Folders"" f
                    JOIN RecursiveFolders rf ON f.""Id"" = rf.""Id""
                    WHERE f.""Id"" = {folderId} AND f.""Status"" = {nameof(FolderStatus.Active)}
                ")
                .FirstOrDefaultAsync();
            
            if (sharedFolder == null)
            {
                return Result<FolderResponse>.Failure(
                    new ForbiddenError("You're not allowed to view this folder."));
            }
            
            return Result<FolderResponse>.Success(FolderResponse.From(sharedFolder, false));
        }
        
        if (userId == null)
        {
            return Result<FolderResponse>.Failure(
                new ForbiddenError("You're not allowed to view this folder"));
        }
        
        var folder = await ctx.Folders.FirstOrDefaultAsync(f => 
            f.Id == folderId && f.OwnerId == userId && f.Status == FolderStatus.Active);
        
        if (folder == null)
        {
            return Result<FolderResponse>.Failure(new NotFoundError("Folder not found."));
        }

        return Result<FolderResponse>.Success(FolderResponse.From(folder, true));
    }

    public async Task<Result<bool>> DeleteFolder(Guid userId, Guid folderId)
    {
        await using var transaction = await ctx.Database.BeginTransactionAsync();
        try
        {
            var folder = await ctx.Folders.FirstOrDefaultAsync(f => 
                f.Id == folderId && f.OwnerId == userId && f.Status == FolderStatus.Active);
            
            if (folder == null)
            {
                return Result<bool>.Failure(new NotFoundError("Folder not found."));
            }
            
            // We're updating storage usage here because our app still too small
            // It's a nice user experience to have updates in real time
            // If it grows too big, just let the background tasks handle it
            var subFiles = await ctx.Files
                .FromSqlInterpolated($@"
                    WITH RECURSIVE RecursiveFolders AS (
                        SELECT ""Id"" FROM ""Folders"" WHERE ""Id"" = {folderId}
                        UNION ALL
                        SELECT f.""Id"" FROM ""Folders"" f
                        INNER JOIN RecursiveFolders rf ON f.""ParentFolderId"" = rf.""Id""
                    )
                    SELECT f.* FROM ""Files"" f
                    JOIN RecursiveFolders rf ON f.""ParentFolderId"" = rf.""Id""
                ")
                .ToListAsync();

            var sizeGroupedByContentType = subFiles
                .GroupBy(f => f.ContentType.ToContentTypeEnum())
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(f => f.Size)
                );
            
            var storageUsage = await ctx.StorageUsage
                .Where(s => s.UserId == userId)
                .ToListAsync();
            
            // free space so user can upload more
            foreach (var usage in storageUsage)
            {
                if (sizeGroupedByContentType.TryGetValue(usage.ContentType, out var size))
                {
                    usage.TotalSize -= size;
                }
            }
    
            // mark for deletion (BATCH JOBS to delete all subfiles S3 objects later)
            folder.Status = FolderStatus.Deleted;
            await ctx.SaveChangesAsync();
            await transaction.CommitAsync();
            
            backgroundTaskQueue.Enqueue(new BackgroundTask(folder.Id, BackgroundTaskType.Folder));
            return Result<bool>.Success(true);
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            return Result<bool>.Failure(new InternalServerError("An error occured while deleting this folder."));
        }
    }
}