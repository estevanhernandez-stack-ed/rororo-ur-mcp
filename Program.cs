using Labs626.UrMcp.Ipc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// RoRoRo Ur MCP — a stdio MCP server Claude launches; authorized as an installed, consented
// RoRoRo plugin. stdout carries the MCP protocol, so ALL logging goes to stderr — one stray
// Console.WriteLine is a corrupted protocol stream, which is why the threshold below is Trace
// (everything to stderr) rather than a filter.
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<IRoRoRoHost, RoRoRoHostClient>();
builder.Services.AddSingleton<IUrTaskBridge, UrTaskBridgeClient>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
