using System.DirectoryServices.Protocols;
using System.Net;

namespace TraceableLdapClient.Tests;

[NotInParallel]
public class TraceableLdapConnectionIntegrationTests
{
    [ClassDataSource<LdapContainer>(Shared = SharedType.PerTestSession)]
    public required LdapContainer Ldap { get; init; }

    [Test]
    public async Task BindShouldAuthenticateSuccessfully()
    {
        using TraceableLdapConnection conn = Ldap.CreateConnection();
        conn.Bind();
    }

    [Test]
    public async Task BindWithCredentialShouldAuthenticateSuccessfully()
    {
        using TraceableLdapConnection conn = Ldap.CreateConnection();
        var credential = new NetworkCredential(Ldap.AdminUser, Ldap.UserPass);
        conn.Bind(credential);
    }

    [Test]
    public async Task SendRequestSearchRequestReturnsEntries()
    {
        using TraceableLdapConnection conn = Ldap.CreateConnection();
        conn.Bind();
        var searchRequest = new SearchRequest(
            Ldap.BaseDn,
            "(objectClass=*)",
            SearchScope.Subtree,
            null);
        DirectoryResponse response = conn.SendRequest(searchRequest);
        await Assert.That(response).IsTypeOf<SearchResponse>();
    }

    [Test]
    public async Task SendRequestWithTimeoutSearchRequestReturnsEntries()
    {
        using TraceableLdapConnection conn = Ldap.CreateConnection();
        conn.Bind();
        var searchRequest = new SearchRequest(
            Ldap.BaseDn,
            "(objectClass=*)",
            SearchScope.Subtree,
            null);
        DirectoryResponse response = conn.SendRequest(searchRequest, TimeSpan.FromSeconds(5));
        await Assert.That(response).IsTypeOf<SearchResponse>();
    }

    [Test]
    public async Task BeginSendRequestAndEndSendRequestWorks()
    {
        using TraceableLdapConnection conn = Ldap.CreateConnection();
        conn.Bind();
        var searchRequest = new SearchRequest(
            Ldap.BaseDn,
            "(objectClass=*)",
            SearchScope.Subtree,
            null);
        IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, PartialResultProcessing.NoPartialResultSupport, callback: default!, state: default!);
        DirectoryResponse response = conn.EndSendRequest(asyncResult);
        await Assert.That(response).IsTypeOf<SearchResponse>();
    }

    [Test]
    public async Task BeginSendRequestWithTimeoutAndEndSendRequestWorks()
    {
        using TraceableLdapConnection conn = Ldap.CreateConnection();
        conn.Bind();
        var searchRequest = new SearchRequest(
            Ldap.BaseDn,
            "(objectClass=*)",
            SearchScope.Subtree,
            null);
        IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, TimeSpan.FromSeconds(5), PartialResultProcessing.NoPartialResultSupport, callback: default!, state: default!);
        DirectoryResponse response = conn.EndSendRequest(asyncResult);
        await Assert.That(response).IsTypeOf<SearchResponse>();
    }

    [Test]
    public async Task GetPartialResultsReturnsKnownAdmin()
    {
        using TraceableLdapConnection conn = Ldap.CreateConnection();
        conn.Bind();
        var searchRequest = new SearchRequest(
            Ldap.BaseDn,
            "(objectClass=*)",
            SearchScope.Subtree,
            null);
        IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, PartialResultProcessing.ReturnPartialResults, callback: default!, state: default!);
        PartialResultsCollection? partialResults = conn.GetPartialResults(asyncResult);
        await Assert.That(partialResults).HasMember(p => p!.Count).EqualTo(1);
    }

    [Test]
    public async Task AbortShouldNotThrow()
    {
        using TraceableLdapConnection conn = Ldap.CreateConnection();
        conn.Bind();
        var searchRequest = new SearchRequest(
            Ldap.BaseDn,
            "(objectClass=*)",
            SearchScope.Subtree,
            null);
        IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, PartialResultProcessing.NoPartialResultSupport, callback: default!, state: default!);
        conn.Abort(asyncResult);
    }

    [Test]
    public async Task DisposeShouldNotThrow()
    {
        TraceableLdapConnection conn = Ldap.CreateConnection();
        conn.Dispose();
    }
}
