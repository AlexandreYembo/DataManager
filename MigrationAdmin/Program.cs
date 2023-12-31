using Blazored.LocalStorage;
using Migration.Infrastructure.CosmosDb;
using Migration.Infrastructure.Redis;
using Migration.Repository;
using Migration.Services;
using Migration.Services.Publishers;
using Migration.Services.Subscribers;
using MigrationAdmin.Extensions;
using MigrationAdmin.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddConfiguration();

builder.Services.AddTransient(typeof(IStorage<>), typeof(LocalStorage<>));

#region Register pub/sub
builder.Services.AddTransient(typeof(IPublisher<,>), typeof(Publisher<,>));

builder.Services.AddScoped<LogPublisher>();
builder.Services.AddScoped<LogDetailsPublisher>();

builder.Services.AddScoped<LogResultSubscriber>();

#endregion

#region register Services
builder.Services.AddScoped<IUpdateRecordsInBatchService, UpdateRecordsInBatchService>();
builder.Services.AddScoped<IQueryService, MutipleQueriesService>();
builder.Services.AddScoped<IMigrationService, MigrationService>();
#endregion


//Add repositories
builder.Services.AddBlazoredLocalStorage();

builder.Services.RegisterRedis();

builder.Services.AddTransient<Func<DataSettings, IGenericRepository>>(_ => settings =>
     settings?.ConnectionType switch
{
    ConnectionType.CosmosDb => new CosmosDbGenericRepository(settings),
    ConnectionType.File => new FileRepository(settings),
    //   DbType.TableStorage => new CosmosDbGenericRepository(settings), TODO: To add the repository for Table Storage
    _ => throw new ArgumentException(string.Empty, "Invalid Db Type")
});

builder.Services.AddTransient<Func<DBSettings, IGenericRepository>>(_ => settings =>
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
        //   DbType.TableStorage => new CosmosDbGenericRepository(settings), TODO: To add the repository for Table Storage
        _ => throw new ArgumentException(string.Empty, "Invalid Db Type")
    };
});


builder.Services.AddTransient<Func<DataSettings, ITestConnection>>(_ => settings => 
    settings?.ConnectionType switch
    {
        ConnectionType.CosmosDb => new CosmosDbConnection(settings),
        //DataType.TableStorage => new TableStorageConnection(settings), // TODO: Add
        //DataType.Api => new ApiClient(settings),// TODO: Add
        _ => throw new ArgumentException(string.Empty, "Invalid Data Type")
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();