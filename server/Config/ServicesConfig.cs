using System.Security.Cryptography;
using EncryptionApp.Api.Factory;
using EncryptionApp.Api.Infra.Security;
using EncryptionApp.Api.Infra.Storage;
using EncryptionApp.Api.Services;
using EncryptionApp.Api.Workers;

namespace EncryptionApp.Config;

public static class ServicesConfig
{
    public static void AddServicesConfig(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<AmazonS3>();
        builder.Services.AddSingleton<ResponseFactory>();
        
        builder.Services.AddSingleton<BackgroundTaskQueue>();
        builder.Services.AddHostedService<DeletionBackgroundTask>();
        builder.Services.AddHostedService<CleanupBackgroundTask>();
        
        builder.Services.AddTransient<StorageUsageService>();
        builder.Services.AddTransient<FilesService>();
        builder.Services.AddTransient<UploadsService>();
        builder.Services.AddTransient<UsersService>();
        builder.Services.AddTransient<AuthService>();
        builder.Services.AddTransient<FoldersService>();
        builder.Services.AddTransient<ShareService>();
        builder.Services.AddTransient<ItemsService>();

        builder.Services.AddSingleton<JwtTokenHandler>();
    }
}