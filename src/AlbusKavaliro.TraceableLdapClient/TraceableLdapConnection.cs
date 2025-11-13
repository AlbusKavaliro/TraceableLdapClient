using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace AlbusKavaliro.TraceableLdapClient;

public class TraceableLdapConnection : ILdapConnection
{
    private static readonly Meter s_meter = new("TraceableLdapClient.LdapConnection");

    private static readonly Counter<long> s_requestCounter = s_meter.CreateCounter<long>("network.client.requests");

    private static readonly Counter<long> s_errorCounter = s_meter.CreateCounter<long>("network.client.errors");

    private static readonly Histogram<double> s_durationHistogram = s_meter.CreateHistogram<double>("network.client.duration", unit: "ms");

    private static readonly Counter<long> s_searchEntryCounter = s_meter.CreateCounter<long>("ldap.search.entries_returned");

    private readonly ActivitySource _activitySource = new("TraceableLdapClient.LdapConnection");

    private readonly LdapConnection _inner;

    private bool _disposed;

    public AuthType AuthType
    {
        get => _inner.AuthType;
        set => _inner.AuthType = value;
    }

    public bool AutoBind
    {
        get => _inner.AutoBind;
        set => _inner.AutoBind = value;
    }

    public X509CertificateCollection ClientCertificates => _inner.ClientCertificates;

#pragma warning disable S2376 // Setter-only property for compatibility with LdapConnection
    public NetworkCredential Credential
    {
        set => _inner.Credential = value;
    }
#pragma warning restore S2376

    public DirectoryIdentifier Directory => _inner.Directory;

    public LdapSessionOptions SessionOptions => _inner.SessionOptions;

    public TimeSpan Timeout
    {
        get => _inner.Timeout;
        set => _inner.Timeout = value;
    }

    public TraceableLdapConnection(string server)
    {
        _inner = new LdapConnection(server);
    }


    public TraceableLdapConnection(LdapDirectoryIdentifier identifier)
    {
        _inner = new LdapConnection(identifier);
    }

    public TraceableLdapConnection(LdapDirectoryIdentifier identifier, NetworkCredential? credential)
    {
        _inner = new LdapConnection(identifier, credential);
    }

    public TraceableLdapConnection(LdapDirectoryIdentifier identifier, NetworkCredential? credential, AuthType authType)
    {
        _inner = new LdapConnection(identifier, credential, authType);
    }

    public void Bind()
    {
        using Activity? activity = StartActivity(OtelOperations.Bind);
        long start = Stopwatch.GetTimestamp();
        s_requestCounter.Add(1);

        activity?.SetTag(OtelTags.BindType, "simple");

        try
        {
            _inner.Bind();
            activity?.SetTag(OtelTags.AuthResult, "success");
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            s_errorCounter.Add(1);
            activity?.SetTag(OtelTags.AuthResult, GetAuthResultFromException(ex));
            SetException(activity, ex);
            throw;
        }
        finally
        {
            s_durationHistogram.Record(GetElapsedMilliseconds(start));
        }
    }

    public void Bind(NetworkCredential newCredential)
    {
        using Activity? activity = StartActivity(OtelOperations.Bind);
        long start = Stopwatch.GetTimestamp();
        s_requestCounter.Add(1);

        if (activity is not null)
        {
            activity.SetTag(OtelTags.BindType, DetermineBindType(newCredential));
            if (!string.IsNullOrEmpty(newCredential?.UserName))
            {
                activity.SetTag(OtelTags.BindDn, newCredential.UserName);
            }
        }

        try
        {
            _inner.Bind(newCredential);
            activity?.SetTag(OtelTags.AuthResult, "success");
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            s_errorCounter.Add(1);
            activity?.SetTag(OtelTags.AuthResult, GetAuthResultFromException(ex));
            SetException(activity, ex);
            throw;
        }
        finally
        {
            s_durationHistogram.Record(GetElapsedMilliseconds(start));
        }
    }

    public DirectoryResponse SendRequest(DirectoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        using Activity? activity = StartActivity(request);
        long start = Stopwatch.GetTimestamp();
        s_requestCounter.Add(1);

        try
        {
            DirectoryResponse response = _inner.SendRequest(request);
            SetResponseAttributes(activity, response);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return response;
        }
        catch (Exception ex)
        {
            s_errorCounter.Add(1);
            SetException(activity, ex);
            throw;
        }
        finally
        {
            s_durationHistogram.Record(GetElapsedMilliseconds(start));
        }
    }

