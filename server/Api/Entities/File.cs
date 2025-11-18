using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EncryptionApp.Api.Entities;

public class File : BaseEncryptedEntity
{
    [Required]
    [MaxLength(256)]
    public string StorageKey { get; set; }
    
    [Required]
    public string UploadId { get; set; }

    [Required] 
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FileStatus Status { get; set; } = FileStatus.Pending;
    
    [Required]
    public string ContentType { get; set; }
    
    [Required]
    public long Size { get; set; }
    
    public Guid? ParentFolderId { get; set; }
    public virtual Folder? ParentFolder { get; set; }
    
    public Guid? OwnerId { get; set; }
    public virtual User? Owner { get; set; }
}

public enum FileStatus
{
    Deleted,
    Pending,
    Completed
}