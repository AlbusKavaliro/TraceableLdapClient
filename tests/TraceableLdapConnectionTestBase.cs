using System.DirectoryServices.Protocols;
using System.Net;
using Xunit;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace TraceableLdapClient.Tests;

public abstract class TraceableLdapConnectionTestBase : IAsyncLifetime
{
    private const int LdapPort = 3890;
    private const string JwtSecret = "REPLACE_WITH_RANDOM";
    private const string KeySeed = "REPLACE_WITH_RANDOM";
    protected const string LdapBaseDn = "dc=example,dc=com";
    protected const string LdapUserPass = "adminPas$word";
    protected const string AdminUser = "cn=admin,ou=people,dc=example,dc=com";
    private readonly IContainer _ldapContainer;
    private string _ldapHost = "localhost";
    private int _ldapPort;

    protected TraceableLdapConnectionTestBase()
    {
        _ldapContainer = new ContainerBuilder()
            .WithImage("docker.io/lldap/lldap:stable")
            .WithPortBinding(LdapPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(LdapPort))
            .WithEnvironment("LLDAP_JWT_SECRET", JwtSecret)
            .WithEnvironment("LLDAP_KEY_SEED", KeySeed)
            .WithEnvironment("LLDAP_LDAP_BASE_DN", LdapBaseDn)
            .WithEnvironment("LLDAP_LDAP_USER_PASS", LdapUserPass)
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _ldapContainer.StartAsync().ConfigureAwait(true);
        _ldapPort = _ldapContainer.GetMappedPublicPort(LdapPort);
        _ldapHost = _ldapContainer.Hostname;
    }

    public async ValueTask DisposeAsync()
    {
        await _ldapContainer.DisposeAsync().ConfigureAwait(true);
    }

    protected TraceableLdapConnection CreateConnection()
    {
        var identifier = new LdapDirectoryIdentifier(_ldapHost, _ldapPort);
        var credential = new NetworkCredential(AdminUser, LdapUserPass);
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
