using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Connectors.Redis
{
    public static class DependencyInjection
    {
        public static IServiceCollection RegisterRedis(this IServiceCollection service)
        {
            service.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var serviceConfig = sp.GetRequiredService<IConfiguration>() as IConfigurationRoot;

                var connectionString = serviceConfig.GetConnectionString("Redis");

                return ConnectionMultiplexer.Connect(connectionString);
            });

            service.AddSingleton<IDatabase>(sp => sp.GetService<IConnectionMultiplexer>().GetDatabase());

            service.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            service.AddScoped<ICacheRepository, CacheRepository>();

            return service;
        }
    }
}