    public DirectoryResponse SendRequest(DirectoryRequest request, TimeSpan requestTimeout)
    {
        ArgumentNullException.ThrowIfNull(request);

        using Activity? activity = StartActivity(request);
        long start = Stopwatch.GetTimestamp();
        s_requestCounter.Add(1);
        activity?.SetTag(OtelTags.Timeout, requestTimeout.ToString());

        try
        {
            DirectoryResponse response = _inner.SendRequest(request, requestTimeout);
            SetResponseAttributes(activity, response);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return response;
        }
        catch (Exception ex)
        {
            s_errorCounter.Add(1);
            SetException(activity, ex);
            throw;
        }
        finally
        {
            s_durationHistogram.Record(GetElapsedMilliseconds(start));
        }
    }

    public IAsyncResult BeginSendRequest(DirectoryRequest request, PartialResultProcessing partialMode, AsyncCallback callback, object state)
    {
        ArgumentNullException.ThrowIfNull(request);
        Activity? activity = StartActivity(request);
        long start = Stopwatch.GetTimestamp();
        s_requestCounter.Add(1);

        try
        {
            IAsyncResult innerResult = _inner.BeginSendRequest(request, partialMode, callback, state);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return new TracedAsyncResult(innerResult, activity, start);
        }
        catch (Exception ex)
        {
            s_errorCounter.Add(1);
            SetException(activity, ex);
            throw;
        }
    }

    public IAsyncResult BeginSendRequest(DirectoryRequest request, TimeSpan requestTimeout, PartialResultProcessing partialMode, AsyncCallback callback, object state)
    {
        ArgumentNullException.ThrowIfNull(request);
        Activity? activity = StartActivity(request);
        long start = Stopwatch.GetTimestamp();
        s_requestCounter.Add(1);

        activity?.SetTag(OtelTags.Timeout, requestTimeout.ToString());
        try
        {
            IAsyncResult innerResult = _inner.BeginSendRequest(request, requestTimeout, partialMode, callback, state);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return new TracedAsyncResult(innerResult, activity, start);
        }
        catch (Exception ex)
        {
            s_errorCounter.Add(1);
            SetException(activity, ex);
            throw;
        }
    }

    public DirectoryResponse EndSendRequest(IAsyncResult asyncResult)
    {
        Activity? activity = null;
        long start = 0;
        if (asyncResult is TracedAsyncResult traced)
        {
            activity = traced.Activity;
            asyncResult = traced.Inner;
            start = traced.StartTimestamp;
        }
        try
        {
            DirectoryResponse response = _inner.EndSendRequest(asyncResult);
            SetResponseAttributes(activity, response);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return response;
        }
        catch (Exception ex)
        {
            s_errorCounter.Add(1);
            SetException(activity, ex);
            throw;
        }
        finally
        {
            s_durationHistogram.Record(GetElapsedMilliseconds(start));
            activity?.Dispose();
        }
    }

    public PartialResultsCollection? GetPartialResults(IAsyncResult asyncResult)
    {
        Activity? parentActivity = null;
        if (asyncResult is TracedAsyncResult traced)
        {
            parentActivity = traced.Activity;
            asyncResult = traced.Inner;
        }

        using Activity? subActivity = _activitySource.StartActivity(nameof(GetPartialResults), ActivityKind.Internal, parentActivity?.Context ?? default);
        long start = Stopwatch.GetTimestamp();
        try
        {
            PartialResultsCollection? results = _inner.GetPartialResults(asyncResult);
            subActivity?.SetTag(OtelTags.PartialResultsCount, results?.Count ?? 0);
            subActivity?.SetStatus(ActivityStatusCode.Ok);
            return results;
        }
        catch (Exception ex)
        {
            s_errorCounter.Add(1);
            SetException(subActivity, ex);
            throw;
        }
        finally
        {
            s_durationHistogram.Record(GetElapsedMilliseconds(start));
        }
    }

