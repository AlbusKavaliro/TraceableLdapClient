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
        Activity? activity = StartActivity(OtelOperations.Bind);
        long start = Stopwatch.GetTimestamp();
        s_requestCounter.Add(1);
        try
        {
            _inner.Bind();
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
        }
    }

    public void Bind(NetworkCredential newCredential)
    {
        using Activity? activity = StartActivity(OtelOperations.Bind);
        long start = Stopwatch.GetTimestamp();
        s_requestCounter.Add(1);
        activity?.SetTag(OtelTags.Username, newCredential?.UserName);
        try
        {
            _inner.Bind(newCredential);
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
        }
    }

    public DirectoryResponse SendRequest(DirectoryRequest request)
    {
        using Activity? activity = StartActivity(request);
        long start = Stopwatch.GetTimestamp();
        s_requestCounter.Add(1);
        ArgumentNullException.ThrowIfNull(request);
        activity?.SetTag(OtelTags.RequestType, request.GetType().Name);
        activity?.SetTag(OtelTags.DistinguishedName, request is SearchRequest sr ? sr.DistinguishedName : null);
        try
        {
            DirectoryResponse response = _inner.SendRequest(request);
            activity?.SetTag(OtelTags.ResponseType, response.GetType().Name);
            activity?.SetTag(OtelTags.ResultCode, response.ResultCode.ToString());
            activity?.SetTag(OtelTags.ErrorMessage, response.ErrorMessage);
            activity?.SetStatus(ActivityStatusCode.Ok);
            if (response is SearchResponse searchResponse)
            {
                s_searchEntryCounter.Add(searchResponse.Entries.Count);
            }

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
        using Activity? activity = StartActivity(request);
        long start = Stopwatch.GetTimestamp();
        s_requestCounter.Add(1);
        ArgumentNullException.ThrowIfNull(request);
        activity?.SetTag(OtelTags.RequestType, request.GetType().Name);
        activity?.SetTag(OtelTags.Timeout, requestTimeout.ToString());
        try
        {
            DirectoryResponse response = _inner.SendRequest(request, requestTimeout);
            activity?.SetTag(OtelTags.ResponseType, response.GetType().Name);
            activity?.SetTag(OtelTags.ResultCode, response.ResultCode.ToString());
            activity?.SetTag(OtelTags.ErrorMessage, response.ErrorMessage);
            activity?.SetStatus(ActivityStatusCode.Ok);
            if (response is SearchResponse searchResponse)
            {
                s_searchEntryCounter.Add(searchResponse.Entries.Count);
            }
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
        activity?.SetTag(OtelTags.RequestType, request.GetType().Name);
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
        activity?.SetTag(OtelTags.RequestType, request.GetType().Name);
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
            activity?.SetTag(OtelTags.ResponseType, response.GetType().Name);
            activity?.SetTag(OtelTags.ResultCode, response.ResultCode.ToString());
            activity?.SetTag(OtelTags.ErrorMessage, response.ErrorMessage);
            activity?.SetStatus(ActivityStatusCode.Ok);
            if (response is SearchResponse searchResponse)
            {
                s_searchEntryCounter.Add(searchResponse.Entries.Count);
            }
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
        if (activity is not null && request is SearchRequest sr)
        {
            activity.SetTag("ldap.search.base", sr.DistinguishedName);
            activity.SetTag("ldap.search.scope", sr.Scope.ToString());
            activity.SetTag("ldap.search.filter", sr.Filter);
            activity.SetTag("ldap.search.attributes", string.Join(',', sr.Attributes));
            activity.SetTag("ldap.search.size_limit", sr.SizeLimit);
            activity.SetTag("ldap.search.time_limit", sr.TimeLimit);
        }

        return activity;
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
        public const string ExceptionType = "exception.type";
        public const string ExceptionMessage = "exception.message";
        public const string ExceptionStacktrace = "exception.stacktrace";
        public const string RequestType = "requestType";
        public const string Operation = "ldap.operation";
        public const string ResponseType = "ldap.response.type";
        public const string ResultCode = "ldap.response.result_code";
        public const string ErrorMessage = "ldap.response.error_message";
        public const string Username = "username";
        public const string Timeout = "timeout";
        public const string Aborted = "aborted";
        public const string PartialResultsCount = "partialResultsCount";
        public const string DistinguishedName = "distinguishedName";
    }

    private static void SetException(Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag(OtelTags.ExceptionType, ex.GetType().FullName);
        activity?.SetTag(OtelTags.ExceptionMessage, ex.Message);
        activity?.SetTag(OtelTags.ExceptionStacktrace, ex.StackTrace);
    }

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
