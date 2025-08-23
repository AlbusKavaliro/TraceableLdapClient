using System.DirectoryServices.Protocols;
using System.Net;
using TraceableLdapClient;
using WorkerService;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddScoped<ILdapConnection>(provider =>
{
    return new TraceableLdapConnection(new LdapDirectoryIdentifier("localhost", 3890),
         new NetworkCredential("cn=admin,ou=people,dc=example,dc=com", "adminPas$word"), AuthType.Basic)
    {
        AutoBind = true,
        Timeout = TimeSpan.FromSeconds(30),
        SessionOptions =
        {
            ReferralChasing = ReferralChasingOptions.All,
            ProtocolVersion = 3
        }
    };
});
builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
