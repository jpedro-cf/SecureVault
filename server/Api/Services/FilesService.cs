using EncryptionApp.Api.Dtos.Files;
using EncryptionApp.Api.Entities;
using EncryptionApp.Api.Factory;
using EncryptionApp.Api.Global;
using EncryptionApp.Api.Global.Errors;
using EncryptionApp.Api.Infra.Storage;
using EncryptionApp.Config;
using Microsoft.EntityFrameworkCore;
using EncryptionApp.Api.Global.Helpers;
using Microsoft.IdentityModel.Tokens;

namespace EncryptionApp.Api.Services;

public class FilesService(
    AppDbContext ctx, 
    AmazonS3 amazonS3,
    ResponseFactory responseFactory)
{
    public async Task<Result<FileResponse>> GetFile(Guid fileId, Guid? userId, GetFileRequest data)
    {
        if (!string.IsNullOrEmpty(data.ShareId))
        {
            // This could be optimized by performing background tasks, caching or a pre-computed table
            var sharedFile = await ctx.Files
                .FromSqlInterpolated($@"
                    WITH RECURSIVE RecursiveFolders AS (
                        SELECT f.""Id"" FROM ""Folders"" f
                        JOIN ""SharedLinks"" s ON s.""ItemId"" = f.""Id"" 
                        WHERE s.""Id"" = {Guid.Parse(data.ShareId!)} 
                            AND f.""Status"" = {nameof(FolderStatus.Active)}

                        UNION ALL

                        SELECT f.""Id"" FROM ""Folders"" f
                        JOIN RecursiveFolders rf ON f.""ParentFolderId"" = rf.""Id""
                        WHERE f.""Status"" = {nameof(FolderStatus.Active)}
                    )

                    SELECT f.* FROM ""Files"" f
                    JOIN RecursiveFolders rf ON f.""ParentFolderId"" = rf.""Id""
                    WHERE f.""Id"" = {fileId} AND f.""Status"" = {nameof(FileStatus.Completed)}
                    
                    UNION 
                    
                    SELECT f.* FROM ""Files"" f
                    JOIN ""SharedLinks"" s ON s.""ItemId"" = f.""Id""
                    WHERE f.""Id"" = {fileId} AND f.""Status"" = {nameof(FileStatus.Completed)}
                ")
                .FirstOrDefaultAsync();
            
            if (sharedFile == null)
            {
                return Result<FileResponse>.Failure(
                    new ForbiddenError("You're not allowed to view this file."));
            }

            return Result<FileResponse>.Success(
                await responseFactory.CreateFileResponse(sharedFile,false));
        }
        
        if (userId == null)
        {
            return Result<FileResponse>.Failure(
                new ForbiddenError("You're not allowed to view this file"));
        }
        
        var file = await ctx.Files.FirstOrDefaultAsync(f => 
            f.Id == fileId && f.Status == FileStatus.Completed && f.OwnerId == userId);
        if (file == null)
        {
            return Result<FileResponse>.Failure(new NotFoundError("File not found."));
        }

        return Result<FileResponse>.Success(
            await responseFactory.CreateFileResponse(file,true));
    }
    
    public async Task<Result<bool>> DeleteFile(Guid userId, Guid fileId)
    {
        await using var transaction = await ctx.Database.BeginTransactionAsync();
        try
        {
            var file = await ctx.Files.FirstOrDefaultAsync(f => 
                f.Id == fileId && f.OwnerId == userId && f.Status == FileStatus.Completed);

            if (file == null)
            {
                return Result<bool>.Failure(
                    new NotFoundError("File not found or upload was not completed."));
            }

            var contentType = file.ContentType.ToContentTypeEnum();
            var storageUsage = await ctx.StorageUsage.FirstAsync(s => 
                s.UserId == userId && s.ContentType == contentType);

            storageUsage.TotalSize -= file.Size;
            
            ctx.Files.Remove(file);
            await ctx.SaveChangesAsync();

            await amazonS3.DeleteObject(file.StorageKey);
            
            await transaction.CommitAsync();
            return Result<bool>.Success(true);
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            return Result<bool>.Failure(
                new InternalServerError("An error occured while deleting the file."));
        }
    }
}