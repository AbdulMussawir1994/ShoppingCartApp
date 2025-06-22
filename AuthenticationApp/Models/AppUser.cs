using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthenticationApp.Models;

public class AppUser
{
    [BsonId] // Primary key
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    public string UserName { get; set; }
    public string Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("CNIC")]
    public string CNIC { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public long? UpdatedBy { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedDate { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastLoginDate { get; set; }

    public bool IsActive { get; set; } = true;

    public List<string> Roles { get; set; } = new();
}
