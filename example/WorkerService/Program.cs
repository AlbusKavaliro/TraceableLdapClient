using System.DirectoryServices.Protocols;
using System.Net;
using TraceableLdapClient;
using WorkerService;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<ILdapConnection>(provider =>
{
    return new TraceableLdapConnection(new LdapDirectoryIdentifier("localhost", 3890),
         new NetworkCredential("admin", "adminPas$word"), AuthType.Basic)
    {
        AutoBind = true,
        Timeout = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
