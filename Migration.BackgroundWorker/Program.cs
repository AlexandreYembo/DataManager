using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Migration.BackgroundWorker;
using Migration.Infrastructure.AzureTableStorage;
using Migration.Infrastructure.CosmosDb;
using Migration.Infrastructure.Redis;
using Migration.Repository;
using Migration.Repository.Publishers;
using Migration.Repository.Subscribers;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();

        #region Register pub/sub
        services.AddTransient(typeof(IPublisher<,>), typeof(Publisher<,>));

        services.AddScoped<LogPublisher>();
        services.AddScoped<LogDetailsPublisher>();

        services.AddScoped<LogResultSubscriber>();
        #endregion

        //Add repositories
        services.RegisterRedis();

        services.AddTransient<Func<DataSettings, IGenericRepository>>(_ => settings =>
            settings?.ConnectionType switch
            {
                ConnectionType.CosmosDb => new CosmosDbGenericRepository(settings),
                ConnectionType.File => new FileRepository(settings),
                ConnectionType.TableStorage => new TableStorageGenericRepository(settings),
                _ => throw new ArgumentException(string.Empty, "Invalid Db Type")
            });

        services.AddTransient<Func<DBSettings, IGenericRepository>>(_ => settings =>
        {
            var dataSettings = new DataSettings() // TODO:move to the Data Settings
            {
                Parameters = new List<CustomAttributes>()
                {
                    new() { Key = "Endpoint", Value = settings.Endpoint },
                    new() { Key = "AuthKey", Value = settings.AuthKey },
                    new() { Key = "Database", Value = settings.Database }
                },
                Name = settings.Name
            };

            return settings?.DbType switch
            {
                DbType.Cosmos => new CosmosDbGenericRepository(dataSettings),
                DbType.TableStorage => new TableStorageGenericRepository(dataSettings),
                _ => throw new ArgumentException(string.Empty, "Invalid Db Type")
            };
        });


        services.AddTransient<Func<DataSettings, ITestConnection>>(_ => settings =>
            settings?.ConnectionType switch
            {
                ConnectionType.CosmosDb => new CosmosDbConnection(settings),
                ConnectionType.TableStorage => new TableStorageConnection(settings),
                //DataType.Api => new ApiClient(settings),// TODO: Add
                _ => throw new ArgumentException(string.Empty, "Invalid Data Type")
            });

    })
    .Build();

await host.RunAsync();