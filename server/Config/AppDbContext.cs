using EncryptionApp.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using File = EncryptionApp.Api.Entities.File;

namespace EncryptionApp.Config;

public class AppDbContext(DbContextOptions options) : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Folder> Folders { get; set; }
    public DbSet<File> Files { get; set; }
    public DbSet<StorageUsage> StorageUsage { get; set; }
    public DbSet<SharedLink> SharedLinks { get; set; }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        builder.Entity<Folder>()
            .HasOne(f => f.Owner)
            .WithMany(u => u.Folders)
            .HasForeignKey(f => f.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<File>()
            .HasOne(f => f.Owner)
            .WithMany(u => u.Files)
            .HasForeignKey(f => f.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Folder>()
            .HasOne<Folder>()
            .WithMany(f => f.Folders)
            .HasForeignKey(f => f.ParentFolderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<File>()
            .HasOne(f => f.ParentFolder)
            .WithMany(f => f.Files)
            .HasForeignKey(f => f.ParentFolderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Folder>()
            .Property(f => f.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Entity<File>()
            .Property(f => f.Name)
            .HasMaxLength(256)
            .IsRequired();
        
        builder.Entity<File>()
            .Property(f => f.Status)
            .HasConversion<string>();
        
        builder.Entity<Folder>()
            .Property(f => f.Status)
            .HasConversion<string>();

        builder.Entity<StorageUsage>()
            .HasOne(s => s.User)
            .WithMany(u => u.StorageUsages)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<StorageUsage>()
            .Property(s => s.ContentType)
            .HasConversion<string>();
        
        builder.Entity<StorageUsage>()
            .HasIndex(s => new { s.ContentType, s.UserId })
            .IsUnique();
        
        builder.Entity<SharedLink>()
            .HasOne(s => s.Owner)
            .WithMany(u => u.SharedLinks)
            .HasForeignKey(s => s.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<SharedLink>()
            .Property(s => s.ItemType)
            .HasConversion<string>();
    }
}