    public void Abort(IAsyncResult asyncResult)
    {
        Activity? activity = null;
        long start = 0;
        if (asyncResult is TracedAsyncResult traced)
        {
            activity = traced.Activity;
            asyncResult = traced.Inner;
            start = traced.StartTimestamp;
        }
        try
        {
            _inner.Abort(asyncResult);
            activity?.SetTag(OtelTags.Aborted, true);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            s_errorCounter.Add(1);
            SetException(activity, ex);
            throw;
        }
        finally
        {
            s_durationHistogram.Record(GetElapsedMilliseconds(start));
            activity?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _inner.Dispose();
                _activitySource.Dispose();
            }

            _disposed = true;
        }
    }

    private Activity? StartActivity(DirectoryRequest request, ActivityKind kind = ActivityKind.Client)
    {
        (string operation, string target) = GetActivityInfo(request);
        Activity? activity = StartActivity(operation, target, kind);

        if (activity is not null)
        {
            activity.SetTag(OtelTags.RequestType, request.GetType().Name);
            SetRequestSpecificAttributes(activity, request);
        }

        return activity;
    }

#pragma warning disable CA1308 // Normalize strings to uppercase - OTEL semantic conventions
    private static void SetRequestSpecificAttributes(Activity activity, DirectoryRequest request)
    {
        switch (request)
        {
            case SearchRequest sr:
                SetSearchRequestAttributes(activity, sr);
                break;

            case AddRequest ar:
                SetAddRequestAttributes(activity, ar);
                break;

            case ModifyRequest mr:
                SetModifyRequestAttributes(activity, mr);
                break;

            case DeleteRequest dr:
                activity.SetTag(OtelTags.DistinguishedName, dr.DistinguishedName);
                break;

            case CompareRequest cr:
                activity.SetTag(OtelTags.DistinguishedName, cr.DistinguishedName);
                activity.SetTag(OtelTags.CompareAttribute, cr.Assertion.Name);
                break;

            case ExtendedRequest er:
                activity.SetTag(OtelTags.ExtendedOperationOid, er.RequestName);
                // Map common extended operation OIDs to friendly names
                activity.SetTag(OtelTags.ExtendedOperationName, GetExtendedOperationName(er.RequestName));
                break;
        }
    }

    private static void SetSearchRequestAttributes(Activity activity, SearchRequest sr)
    {
        activity.SetTag(OtelTags.SearchBase, sr.DistinguishedName);
        activity.SetTag(OtelTags.SearchScope, GetDictionaryValueOrLowerString(s_searchScopeNames, sr.Scope));
        activity.SetTag(OtelTags.SearchFilter, sr.Filter);
        activity.SetTag(OtelTags.SearchSizeLimit, sr.SizeLimit);
        activity.SetTag(OtelTags.SearchTimeLimit, sr.TimeLimit.TotalSeconds);
        activity.SetTag(OtelTags.SearchTypesOnly, sr.TypesOnly);
        activity.SetTag(
            OtelTags.SearchDerefAliases,
            GetDereferenceAliasName(sr.Aliases)
        );

        int attrCount = sr.Attributes.Count;
        if (attrCount > 0)
        {
            activity.SetTag(OtelTags.SearchAttributes, string.Join(',', sr.Attributes.Cast<string>()));
        }
    }

    private static void SetAddRequestAttributes(Activity activity, AddRequest ar)
    {
        activity.SetTag(OtelTags.DistinguishedName, ar.DistinguishedName);
        int attributeCount = ar.Attributes.Count;
        activity.SetTag(OtelTags.AddAttributeCount, attributeCount);
        if (attributeCount > 0)
        {
            var attributeNames = new List<string>(attributeCount);
            foreach (DirectoryAttribute attribute in ar.Attributes)
            {
                attributeNames.Add(attribute.Name);
            }

            activity.SetTag(OtelTags.AddAttributes, string.Join(',', attributeNames));
        }
    }

    private static string GetDereferenceAliasName(DereferenceAlias alias)
    {
        if (s_dereferenceAliasNames.TryGetValue(alias, out string? name))
        {
            return name;
        }
        return s_dereferenceAliasFallbackNames[alias];
    }

    private static void SetModifyRequestAttributes(Activity activity, ModifyRequest mr)
    {
        activity.SetTag(OtelTags.DistinguishedName, mr.DistinguishedName);
        int modCount = mr.Modifications.Count;
        activity.SetTag(OtelTags.ModifyAttributeCount, modCount);
        if (modCount > 0)
        {
            var operationNames = new List<string>(modCount);
            var attributeNames = new List<string>(modCount);
            foreach (object? modificationObj in mr.Modifications)
            {
                if (modificationObj is DirectoryAttributeModification modification)
                {
                    string operationName = GetDictionaryValueOrLowerString(s_modifyOperationNames, modification.Operation);
                    operationNames.Add(operationName);
                    attributeNames.Add(modification.Name);
                }
            }

            activity.SetTag(OtelTags.ModifyAttributes, string.Join(',', attributeNames));
            activity.SetTag(OtelTags.ModifyOperation, string.Join(',', operationNames));
        }
    }

    private static class LdapOids
    {
        public const string StartTls = "1.3.6.1.4.1.1466.20037";
        public const string ModifyPassword = "1.3.6.1.4.1.4203.1.11.1";
        public const string WhoAmI = "1.3.6.1.4.1.4203.1.11.3";
        public const string Cancel = "1.3.6.1.1.8";
        public const string PagedResults = "1.2.840.113556.1.4.319";
        public const string Notification = "1.2.840.113556.1.4.528";
    }

    private static readonly Dictionary<SearchScope, string> s_searchScopeNames = new()
    {
        { SearchScope.Base, "base" },
        { SearchScope.OneLevel, "onelevel" },
        { SearchScope.Subtree, "subtree" }
    };

    private static readonly Dictionary<DereferenceAlias, string> s_dereferenceAliasNames = new()
    {
        { DereferenceAlias.Never, "never" },
        { DereferenceAlias.InSearching, "searching" },
        { DereferenceAlias.FindingBaseObject, "finding" },
        { DereferenceAlias.Always, "always" }
    };

