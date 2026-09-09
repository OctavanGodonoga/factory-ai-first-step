using MongoDB.Driver;

namespace TalentMap.Api.Extensions;

public static class MongoServiceCollectionExtensions
{
    public static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MongoDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing required configuration value 'ConnectionStrings:MongoDb'.");
        }

        var databaseName = configuration["MongoDbSettings:DatabaseName"];
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "Missing required configuration value 'MongoDbSettings:DatabaseName'.");
        }

        services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));

        return services;
    }
}
