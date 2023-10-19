using Blazored.LocalStorage;
using Migration.Infrastructure.CosmosDb;
using Migration.Repository;
using Migration.Services;
using Migration.Services.Publishers;
using Migration.Services.Subscribers;
using MigrationAdmin.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddTransient(typeof(IStorage<>), typeof(LocalStorage<>));

#region Register pub/sub
builder.Services.AddTransient(typeof(IPublisher<,>), typeof(Publisher<,>));

builder.Services.AddScoped<LogPublisher>();
builder.Services.AddScoped<LogDetailsPublisher>();

builder.Services.AddScoped<LogResultSubscriber>();

#endregion

builder.Services.AddScoped<IUpdateRecordsInBatchService, UpdateRecordsInBatchService>();

//Add repositories
builder.Services.AddBlazoredLocalStorage();

builder.Services.AddTransient<Func<DBSettings, IGenericRepository>>(_ => settings =>
     settings?.DbType switch
{
    DbType.Cosmos => new CosmosDbGenericRepository(settings),
    //   DbType.TableStorage => new CosmosDbGenericRepository(settings), TODO: To add the repository for Table Storage
    _ => throw new ArgumentException(string.Empty, "Invalid Db Type")
});

builder.Services.AddTransient<Func<DBSettings, ITestConnection>>(_ => settings => 
    settings?.DbType switch
    {
        DbType.Cosmos => new CosmosDbConnection(settings),
        _=> throw new ArgumentException(string.Empty, "Invalid Db Type")
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