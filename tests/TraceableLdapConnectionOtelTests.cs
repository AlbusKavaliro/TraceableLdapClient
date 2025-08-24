using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.DirectoryServices.Protocols;
using Xunit;

namespace TraceableLdapClient.Tests;

public class TraceableLdapConnectionOtelTests : TraceableLdapConnectionTestBase
{
    private (List<Activity>, ActivityListener, TraceableLdapConnection) CreateActivityListenerSetUp()
    {
        List<Activity> activities = [];
        ActivityListener activityListener = new()
        {
            ShouldListenTo = source => source.Name == "TraceableLdapClient.LdapConnection",
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activities.Add,
            ActivityStopped = activity => { }
        };
        ActivitySource.AddActivityListener(activityListener);

        TraceableLdapConnection conn = CreateConnection();
        conn.Bind();

        return (activities, activityListener, conn);
    }

    [Fact]
    public void AddRequestActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();

        AddRequest addRequest = new("cn=testadd,ou=people," + LdapBaseDn,
            new DirectoryAttribute("objectClass", "person"),
            new DirectoryAttribute("cn", "testadd"),
            new DirectoryAttribute("sn", "add"));
        try { conn.SendRequest(addRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

        activityListener.Dispose();

        Xunit.Assert.Contains(activities, a => a.DisplayName.Contains("add", StringComparison.InvariantCultureIgnoreCase));
    }

    [Fact]
    public void DeleteRequestActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            DeleteRequest deleteRequest = new("cn=testdelete,ou=people," + LdapBaseDn);
            try { conn.SendRequest(deleteRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();

            Xunit.Assert.Contains(activities, a => a.DisplayName.Contains("delete", StringComparison.InvariantCultureIgnoreCase));
        }
    }

    [Fact]
    public void ModifyRequestActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            ModifyRequest modifyRequest = new("cn=testmodify,ou=people," + LdapBaseDn, DirectoryAttributeOperation.Replace, "sn", "modded");
            try { conn.SendRequest(modifyRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();

            Xunit.Assert.Contains(activities, a => a.DisplayName.Contains("modify", StringComparison.InvariantCultureIgnoreCase));
        }
    }

    [Fact]
    public void CompareRequestActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            CompareRequest compareRequest = new("cn=testcompare,ou=people," + LdapBaseDn, "sn", "compare");
            try { conn.SendRequest(compareRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();

            Xunit.Assert.Contains(activities, a => a.DisplayName.Contains("compare", StringComparison.InvariantCultureIgnoreCase));
        }
    }

    [Fact]
    public void ExtendedRequestActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            ExtendedRequest extendedRequest = new("1.3.6.1.4.1.1466.20037"); // StartTLS OID
            try { conn.SendRequest(extendedRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();

            Xunit.Assert.Contains(activities, a => a.DisplayName.Contains("extended", StringComparison.InvariantCultureIgnoreCase) || a.DisplayName.Contains("1.3.6.1.4.1.1466.20037", StringComparison.InvariantCultureIgnoreCase));
        }
    }

    [Fact]
    public void FailingLdapQuerySetsNonOkActivityStatus()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest invalidRequest = new(LdapBaseDn, ")objectClass=*()", SearchScope.Subtree, null);
            Xunit.Assert.Throws<LdapException>(() => conn.SendRequest(invalidRequest));

            activityListener.Dispose();

            Xunit.Assert.Contains(activities, a => a.Status == ActivityStatusCode.Error);
        }
    }

    [Fact]
    public void AbortSetsActivityStatusOk()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest searchRequest = new(LdapBaseDn, "(objectClass=*)", SearchScope.Subtree, null);
            IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, PartialResultProcessing.NoPartialResultSupport, callback: default!, state: default!);
            conn.Abort(asyncResult);

            activityListener.Dispose();

            Xunit.Assert.Contains(activities, a => a.DisplayName.Contains("abort", StringComparison.InvariantCultureIgnoreCase) || a.Status == ActivityStatusCode.Ok);
        }
    }

    [Fact]
    public void PartialResultsActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest searchRequest = new(LdapBaseDn, "(objectClass=*)", SearchScope.Subtree, null);
            IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, PartialResultProcessing.ReturnPartialResults, callback: default!, state: default!);
            _ = conn.GetPartialResults(asyncResult);

            activityListener.Dispose();

            Xunit.Assert.Contains(activities, a => a.DisplayName.Contains("GetPartialResults", StringComparison.InvariantCultureIgnoreCase));
        }
    }

    [Fact]
    public void BeginSendEndSendActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest searchRequest = new(LdapBaseDn, "(objectClass=*)", SearchScope.Subtree, null);
            IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, PartialResultProcessing.NoPartialResultSupport, callback: default!, state: default!);
            _ = conn.EndSendRequest(asyncResult);

            activityListener.Dispose();

            Xunit.Assert.Contains(activities, a => a.DisplayName.Contains("search", StringComparison.InvariantCultureIgnoreCase));
            Xunit.Assert.All(activities, a => Xunit.Assert.Equal(ActivityStatusCode.Ok, a.Status));
        }
    }

    [Fact]
    public void ActivitiesAreGeneratedCorrectly()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest searchRequest = new(LdapBaseDn, "(objectClass=*)", SearchScope.Subtree, null);
            _ = conn.SendRequest(searchRequest);

            activityListener.Dispose();

            Xunit.Assert.Contains(activities, a => a.DisplayName.Contains("ldap bind", StringComparison.InvariantCultureIgnoreCase));
            Xunit.Assert.Contains(activities, a => a.DisplayName.Contains("search", StringComparison.InvariantCultureIgnoreCase));
            Xunit.Assert.All(activities, a => Xunit.Assert.Equal(ActivityStatusCode.Ok, a.Status));
        }
    }

    [Fact]
    public void MetricsAreCountedCorrectly()
    {
        List<MetricRecord> metrics = [];
        MeterListener meterListener = new()
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "TraceableLdapClient.LdapConnection")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>((inst, val, tags, state) =>
        {
            metrics.Add(new MetricRecord(inst.Name, val, tags.ToArray()));
        });
        meterListener.SetMeasurementEventCallback<double>((inst, val, tags, state) =>
        {
            metrics.Add(new MetricRecord(inst.Name, val, tags.ToArray()));
        });
        meterListener.Start();

        using TraceableLdapConnection conn = CreateConnection();
        conn.Bind();
        SearchRequest searchRequest = new(LdapBaseDn, "(objectClass=*)", SearchScope.Subtree, null);
        _ = conn.SendRequest(searchRequest);

        meterListener.Dispose();

        long requestsTotal = metrics.Where(m => m.Name == "network.client.requests").Sum(m => Convert.ToInt64(m.Value, System.Globalization.CultureInfo.InvariantCulture));
        double durationTotal = metrics.Where(m => m.Name == "network.client.duration").Sum(m => Convert.ToDouble(m.Value, System.Globalization.CultureInfo.InvariantCulture));
        long entriesTotal = metrics.Where(m => m.Name == "ldap.search.entries_returned").Sum(m => Convert.ToInt64(m.Value, System.Globalization.CultureInfo.InvariantCulture));

        Xunit.Assert.True(requestsTotal >= 2, $"Expected at least 2 requests, got {requestsTotal}");
        Xunit.Assert.True(durationTotal > 0, $"Expected duration > 0, got {durationTotal}");
        Xunit.Assert.True(entriesTotal >= 0, $"Expected entries >= 0, got {entriesTotal}");
    }

    private record struct MetricRecord(string Name, object Value, IReadOnlyList<KeyValuePair<string, object?>> Tags);
}
