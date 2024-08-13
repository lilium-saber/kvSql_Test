using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Projects;
using Aspire;

var builder = DistributedApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();
