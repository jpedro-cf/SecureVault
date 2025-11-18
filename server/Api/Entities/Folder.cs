using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EncryptionApp.Api.Entities;

public class Folder : BaseEncryptedEntity
{
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FolderStatus Status { get; set; } = FolderStatus.Active;
    
    public Guid? ParentFolderId { get; set; }
    public virtual ICollection<Folder> Folders { get; set; } = new List<Folder>();

    public virtual ICollection<File> Files { get; set; } = new List<File>(); 
    
    [Required]
    public Guid OwnerId { get; set; }
    public virtual User Owner { get; set; }

    public static Folder CreateRoot(string name, Guid ownerId, string encryptedKey, string keyEncryptedByRoot)
    {
        return new Folder
        {
            Name = name,
            OwnerId = ownerId,
            EncryptedKey = encryptedKey,
            KeyEncryptedByRoot = keyEncryptedByRoot
        };
    }

    public static Folder CreateSubFolder(
        string name, 
        Guid ownerId,
        Folder parent,
        string encryptedKey, 
        string keyEncryptedByRoot)
    {
        return new Folder
        {
            Name = name,
            OwnerId = ownerId,
            ParentFolderId = parent.Id,
            EncryptedKey = encryptedKey,
            KeyEncryptedByRoot = keyEncryptedByRoot
        };
    }
}

public enum FolderStatus
{
    Deleted,
    Active
}