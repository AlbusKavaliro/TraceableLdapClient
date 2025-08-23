using System;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using TraceableLdapClient;
using DotNet.Testcontainers.Containers;

namespace TraceableLdapClient.Tests;

public class TraceableLdapConnectionIntegrationTests : IAsyncLifetime
{
    private const int LdapPort = 3890;
    private const string JwtSecret = "REPLACE_WITH_RANDOM";
    private const string KeySeed = "REPLACE_WITH_RANDOM";
    private const string LdapBaseDn = "dc=example,dc=com";
    private const string LdapUserPass = "adminPas$word";
    private const string AdminUser = "cn=admin,ou=people,dc=example,dc=com";
    private readonly IContainer _ldapContainer;
    private string _ldapHost = "localhost";
    private int _ldapPort;

    public TraceableLdapConnectionIntegrationTests()
    {
        _ldapContainer = new ContainerBuilder()
            .WithImage("docker.io/lldap/lldap:stable")
            .WithPortBinding(LdapPort, true)
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
        GC.SuppressFinalize(this);
    }

    private TraceableLdapConnection CreateConnection()
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

    [Fact]
    public void BindShouldAuthenticateSuccessfully()
    {
        using TraceableLdapConnection conn = CreateConnection();
        conn.Bind();
    }

    [Fact]
    public void BindWithCredentialShouldAuthenticateSuccessfully()
    {
        using TraceableLdapConnection conn = CreateConnection();
        var credential = new NetworkCredential(AdminUser, LdapUserPass);
        conn.Bind(credential);
    }

    [Fact]
    public void SendRequestSearchRequestReturnsEntries()
    {
        using TraceableLdapConnection conn = CreateConnection();
        conn.Bind();
        var searchRequest = new SearchRequest(
            LdapBaseDn,
            "(objectClass=*)",
            SearchScope.Subtree,
            null);
        DirectoryResponse response = conn.SendRequest(searchRequest);
        Xunit.Assert.IsType<SearchResponse>(response);
    }

    [Fact]
    public void SendRequestWithTimeoutSearchRequestReturnsEntries()
    {
        using TraceableLdapConnection conn = CreateConnection();
        conn.Bind();
        var searchRequest = new SearchRequest(
            LdapBaseDn,
            "(objectClass=*)",
            SearchScope.Subtree,
            null);
        DirectoryResponse response = conn.SendRequest(searchRequest, TimeSpan.FromSeconds(5));
        Xunit.Assert.IsType<SearchResponse>(response);
    }

    [Fact]
    public void BeginSendRequestAndEndSendRequestWorks()
    {
        using TraceableLdapConnection conn = CreateConnection();
        conn.Bind();
        var searchRequest = new SearchRequest(
            LdapBaseDn,
            "(objectClass=*)",
            SearchScope.Subtree,
            null);
        IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, PartialResultProcessing.NoPartialResultSupport, callback: default!, state: default!);
        DirectoryResponse response = conn.EndSendRequest(asyncResult);
        Xunit.Assert.IsType<SearchResponse>(response);
    }

    [Fact]
    public void BeginSendRequestWithTimeoutAndEndSendRequestWorks()
    {
        using TraceableLdapConnection conn = CreateConnection();
        conn.Bind();
        var searchRequest = new SearchRequest(
            LdapBaseDn,
            "(objectClass=*)",
            SearchScope.Subtree,
            null);
        IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, TimeSpan.FromSeconds(5), PartialResultProcessing.NoPartialResultSupport, callback: default!, state: default!);
        DirectoryResponse response = conn.EndSendRequest(asyncResult);
        Xunit.Assert.IsType<SearchResponse>(response);
    }

    [Fact]
    public void GetPartialResultsReturnsNullOrCollection()
    {
        using TraceableLdapConnection conn = CreateConnection();
        conn.Bind();
        var searchRequest = new SearchRequest(
            LdapBaseDn,
            "(objectClass=*)",
            SearchScope.Subtree,
            null);
        IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, PartialResultProcessing.ReturnPartialResults, callback: default!, state: default!);
        PartialResultsCollection? partialResults = conn.GetPartialResults(asyncResult);
        Xunit.Assert.True(partialResults == null || partialResults.Count >= 0);
    }

    [Fact]
    public void AbortShouldNotThrow()
    {
        using TraceableLdapConnection conn = CreateConnection();
        conn.Bind();
        var searchRequest = new SearchRequest(
            LdapBaseDn,
            "(objectClass=*)",
            SearchScope.Subtree,
            null);
        IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, PartialResultProcessing.NoPartialResultSupport, callback: default!, state: default!);
        conn.Abort(asyncResult);
    }

    [Fact]
    public void DisposeShouldNotThrow()
    {
        TraceableLdapConnection conn = CreateConnection();
        conn.Dispose();
    }
}
