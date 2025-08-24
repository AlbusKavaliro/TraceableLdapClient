using System.DirectoryServices.Protocols;
using System.Net;
using Xunit;

namespace TraceableLdapClient.Tests;

public class TraceableLdapConnectionIntegrationTests : TraceableLdapConnectionTestBase
{
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
