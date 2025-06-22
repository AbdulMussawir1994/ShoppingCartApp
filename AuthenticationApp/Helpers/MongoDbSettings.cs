namespace AuthenticationApp.Helpers;

public interface IMongoDbSettings
{
    string ConnectionString { get; set; }
    string DatabaseName { get; set; }
}

public class MongoDbSettings : IMongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}
