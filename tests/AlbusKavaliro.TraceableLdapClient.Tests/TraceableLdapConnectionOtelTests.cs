using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.DirectoryServices.Protocols;

namespace AlbusKavaliro.TraceableLdapClient.Tests;

[NotInParallel]
public class TraceableLdapConnectionOtelTests
{
    [ClassDataSource<LdapContainer>(Shared = SharedType.PerTestSession)]
    public required LdapContainer Ldap { get; init; }

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

        TraceableLdapConnection conn = Ldap.CreateConnection();

        conn.Bind();

        return (activities, activityListener, conn);
    }

    [Test]
    public async Task AddRequestActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();

        AddRequest addRequest = new("cn=testadd,ou=people," + Ldap.BaseDn,
            new DirectoryAttribute("objectClass", "person"),
            new DirectoryAttribute("cn", "testadd"),
            new DirectoryAttribute("sn", "add"));
        try { conn.SendRequest(addRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

        activityListener.Dispose();
        await Assert.That(activities).Contains(a => a.DisplayName.Contains("add", StringComparison.InvariantCultureIgnoreCase));
    }

    [Test]
    public async Task DeleteRequestActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            DeleteRequest deleteRequest = new("cn=testdelete,ou=people," + Ldap.BaseDn);
            try { conn.SendRequest(deleteRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();
            await Assert.That(activities).Contains(a => a.DisplayName.Contains("delete", StringComparison.InvariantCultureIgnoreCase));
        }
    }

    [Test]
    public async Task ModifyRequestActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            ModifyRequest modifyRequest = new("cn=testmodify,ou=people," + Ldap.BaseDn, DirectoryAttributeOperation.Replace, "sn", "modded");
            try { conn.SendRequest(modifyRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();

            await Assert.That(activities).Contains(a => a.DisplayName.Contains("modify", StringComparison.InvariantCultureIgnoreCase));
        }
    }

    [Test]
    public async Task CompareRequestActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            CompareRequest compareRequest = new("cn=testcompare,ou=people," + Ldap.BaseDn, "sn", "compare");
            try { conn.SendRequest(compareRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();
            await Assert.That(activities).Contains(a => a.DisplayName.Contains("compare", StringComparison.InvariantCultureIgnoreCase));
        }
    }

    [Test]
    public async Task ExtendedRequestActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            ExtendedRequest extendedRequest = new("1.3.6.1.4.1.1466.20037"); // StartTLS OID
            try { conn.SendRequest(extendedRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();

            await Assert.That(activities).Contains(a => a.DisplayName.Contains("extended", StringComparison.InvariantCultureIgnoreCase) || a.DisplayName.Contains("1.3.6.1.4.1.1466.20037", StringComparison.InvariantCultureIgnoreCase));
        }
    }

    [Test]
    public async Task FailingLdapQuerySetsNonOkActivityStatus()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest invalidRequest = new(Ldap.BaseDn, ")objectClass=*()", SearchScope.Subtree, null);
            Assert.Throws<LdapException>(() => conn.SendRequest(invalidRequest));

            activityListener.Dispose();

            await Assert.That(activities).Contains(a => a.Status == ActivityStatusCode.Error);
        }
    }

    [Test]
    public async Task AbortSetsActivityStatusOk()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest searchRequest = new(Ldap.BaseDn, "(objectClass=*)", SearchScope.Subtree, null);
            IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, PartialResultProcessing.NoPartialResultSupport, callback: default!, state: default!);
            conn.Abort(asyncResult);

            activityListener.Dispose();

            await Assert.That(activities).Contains(a => a.DisplayName.Contains("abort", StringComparison.InvariantCultureIgnoreCase) || a.Status == ActivityStatusCode.Ok);
        }
    }

    [Test]
    public async Task PartialResultsActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest searchRequest = new(Ldap.BaseDn, "(objectClass=*)", SearchScope.Subtree, null);
            IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, PartialResultProcessing.ReturnPartialResults, callback: default!, state: default!);
            _ = conn.GetPartialResults(asyncResult);

            activityListener.Dispose();

            await Assert.That(activities).Contains(a => a.DisplayName.Contains("GetPartialResults", StringComparison.InvariantCultureIgnoreCase));
        }
    }

    [Test]
    public async Task BeginSendEndSendActivityIsGenerated()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest searchRequest = new(Ldap.BaseDn, "(objectClass=*)", SearchScope.Subtree, null);
            IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, PartialResultProcessing.NoPartialResultSupport, callback: default!, state: default!);
            _ = conn.EndSendRequest(asyncResult);

            activityListener.Dispose();

            await Assert.That(activities).Contains(a => a.DisplayName.Contains("ldap search", StringComparison.InvariantCultureIgnoreCase));
            await Assert.That(activities).ContainsOnly(a => a.Status == ActivityStatusCode.Ok);
        }
    }

    [Test]
    public async Task ActivitiesAreGeneratedCorrectly()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest searchRequest = new(Ldap.BaseDn, "(objectClass=*)", SearchScope.Subtree, null);
            _ = conn.SendRequest(searchRequest);

            activityListener.Dispose();

            await Assert.That(activities).Contains(a => a.DisplayName.Contains("ldap bind", StringComparison.InvariantCultureIgnoreCase));
            await Assert.That(activities).Contains(a => a.DisplayName.Contains("ldap search", StringComparison.InvariantCultureIgnoreCase));
            await Assert.That(activities).ContainsOnly(a => a.Status == ActivityStatusCode.Ok);
        }
    }

    [Test]
    public async Task MetricsAreCountedCorrectly()
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

        using TraceableLdapConnection conn = Ldap.CreateConnection();
        conn.Bind();
        SearchRequest searchRequest = new(Ldap.BaseDn, "(objectClass=*)", SearchScope.Subtree, null);
        _ = conn.SendRequest(searchRequest);

        meterListener.Dispose();

        long requestsTotal = metrics.Where(m => m.Name == "network.client.requests").Sum(m => Convert.ToInt64(m.Value, System.Globalization.CultureInfo.InvariantCulture));
        double durationTotal = metrics.Where(m => m.Name == "network.client.duration").Sum(m => Convert.ToDouble(m.Value, System.Globalization.CultureInfo.InvariantCulture));
        long entriesTotal = metrics.Where(m => m.Name == "ldap.search.entries_returned").Sum(m => Convert.ToInt64(m.Value, System.Globalization.CultureInfo.InvariantCulture));

        await Assert.That(requestsTotal).IsGreaterThanOrEqualTo(2);
        await Assert.That(durationTotal).IsGreaterThanOrEqualTo(2);
        await Assert.That(entriesTotal).IsGreaterThanOrEqualTo(0);
    }

    private record struct MetricRecord(string Name, object Value, IReadOnlyList<KeyValuePair<string, object?>> Tags);
}
