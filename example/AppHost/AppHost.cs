using Projects;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<WorkerService>("worker");

await builder.Build().RunAsync().ConfigureAwait(false);
