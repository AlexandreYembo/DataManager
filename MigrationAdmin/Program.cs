using Connectors.Azure.CosmosDb.Repository;
using Connectors.Azure.TableStorage.Repository;
using Connectors.Redis;
using Migration.Core;
using Migration.EventHandlers;
using Migration.EventHandlers.Publishers;
using Migration.EventHandlers.Subscribers;
using Migration.Models;
using Migration.Models.Profile;
using Migration.Services;
using Migration.Services.Operations;
using Migration.Services.Operations.OperationsByType;
using Migration.Services.Subscribers;
using MigrationAdmin.Extensions;
using MigrationAdmin.Validations;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddConfiguration();

#region Register pub/sub
builder.Services.AddTransient(typeof(IPublisher<,>), typeof(Publisher<,>));

builder.Services.AddScoped<LogPublisher>();
builder.Services.AddScoped<LogDetailsPublisher>();
builder.Services.AddScoped<GetLogPublisher>();
builder.Services.AddScoped<ActionsPublisher>();
builder.Services.AddScoped<JobsPublisher>();
builder.Services.AddScoped<ValidationMessagePublisher>();

builder.Services.AddScoped<LogSubscriber>();
builder.Services.AddScoped<MigrationLogPersistSubscriber>();

#endregion

#region Operations

builder.Services.AddTransient<UpdateTargetData>();
builder.Services.AddTransient<DeleteTargetData>();
builder.Services.AddTransient<CacheTargetData>();
builder.Services.AddTransient<ReportTargetData>();
builder.Services.AddTransient<InsertTargetData>();

builder.Services.AddTransient<UpdateSourceData>();
builder.Services.AddTransient<DeleteSourceData>();
builder.Services.AddTransient<CacheSourceData>();
builder.Services.AddTransient<ReportSourceData>();

builder.Services.AddTransient<Func<ProfileConfiguration, IOperation>>(serviceProvider => profileConfiguration =>
{
    switch (profileConfiguration.DataQueryMappingType)
    {
        case DataQueryMappingType.SameCollection:
            return ResolveDependencySameCollectionMapping(serviceProvider, profileConfiguration.OperationType);
        case DataQueryMappingType.SourceToTarget:
            return ResolveDependencyTargetCollectionMapping(serviceProvider, profileConfiguration.OperationType);
        default:
            throw new ArgumentException(string.Empty, "Operation not supported");
    }
});

IOperation ResolveDependencyTargetCollectionMapping(IServiceProvider serviceProvider, OperationType operationType)
{
    switch (operationType)
    {
        case OperationType.Update:
            return serviceProvider.GetService<UpdateTargetData>();
        case OperationType.Delete:
            return serviceProvider.GetService<DeleteTargetData>();
        case OperationType.CacheData:
            return serviceProvider.GetService<CacheTargetData>();
        case OperationType.Report:
            return serviceProvider.GetService<ReportTargetData>();
        case OperationType.Import:
            return serviceProvider.GetService<InsertTargetData>();
        default:
            throw new ArgumentException(string.Empty, "Operation not supported");
    }
}

IOperation ResolveDependencySameCollectionMapping(IServiceProvider serviceProvider, OperationType operationType)
{
    switch (operationType)
    {
        case OperationType.Update:
            return serviceProvider.GetService<UpdateSourceData>();
        case OperationType.Delete:
            return serviceProvider.GetService<DeleteSourceData>();
        case OperationType.CacheData:
            return serviceProvider.GetService<CacheSourceData>();
        case OperationType.Report:
            return serviceProvider.GetService<ReportSourceData>();
        case OperationType.Import:
        default:
        throw new ArgumentException(string.Empty, "Operation not supported");
    }
}

#endregion

#region register Services
builder.Services.AddScoped<IQueryService, ProfileDataPreviewService>();
builder.Services.AddScoped<IMigrationService, MigrationService>();
builder.Services.AddScoped<IRevertMigrationService, RevertMigrationService>();
builder.Services.AddScoped<IJobService, JobService>();
#endregion

#region validation
builder.Services.AddScoped<ProfileValidation>();
#endregion


//Add repositories
builder.Services.RegisterRedis();

builder.Services.AddTransient<Func<DataSettings, IGenericRepository>>(_ => settings =>
     settings?.ConnectionType switch
{
    //TODO: also check for settings.IsCacheConnection and change the ICacheRepository to use IGenericRepository
    Migration.Models.ConnectionType.CosmosDb => new CosmosDbGenericRepository(settings),
    //ConnectionType.File => new FileRepository(settings),
    Migration.Models.ConnectionType.TableStorage => new WindowsAzureGenericRepository(settings),
    _ => throw new ArgumentException(string.Empty, "Invalid Db Type")
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