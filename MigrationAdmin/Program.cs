using Connectors.Azure.CosmosDb.Repository;
using Connectors.Azure.TableStorage.Repository;
using Migration.Core;
using Migration.EventHandlers;
using Migration.EventHandlers.Publishers;
using Migration.EventHandlers.Subscribers;
using Migration.Infrastructure.Redis;
using Migration.Models;
using Migration.Services;
using Migration.Services.Subscribers;
using MigrationAdmin.Extensions;
using MigrationAdmin.Validations;

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

builder.Services.AddScoped<LogSubscriber>();
builder.Services.AddScoped<MigrationLogPersistSubscriber>();

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
    ConnectionType.CosmosDb => new CosmosDbGenericRepository(settings),
    //ConnectionType.File => new FileRepository(settings),
    ConnectionType.TableStorage => new WindowsAzureGenericRepository(settings),
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