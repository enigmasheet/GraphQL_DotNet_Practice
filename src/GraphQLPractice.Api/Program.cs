using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Modules;
using HotChocolate.Data;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "Connection string 'Postgres' is not configured. See appsettings.Development.json."
    );

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
);

// Source-generated from [assembly: DataLoaderModule("DataLoaders")].
builder.Services.AddDataLoaders();

// Each module contributes its own services.
foreach (var module in ModuleRegistry.Modules)
{
    module.Register(builder.Services);
}

builder.Services.AddCors(options =>
{
    var origins =
        builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:5200"];

    options.AddPolicy(
        "client",
        policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()
    );
});

builder
    .AddGraphQL()
    .AddTypes() // Source-generated from [assembly: Module("Types")].
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .AddPagingArguments()
    .AddGlobalObjectIdentification(options => options.MaxAllowedNodeBatchSize = 50)
    .AddInMemorySubscriptions()
    .AddMutationConventions(applyToAllMutations: true)
    .RegisterDbContextFactory<AppDbContext>()
    .ModifyPagingOptions(options => options.RequirePagingBoundaries = true)
    .ModifyCostOptions(options =>
    {
        // Hot Chocolate prices a variable-bound filter/sort input at its worst case, so
        // ordinary client operations (e.g. GetPosts passing `where`/`order` as variables)
        // are estimated far above their real cost. Keep the analyzer's protection but
        // give real operations headroom; measure with the `GraphQL-Cost: report` header.
        options.MaxFieldCost = 10_000;
        options.MaxTypeCost = 10_000;
    })
    .ModifyRequestOptions(options =>
        options.IncludeExceptionDetails = builder.Environment.IsDevelopment()
    );

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();

    await db.Database.MigrateAsync();
    await SeedData.SeedAsync(db);
}

app.UseCors("client");
app.UseWebSockets();

// GraphQL endpoint + the Nitro IDE when opened in a browser.
app.MapGraphQL().WithOptions(options => options.Tool.Title = "GraphQL Practice API");

// Dedicated IDE URL (does not shadow the endpoint above).
app.MapNitroApp("/graphql/ui").WithOptions(options => options.Title = "GraphQL Practice API");

// Download the schema SDL: http://localhost:5100/graphql/schema
app.MapGraphQLSchema("/graphql/schema");

app.RunWithGraphQLCommands(args);
