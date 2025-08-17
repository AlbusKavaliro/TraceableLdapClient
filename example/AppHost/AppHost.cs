using Projects;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ContainerResource> ldap = builder.AddContainer(nameof(ldap), "docker.io/lldap/lldap", "stable")
    .WithEndpoint(3890, 3890, "ldap", isProxied: false)
    .WithHttpEndpoint(17170, 17170, "frontend")
    .WithExternalHttpEndpoints()
    .WithEnvironment("LLDAP_JWT_SECRET", "REPLACE_WITH_RANDOM")
    .WithEnvironment("LLDAP_KEY_SEED", "REPLACE_WITH_RANDOM")
    .WithEnvironment("LLDAP_LDAP_BASE_DN", "dc=example,dc=com")
    .WithEnvironment("LLDAP_LDAP_USER_PASS", "adminPas$word");

builder.AddProject<WorkerService>("worker")
    .WaitFor(ldap);

await builder.Build().RunAsync().ConfigureAwait(false);
