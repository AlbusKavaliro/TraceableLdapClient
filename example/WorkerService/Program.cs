using System.DirectoryServices.Protocols;
using System.Net;
using TraceableLdapClient;
using WorkerService;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<ILdapConnection>(provider =>
{
    return new TraceableLdapConnection(new LdapDirectoryIdentifier("ldap", 389),
        new NetworkCredential("admin", "admin"), AuthType.Basic)
    {
        AutoBind = true,
        Timeout = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
