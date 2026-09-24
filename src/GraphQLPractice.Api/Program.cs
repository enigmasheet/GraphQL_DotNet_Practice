using GraphQLPractice.Api.Configuration;
using GraphQLPractice.Api.Data;
using GraphQLPractice.Api.Modules;
using HotChocolate.Data;
using HotChocolate.Subscriptions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Appsettings are surfaced as strongly-typed options, validated at startup rather
// than read as raw IConfiguration values at the point of use.
builder
    .Services.AddOptions<DatabaseSettings>()
    .BindConfiguration(DatabaseSettings.SectionName)
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Postgres),
        "Connection string 'ConnectionStrings:Postgres' is not configured. See appsettings.Development.json."
    )
    .ValidateOnStart();

builder
    .Services.AddOptions<CorsSettings>()
    .BindConfiguration(CorsSettings.SectionName)
    .Validate(
        settings => settings.AllowedOrigins.Length > 0,
        "Configuration 'Cors:AllowedOrigins' must contain at least one origin."
    )
    .ValidateOnStart();

builder.Services.AddDbContextFactory<AppDbContext>(
    (serviceProvider, options) =>
    {
        var database = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
        options.UseNpgsql(database.Postgres, npgsql => npgsql.EnableRetryOnFailure());
    }
);

// Source-generated from [assembly: DataLoaderModule("DataLoaders")].
builder.Services.AddDataLoaders();

// Each module contributes its own services.
foreach (var module in ModuleRegistry.Modules)
{
    module.Register(builder.Services);
}

// The "client" CORS policy is built from CorsSettings through the options pattern,
// so the origin list has a single source of truth (appsettings: Cors:AllowedOrigins).
builder.Services.AddCors();
builder
    .Services.AddOptions<CorsOptions>()
    .Configure<IOptions<CorsSettings>>(
        (cors, settings) =>
            cors.AddPolicy(
                "client",
                policy =>
                    policy
                        .WithOrigins(settings.Value.AllowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
            )
    );

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
