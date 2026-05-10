using System.DirectoryServices.Protocols;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace AlbusKavaliro.TraceableLdapClient;

/// <inheritdoc cref="LdapConnection"/>
public interface ILdapConnection : IDisposable
{
    /// <inheritdoc cref="LdapConnection.AuthType"/>
    AuthType AuthType { get; set; }

    /// <inheritdoc cref="LdapConnection.AutoBind"/>
    bool AutoBind { get; set; }

#pragma warning disable S2376 // Setter-only property for compatibility with LdapConnection
#pragma warning disable CA1044 // Properties should not be write only for compatibility with LdapConnection
    /// <inheritdoc cref="LdapConnection.Credential"/>
    NetworkCredential Credential { set; }
#pragma warning restore CA1044
#pragma warning restore S2376

    /// <inheritdoc cref="LdapConnection.ClientCertificates"/>
    X509CertificateCollection ClientCertificates { get; }

    /// <inheritdoc cref="LdapConnection.Directory"/>
    DirectoryIdentifier Directory { get; }

    /// <inheritdoc cref="LdapConnection.SessionOptions"/>
    LdapSessionOptions SessionOptions { get; }

    /// <inheritdoc cref="LdapConnection.Timeout"/>
    TimeSpan Timeout { get; set; }

    /// <inheritdoc cref="LdapConnection.Abort"/>
    void Abort(IAsyncResult asyncResult);

    /// <inheritdoc cref="LdapConnection.BeginSendRequest"/>
    IAsyncResult BeginSendRequest(DirectoryRequest request, PartialResultProcessing partialMode, AsyncCallback callback, object state);

    /// <inheritdoc cref="LdapConnection.BeginSendRequest"/>
    IAsyncResult BeginSendRequest(DirectoryRequest request, TimeSpan requestTimeout, PartialResultProcessing partialMode, AsyncCallback callback, object state);

    /// <inheritdoc cref="LdapConnection.Bind"/>
    void Bind();

    /// <inheritdoc cref="LdapConnection.Bind"/>
    void Bind(NetworkCredential newCredential);

    /// <inheritdoc cref="LdapConnection.EndSendRequest"/>
    DirectoryResponse EndSendRequest(IAsyncResult asyncResult);

    /// <inheritdoc cref="LdapConnection.GetPartialResults"/>
    PartialResultsCollection? GetPartialResults(IAsyncResult asyncResult);

    /// <inheritdoc cref="LdapConnection.SendRequest"/>
    DirectoryResponse SendRequest(DirectoryRequest request);

    /// <inheritdoc cref="LdapConnection.SendRequest"/>
    DirectoryResponse SendRequest(DirectoryRequest request, TimeSpan requestTimeout);
}
