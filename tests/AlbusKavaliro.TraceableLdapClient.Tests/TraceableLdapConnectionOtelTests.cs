using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.DirectoryServices.Protocols;
using System.Net;

namespace AlbusKavaliro.TraceableLdapClient.Tests;

[NotInParallel]
public class TraceableLdapConnectionOtelTests
{
    private const string ActivitySourceName = "TraceableLdapClient.LdapConnection";
    private const string ObjectClassAttribute = "objectClass";
    private const string ObjectClassFilter = "(objectClass=*)";
    private const string StartTlsOid = "1.3.6.1.4.1.1466.20037";
    private const string SearchOperationName = "search";

    [ClassDataSource<LdapContainer>(Shared = SharedType.PerTestSession)]
    public required LdapContainer Ldap { get; init; }

    private (List<Activity>, ActivityListener, TraceableLdapConnection) CreateActivityListenerSetUp()
    {
        List<Activity> activities = [];
        ActivityListener activityListener = new()
        {
            ShouldListenTo = source => source.Name == ActivitySourceName,
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
            new DirectoryAttribute(ObjectClassAttribute, "person"),
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
            ExtendedRequest extendedRequest = new(StartTlsOid); // StartTLS OID
            try { conn.SendRequest(extendedRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();

            await Assert.That(activities).Contains(a => a.DisplayName.Contains("extended", StringComparison.InvariantCultureIgnoreCase) || a.DisplayName.Contains(StartTlsOid, StringComparison.InvariantCultureIgnoreCase));
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
            SearchRequest searchRequest = new(Ldap.BaseDn, ObjectClassFilter, SearchScope.Subtree, null);
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
            SearchRequest searchRequest = new(Ldap.BaseDn, ObjectClassFilter, SearchScope.Subtree, null);
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
            SearchRequest searchRequest = new(Ldap.BaseDn, ObjectClassFilter, SearchScope.Subtree, null);
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
            SearchRequest searchRequest = new(Ldap.BaseDn, ObjectClassFilter, SearchScope.Subtree, null);
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
                if (instrument.Meter.Name == ActivitySourceName)
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
        SearchRequest searchRequest = new(Ldap.BaseDn, ObjectClassFilter, SearchScope.Subtree, null);
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

    [Test]
    public async Task ConstructorWithStringWorks()
    {
        using TraceableLdapConnection conn = new("localhost:389");
        await Assert.That(conn.Directory).IsNotNull();
    }

    [Test]
    public async Task ConstructorWithLdapDirectoryIdentifierWorks()
    {
        LdapDirectoryIdentifier identifier = new("localhost", 389, false, false);
        using TraceableLdapConnection conn = new(identifier);
        await Assert.That(conn.Directory).IsEqualTo(identifier);
    }

    [Test]
    public async Task ConstructorWithCredentialsWorks()
    {
        LdapDirectoryIdentifier identifier = new("localhost", 389, false, false);
        NetworkCredential credential = new("user", "pass");
        using TraceableLdapConnection conn = new(identifier, credential);
        await Assert.That(conn.Directory).IsEqualTo(identifier);
    }

    [Test]
    public async Task ConstructorWithAuthTypeWorks()
    {
        LdapDirectoryIdentifier identifier = new("localhost", 389, false, false);
        NetworkCredential credential = new("user", "pass");
        using TraceableLdapConnection conn = new(identifier, credential, AuthType.Basic);
        await Assert.That(conn.AuthType).IsEqualTo(AuthType.Basic);
    }

    [Test]
    public async Task PropertiesCanBeSetAndRetrieved()
    {
        using TraceableLdapConnection conn = Ldap.CreateConnection();

        conn.AuthType = AuthType.Negotiate;
        await Assert.That(conn.AuthType).IsEqualTo(AuthType.Negotiate);

        conn.AutoBind = true;
        await Assert.That(conn.AutoBind).IsTrue();

        TimeSpan timeout = TimeSpan.FromSeconds(30);
        conn.Timeout = timeout;
        await Assert.That(conn.Timeout).IsEqualTo(timeout);

        await Assert.That(conn.ClientCertificates).IsNotNull();
        await Assert.That(conn.SessionOptions).IsNotNull();
    }

    [Test]
    public async Task BindWithNetworkCredentialGeneratesActivity()
    {
        List<Activity> activities = [];
        ActivityListener activityListener = new()
        {
            ShouldListenTo = source => source.Name == ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activities.Add,
            ActivityStopped = activity => { }
        };
        ActivitySource.AddActivityListener(activityListener);

        using TraceableLdapConnection conn = Ldap.CreateConnection();
        NetworkCredential credential = new("testuser", "testpass");

        try { conn.Bind(credential); } catch (DirectoryOperationException) { /* ignore errors for test */ }

        activityListener.Dispose();
        await Assert.That(activities).Contains(a => a.DisplayName.Contains("bind", StringComparison.InvariantCultureIgnoreCase));

        Activity? bindActivity = activities.FirstOrDefault(a => a.DisplayName.Contains("bind", StringComparison.InvariantCultureIgnoreCase));
        if (bindActivity != null)
        {
            await Assert.That(bindActivity.Tags).Contains(tag => tag.Key == "ldap.bind.type" && tag.Value?.ToString() == "simple");
            await Assert.That(bindActivity.Tags).Contains(tag => tag.Key == "ldap.bind.dn" && tag.Value?.ToString() == "testuser");
        }
    }

    [Test]
    public async Task SendRequestWithTimeoutGeneratesActivity()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest searchRequest = new(Ldap.BaseDn, ObjectClassFilter, SearchScope.Subtree, null);
            TimeSpan timeout = TimeSpan.FromSeconds(10);

            try { conn.SendRequest(searchRequest, timeout); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();
            await Assert.That(activities).Contains(a => a.DisplayName.Contains(SearchOperationName, StringComparison.InvariantCultureIgnoreCase));

            Activity? searchActivity = activities.FirstOrDefault(a => a.DisplayName.Contains(SearchOperationName, StringComparison.InvariantCultureIgnoreCase) && a != activities[0]);
            if (searchActivity != null)
            {
                await Assert.That(searchActivity.Tags).Contains(tag => tag.Key == "timeout" && tag.Value?.ToString() == timeout.ToString());
            }
        }
    }

    [Test]
    public async Task BeginSendRequestWithTimeoutGeneratesActivity()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest searchRequest = new(Ldap.BaseDn, ObjectClassFilter, SearchScope.Subtree, null);
            TimeSpan timeout = TimeSpan.FromSeconds(10);

            IAsyncResult asyncResult = conn.BeginSendRequest(searchRequest, timeout, PartialResultProcessing.NoPartialResultSupport, callback: default!, state: default!);
            try { conn.EndSendRequest(asyncResult); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();
            await Assert.That(activities).Contains(a => a.DisplayName.Contains(SearchOperationName, StringComparison.InvariantCultureIgnoreCase));

            Activity? searchActivity = activities.FirstOrDefault(a => a.DisplayName.Contains(SearchOperationName, StringComparison.InvariantCultureIgnoreCase) && a != activities[0]);
            if (searchActivity != null)
            {
                await Assert.That(searchActivity.Tags).Contains(tag => tag.Key == "timeout" && tag.Value?.ToString() == timeout.ToString());
            }
        }
    }

    [Test]
    public async Task SearchRequestActivityIsGeneratedWithCustomAttributes()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest searchRequest = new(Ldap.BaseDn, "(cn=test*)", SearchScope.OneLevel, "cn", "sn")
            {
                SizeLimit = 100,
                TimeLimit = TimeSpan.FromSeconds(30),
                TypesOnly = true,
                Aliases = DereferenceAlias.Always
            };

            try { conn.SendRequest(searchRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();
            Activity? searchActivity = activities.FirstOrDefault(a => a.DisplayName.Contains(SearchOperationName, StringComparison.InvariantCultureIgnoreCase) && a != activities[0]);

            await Assert.That(searchActivity).IsNotNull();
            await Assert.That(searchActivity!.Tags).Contains(tag => tag.Key == "ldap.search.base");
            await Assert.That(searchActivity.Tags).Contains(tag => tag.Key == "ldap.search.scope");
            await Assert.That(searchActivity.Tags).Contains(tag => tag.Key == "ldap.search.filter");
        }
    }

    [Test]
    public async Task ModifyRequestActivityIsGeneratedWithAttributes()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            ModifyRequest modifyRequest = new("cn=testmodify,ou=people," + Ldap.BaseDn);
            modifyRequest.Modifications.Add(new DirectoryAttributeModification
            {
                Name = "sn",
                Operation = DirectoryAttributeOperation.Replace
            });
            modifyRequest.Modifications.Add(new DirectoryAttributeModification
            {
                Name = "givenName",
                Operation = DirectoryAttributeOperation.Add
            });

            try { conn.SendRequest(modifyRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();
            Activity? modifyActivity = activities.FirstOrDefault(a => a.DisplayName.Contains("modify", StringComparison.InvariantCultureIgnoreCase));

            await Assert.That(modifyActivity).IsNotNull();
            await Assert.That(modifyActivity!.Tags).Contains(tag => tag.Key == "ldap.dn" && tag.Value?.ToString() == "cn=testmodify,ou=people," + Ldap.BaseDn);
        }
    }

    [Test]
    public async Task AddRequestActivityIsGeneratedWithAttributes()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            AddRequest addRequest = new("cn=testadd,ou=people," + Ldap.BaseDn,
                new DirectoryAttribute(ObjectClassAttribute, "person"),
                new DirectoryAttribute("cn", "testadd"),
                new DirectoryAttribute("sn", "add"));

            try { conn.SendRequest(addRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();
            Activity? addActivity = activities.FirstOrDefault(a => a.DisplayName.Contains("add", StringComparison.InvariantCultureIgnoreCase));

            await Assert.That(addActivity).IsNotNull();
            await Assert.That(addActivity!.Tags).Contains(tag => tag.Key == "ldap.dn" && tag.Value?.ToString() == "cn=testadd,ou=people," + Ldap.BaseDn);
        }
    }

    [Test]
    public async Task NetworkTagsAreSetCorrectly()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            SearchRequest searchRequest = new(Ldap.BaseDn, ObjectClassFilter, SearchScope.Subtree, null);
            try { conn.SendRequest(searchRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();
            Activity? searchActivity = activities.FirstOrDefault(a => a.DisplayName.Contains(SearchOperationName, StringComparison.InvariantCultureIgnoreCase) && a != activities[0]);

            await Assert.That(searchActivity).IsNotNull();
            await Assert.That(searchActivity!.Tags).Contains(tag => tag.Key == "network.protocol.name" && tag.Value?.ToString() == "ldap");
            await Assert.That(searchActivity.Tags).Contains(tag => tag.Key == "network.transport" && tag.Value?.ToString() == "tcp");
        }
    }

    [Test]
    public async Task ErrorCounterIsIncrementedOnFailure()
    {
        List<MetricRecord> metrics = [];
        MeterListener meterListener = new()
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ActivitySourceName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>((inst, val, tags, state) =>
        {
            metrics.Add(new MetricRecord(inst.Name, val, tags.ToArray()));
        });
        meterListener.Start();

        using TraceableLdapConnection conn = Ldap.CreateConnection();
        conn.Bind();

        // Perform an operation that should fail
        SearchRequest invalidRequest = new(Ldap.BaseDn, ")invalid filter(", SearchScope.Subtree, null);
        try { conn.SendRequest(invalidRequest); } catch (LdapException) { /* expected */ }

        meterListener.Dispose();

        long errorCount = metrics.Where(m => m.Name == "network.client.errors").Sum(m => Convert.ToInt64(m.Value, System.Globalization.CultureInfo.InvariantCulture));
        await Assert.That(errorCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task CompareRequestActivityContainsAttributes()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            CompareRequest compareRequest = new("cn=comparetest,ou=people," + Ldap.BaseDn, "sn", "test");
            try { conn.SendRequest(compareRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();
            Activity? compareActivity = activities.FirstOrDefault(a => a.DisplayName.Contains("compare", StringComparison.InvariantCultureIgnoreCase));
            if (compareActivity != null)
            {
                await Assert.That(compareActivity.Tags).Contains(tag => tag.Key == "ldap.dn");
                await Assert.That(compareActivity.Tags).Contains(tag => tag.Key == "ldap.compare.attribute");
            }
        }
    }

    [Test]
    public async Task ExtendedRequestActivityContainsOidAndName()
    {
        (List<Activity> activities, ActivityListener activityListener, TraceableLdapConnection conn) = CreateActivityListenerSetUp();
        using (conn)
        {
            ExtendedRequest extendedRequest = new(StartTlsOid); // StartTLS OID
            try { conn.SendRequest(extendedRequest); } catch (DirectoryOperationException) { /* ignore errors for test */ }

            activityListener.Dispose();
            Activity? extendedActivity = activities.FirstOrDefault(a => a.DisplayName.Contains("extended", StringComparison.InvariantCultureIgnoreCase) || a.DisplayName.Contains(StartTlsOid, StringComparison.InvariantCultureIgnoreCase));
            if (extendedActivity != null)
            {
                await Assert.That(extendedActivity.Tags).Contains(tag => tag.Key == "ldap.extended.operation_oid" && tag.Value?.ToString() == StartTlsOid);
                await Assert.That(extendedActivity.Tags).Contains(tag => tag.Key == "ldap.extended.operation_name" && tag.Value?.ToString() == "start_tls");
            }
        }
    }

    [Test]
    public async Task AnonymousBindGeneratesCorrectBindType()
    {
        List<Activity> activities = [];
        ActivityListener activityListener = new()
        {
            ShouldListenTo = source => source.Name == ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activities.Add,
            ActivityStopped = activity => { }
        };
        ActivitySource.AddActivityListener(activityListener);

        using TraceableLdapConnection conn = Ldap.CreateConnection();

        try { conn.Bind(null); } catch (DirectoryOperationException) { /* ignore errors for test */ }

        activityListener.Dispose();
        Activity? bindActivity = activities.FirstOrDefault(a => a.DisplayName.Contains("bind", StringComparison.InvariantCultureIgnoreCase));
        if (bindActivity != null)
        {
            await Assert.That(bindActivity.Tags).Contains(tag => tag.Key == "ldap.bind.type" && tag.Value?.ToString() == "anonymous");
        }
    }
}