#pragma warning disable CA1308 // Normalize strings to uppercase - OTEL semantic conventions
    private static readonly Dictionary<DereferenceAlias, string> s_dereferenceAliasFallbackNames =
        Enum.GetValues<DereferenceAlias>()
            .ToDictionary(
                v => v,
                v => Enum.GetName(v)?.ToLowerInvariant() ?? v.ToString().ToLowerInvariant()
            );
#pragma warning restore CA1308 // Normalize strings to uppercase - OTEL semantic conventions

    private static readonly Dictionary<DirectoryAttributeOperation, string> s_modifyOperationNames = new()
    {
        { DirectoryAttributeOperation.Add, "add" },
        { DirectoryAttributeOperation.Delete, "delete" },
        { DirectoryAttributeOperation.Replace, "replace" }
    };

    private static string GetExtendedOperationName(string oid)
    {
        return oid switch
        {
            LdapOids.StartTls => "start_tls",
            LdapOids.ModifyPassword => "modify_password",
            LdapOids.WhoAmI => "who_am_i",
            LdapOids.Cancel => "cancel",
            LdapOids.PagedResults => "paged_results",
            LdapOids.Notification => "notification",
            _ => oid
        };
    }

    private Activity? StartActivity(string operation, string? target = null, ActivityKind kind = ActivityKind.Client)
    {
        Activity? activity = _activitySource.StartActivity($"ldap {operation} {target}".TrimEnd(), kind);
        if (activity is not null)
        {
            SetNetworkTags(activity);
            activity.SetTag(OtelTags.Operation, operation);
        }

        return activity;
    }

    private const int LdapsPort = 636;

    private void SetNetworkTags(Activity activity)
    {
        activity.SetTag("network.protocol.name", "ldap");
        activity.SetTag("network.transport", "tcp");

        if (Directory is LdapDirectoryIdentifier ldapId)
        {
            if (ldapId.Servers != null && ldapId.Servers.Length > 0)
            {
                activity.SetTag("server.address", ldapId.Servers[0]);
            }

            activity.SetTag("server.port", ldapId.PortNumber);

            // Determine connection type and encryption
            // Note: This logic uses configuration flags and port number to infer intended encryption,
            // but may not reflect the actual encryption state of the connection.
            bool isLdaps = ldapId.PortNumber == LdapsPort;
            bool isEncrypted = isLdaps || SessionOptions.SecureSocketLayer;

            activity.SetTag(OtelTags.ConnectionType, isLdaps ? "ldaps" : isEncrypted ? "starttls" : "plain");
            activity.SetTag(OtelTags.ConnectionEncrypted, isEncrypted);

            // Add TLS version if available and encrypted
            if (isEncrypted && SessionOptions.SslInformation?.Protocol != null)
            {
                activity.SetTag(OtelTags.TlsVersion, SessionOptions.SslInformation.Protocol.ToString());
            }
        }
    }

    private static (string operation, string target) GetActivityInfo(DirectoryRequest request)
    {
#pragma warning disable CA1308 // Normalize strings to uppercase - OTEL semantic conventions
        string operation = request.GetType().Name.Replace("Request", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase - OTEL semantic conventions
        string target = string.Empty;

        if (request is SearchRequest sr)
        {
            target = $"{sr.DistinguishedName} ({sr.Filter})";
        }
        else if (request is AddRequest ar)
        {
            target = ar.DistinguishedName;
        }
        else if (request is DeleteRequest dr)
        {
            target = dr.DistinguishedName;
        }
        else if (request is ModifyRequest mr)
        {
            target = mr.DistinguishedName;
        }
        else if (request is CompareRequest cr)
        {
            target = cr.DistinguishedName;
        }
        else if (request is ExtendedRequest er)
        {
            target = er.RequestName;
        }

        return (operation, target);
    }

    private void SetResponseAttributes(Activity? activity, DirectoryResponse response)
    {
        if (activity is null)
        {
            return;
        }

        // Set standard response attributes
        activity.SetTag(OtelTags.ResponseType, response.GetType().Name);
        activity.SetTag(OtelTags.ResultCode, response.ResultCode.ToString());

        if (!string.IsNullOrEmpty(response.ErrorMessage))
        {
            activity.SetTag(OtelTags.ErrorMessage, response.ErrorMessage);
        }

        if (!string.IsNullOrEmpty(response.MatchedDN))
        {
            activity.SetTag(OtelTags.MatchedDn, response.MatchedDN);
        }

        if (response.Referral?.Length > 0)
        {
            activity.SetTag(OtelTags.Referrals, string.Join(',', response.Referral.Select(u => u.ToString())));
        }

        SetResponseSpecificAttributes(activity, response);
    }

    private void SetResponseSpecificAttributes(Activity activity, DirectoryResponse response)
    {
        switch (response)
        {
            case SearchResponse searchResponse:
                int entryCount = searchResponse.Entries.Count;
                activity.SetTag(OtelTags.SearchEntriesReturned, entryCount);

                // Record search entries metric with proper tags
                s_searchEntryCounter.Add(entryCount,
                [
                    new("ldap.operation", "search"),
                    new("ldap.response.result_code", (int)response.ResultCode),
                    new("server.address", GetServerAddress())
                ]);
                break;

            case CompareResponse compareResponse:
                if (compareResponse.ResultCode == ResultCode.CompareTrue)
                {
                    activity.SetTag(OtelTags.CompareResult, "true");
                }
                else if (compareResponse.ResultCode == ResultCode.CompareFalse)
                {
                    activity.SetTag(OtelTags.CompareResult, "false");
                }
                else
                {
                    activity.SetTag(OtelTags.CompareResult, "error");
                }
                break;

            case ExtendedResponse extendedResponse:
                if (!string.IsNullOrEmpty(extendedResponse.ResponseName))
                {
                    activity.SetTag(OtelTags.ExtendedOperationOid, extendedResponse.ResponseName);
                    activity.SetTag(OtelTags.ExtendedOperationName, GetExtendedOperationName(extendedResponse.ResponseName));
                }
                break;
        }
    }

    private string? GetServerAddress()
    {
        if (Directory is LdapDirectoryIdentifier ldapId &&
            ldapId.Servers?.Length > 0)
        {
            return ldapId.Servers[0];
        }
        return null;
    }

    private static string DetermineBindType(NetworkCredential? credential)
    {
        if (credential is null)
            return "anonymous";

        // In .NET LDAP, simple bind is the default when credentials are provided
        // SASL mechanisms would require additional configuration
        return "simple";
    }

    private static string GetAuthResultFromException(Exception ex)
    {
        return ex switch
        {
            DirectoryOperationException doe when doe.Response?.ResultCode == ResultCode.InsufficientAccessRights => "insufficient_access",
            DirectoryOperationException doe when doe.Response?.ResultCode == ResultCode.UnwillingToPerform => "unwilling_to_perform",
            DirectoryOperationException doe when doe.Response?.ResultCode == ResultCode.StrongAuthRequired => "strong_auth_required",
            _ => "error"
        };
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
    {
        if (startTimestamp == 0) return 0;
        return Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
    }

    private static class OtelOperations
    {
        public const string Bind = "bind";
    }

    private static class OtelTags
    {
        // Exception attributes (standard OTel)
        public const string ExceptionType = "exception.type";
        public const string ExceptionMessage = "exception.message";
        public const string ExceptionStacktrace = "exception.stacktrace";

        // Core LDAP operation attributes
        public const string Operation = "ldap.operation";
        public const string RequestType = "ldap.request.type";

        // LDAP response attributes
        public const string ResponseType = "ldap.response.type";
        public const string ResultCode = "ldap.response.result_code";
        public const string ErrorMessage = "ldap.response.error_message";
        public const string MatchedDn = "ldap.response.matched_dn";
        public const string Referrals = "ldap.response.referrals";

        // Common LDAP attributes
        public const string DistinguishedName = "ldap.dn";

        // LDAP search attributes
        public const string SearchBase = "ldap.search.base";
        public const string SearchScope = "ldap.search.scope";
        public const string SearchFilter = "ldap.search.filter";
        public const string SearchAttributes = "ldap.search.attributes";
        public const string SearchSizeLimit = "ldap.search.size_limit";
        public const string SearchTimeLimit = "ldap.search.time_limit";
        public const string SearchEntriesReturned = "ldap.search.entries_returned";
        public const string SearchDerefAliases = "ldap.search.deref_aliases";
        public const string SearchTypesOnly = "ldap.search.types_only";

        // LDAP bind/authentication attributes
        public const string BindType = "ldap.bind.type";
        public const string BindDn = "ldap.bind.dn";
        public const string AuthResult = "ldap.auth.result";

        // LDAP modify attributes
        public const string ModifyOperation = "ldap.modify.operation";
        public const string ModifyAttributeCount = "ldap.modify.attribute_count";
        public const string ModifyAttributes = "ldap.modify.attributes";

        // LDAP add attributes
        public const string AddAttributeCount = "ldap.add.attribute_count";
        public const string AddAttributes = "ldap.add.attributes";

        // LDAP compare attributes
        public const string CompareAttribute = "ldap.compare.attribute";
        public const string CompareResult = "ldap.compare.result";

        // LDAP extended operation attributes
        public const string ExtendedOperationName = "ldap.extended.operation_name";
        public const string ExtendedOperationOid = "ldap.extended.operation_oid";

        // Connection and security attributes
        public const string ConnectionType = "ldap.connection.type";
        public const string ConnectionEncrypted = "ldap.connection.encrypted";
        public const string TlsVersion = "tls.protocol.version";

        public const string Timeout = "timeout";
        public const string Aborted = "aborted";
        public const string PartialResultsCount = "partialResultsCount";
    }

    private static void SetException(Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag(OtelTags.ExceptionType, ex.GetType().FullName);
        activity?.SetTag(OtelTags.ExceptionMessage, ex.Message);
        activity?.SetTag(OtelTags.ExceptionStacktrace, ex.StackTrace);
    }

#pragma warning disable CA1308 // Normalize strings to uppercase - OTEL semantic conventions
    private static string GetDictionaryValueOrLowerString<T>(Dictionary<T, string> dict, T key)
        where T : notnull
    {
        return dict.TryGetValue(key, out string? value)
            ? value
            : key?.ToString()?.ToLowerInvariant() ?? string.Empty;
    }
#pragma warning restore CA1308 // Normalize strings to uppercase - OTEL semantic conventions

    private sealed class TracedAsyncResult(IAsyncResult inner, Activity? activity, long startTimestamp = 0) : IAsyncResult
    {
        public IAsyncResult Inner { get; } = inner;
        public Activity? Activity { get; } = activity;
        public long StartTimestamp { get; } = startTimestamp;
        public object? AsyncState => Inner.AsyncState;
        public WaitHandle AsyncWaitHandle => Inner.AsyncWaitHandle;
        public bool CompletedSynchronously => Inner.CompletedSynchronously;
        public bool IsCompleted => Inner.IsCompleted;
    }
}
