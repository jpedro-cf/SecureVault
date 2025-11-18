using System.ComponentModel.DataAnnotations;
using EncryptionApp.Api.Dtos;
using EncryptionApp.Api.Global;

namespace EncryptionApp.Api.Entities;

public class BaseEncryptedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required] 
    public string Name { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // [ iv | ciphertext ]
    public string EncryptedKey { get; set; }
    
    // [ iv | ciphertext ]
    [Required]
    public string KeyEncryptedByRoot { get; set; }
}