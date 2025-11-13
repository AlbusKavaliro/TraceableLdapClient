using System.DirectoryServices.Protocols;
using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using TUnit.Core.Interfaces;

namespace AlbusKavaliro.TraceableLdapClient.Tests;

public class LdapContainer : IAsyncInitializer, IAsyncDisposable
{
    private const int LdapPort = 3890;
    private const string JwtSecret = "REPLACE_WITH_RANDOM";
    private const string KeySeed = "REPLACE_WITH_RANDOM";
    private readonly IContainer _ldapContainer;
    private string _ldapHost = "localhost";
    private int _ldapPort;

    public string BaseDn { get; } = "dc=example,dc=com";
    public string UserPass { get; } = "adminPas$word";
    public string AdminUser { get; } = "cn=admin,ou=people,dc=example,dc=com";

    public LdapContainer()
    {
        _ldapContainer = new ContainerBuilder()
            .WithImage("docker.io/lldap/lldap:stable")
            .WithPortBinding(LdapPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(LdapPort))
            .WithEnvironment("LLDAP_JWT_SECRET", JwtSecret)
            .WithEnvironment("LLDAP_KEY_SEED", KeySeed)
            .WithEnvironment("LLDAP_LDAP_BASE_DN", BaseDn)
            .WithEnvironment("LLDAP_LDAP_USER_PASS", UserPass)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _ldapContainer.StartAsync().ConfigureAwait(true);
        _ldapPort = _ldapContainer.GetMappedPublicPort(LdapPort);
        _ldapHost = _ldapContainer.Hostname;
    }

    public async ValueTask DisposeAsync()
    {
        await _ldapContainer.DisposeAsync().ConfigureAwait(true);
        GC.SuppressFinalize(this);
    }

    public TraceableLdapConnection CreateConnection()
    {
        var identifier = new LdapDirectoryIdentifier(_ldapHost, _ldapPort);
        var credential = new NetworkCredential(AdminUser, UserPass);
        return new TraceableLdapConnection(identifier, credential, AuthType.Basic)
        {
            AutoBind = true,
            Timeout = TimeSpan.FromSeconds(30),
            SessionOptions =
            {
                ReferralChasing = ReferralChasingOptions.All,
                ProtocolVersion = 3
            }
        };
    }
}
