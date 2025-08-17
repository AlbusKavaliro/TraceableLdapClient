using Projects;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ContainerResource> ldap = builder.AddContainer(nameof(ldap), "docker.io/osixia/openldap")
    .WithEnvironment("LDAP_ORGANISATION", "Example Inc.")
    .WithEnvironment("LDAP_DOMAIN", "example.com")
    .WithEnvironment("LDAP_ADMIN_PASSWORD", "admin")
    .WithEnvironment("LDAP_CONFIG_PASSWORD", "config")
    .WithEnvironment("LDAP_TLS", "false");

builder.AddProject<WorkerService>("worker")
    .WaitFor(ldap);

await builder.Build().RunAsync().ConfigureAwait(false);
