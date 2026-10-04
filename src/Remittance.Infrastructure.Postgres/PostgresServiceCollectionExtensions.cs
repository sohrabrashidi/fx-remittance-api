using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Remittance.Core.Abstractions;

namespace Remittance.Infrastructure.Postgres;

public static class PostgresServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresStores(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IRateStore, PostgresRateStore>();
        services.AddSingleton<IQuoteStore, PostgresQuoteStore>();
        services.AddSingleton<ITransferStore, PostgresTransferStore>();
        services.AddSingleton<IIdempotencyStore, PostgresIdempotencyStore>();
        return services;
    }
}
