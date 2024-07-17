global using System.Net;
global using System.Text;
global using Newtonsoft.Json;
global using Microsoft.Azure.Functions.Worker;
global using Microsoft.Azure.Functions.Worker.Http;
global using Microsoft.Extensions.Logging;
global using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
global using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RuneFunctions;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureOpenApi()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddSingleton<IInteractionFunction, InteractionFunction>();
    })
    .Build();

host.Run();

