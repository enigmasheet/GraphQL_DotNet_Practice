using GraphQLPractice.Client;
using GraphQLPractice.Client.Configuration;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Options;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// appsettings.json is surfaced as a strongly-typed option, validated at startup
// rather than read as a raw IConfiguration value.
builder
    .Services.AddOptions<GraphqlSettings>()
    .BindConfiguration(GraphqlSettings.SectionName)
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Url),
        "Configuration 'Graphql:Url' is not configured. See wwwroot/appsettings.json."
    )
    .ValidateOnStart();

builder
    .Services.AddBlogClient()
    .ConfigureHttpClient(
        (serviceProvider, client) =>
            client.BaseAddress = new Uri(
                serviceProvider.GetRequiredService<IOptions<GraphqlSettings>>().Value.Url
            )
    )
    .ConfigureWebSocketClient(
        (serviceProvider, client) =>
            client.Uri = new Uri(
                serviceProvider.GetRequiredService<IOptions<GraphqlSettings>>().Value.WebSocketUrl
            )
    );

await builder.Build().RunAsync();
