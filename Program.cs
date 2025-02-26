

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<WebOpenerService>();
var host = builder.Build();
await host.RunAsync();