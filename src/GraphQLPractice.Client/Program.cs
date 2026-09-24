using GraphQLPractice.Client;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var graphqlUrl = builder.Configuration["GraphqlUrl"] ?? "http://localhost:5100/graphql";
var graphqlWsUrl = graphqlUrl
    .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
    .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase);

builder
    .Services.AddBlogClient()
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(graphqlUrl))
    .ConfigureWebSocketClient(client => client.Uri = new Uri(graphqlWsUrl));

await builder.Build().RunAsync();
