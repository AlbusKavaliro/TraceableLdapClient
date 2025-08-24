using System.DirectoryServices.Protocols;
using System.Net;
using Ass = TUnit.Assertions.Assert;

namespace TraceableLdapClient.Tests;

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
        await Ass.That(response).IsTypeOf<SearchResponse>();
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
        await Ass.That(response).IsTypeOf<SearchResponse>();
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
        await Ass.That(response).IsTypeOf<SearchResponse>();
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
        await Ass.That(response).IsTypeOf<SearchResponse>();
    }

    [Test]
    public async Task GetPartialResultsReturnsNullOrCollection()
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
        await Ass.That(partialResults).IsNull();
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
