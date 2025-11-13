# AlbusKavaliro.TraceableLdapClient

[![NuGet](https://img.shields.io/nuget/v/AlbusKavaliro.TraceableLdapClient.svg)](https://www.nuget.org/packages/AlbusKavaliro.TraceableLdapClient)

This is an Instrumentation Library for .NET, providing OpenTelemetry (OTEL) metrics and traces for LDAP client operations via the `TraceableLdapConnection` implementation. **To enable instrumentation, you must use `TraceableLdapConnection` instead of the standard `LdapConnection`.**

`TraceableLdapConnection` is a drop-in compatible wrapper for `LdapConnection`—it implements the same interface and can be used as a direct replacement in your code. This is necessary because the .NET Framework's `LdapConnection` cannot be instrumented directly.

## Features

- Automatic instrumentation of LDAP client operations
- Collection of OTEL-compliant metrics and traces for LDAP requests
- Enrichment and filtering APIs for customizing telemetry
- Support for context propagation
- Compatible with OpenTelemetry SDK and exporters

## Getting Started

### 1. Install Package

Add a reference to the [AlbusKavaliro.TraceableLdapClient](https://www.nuget.org/packages/AlbusKavaliro.TraceableLdapClient) package. Also add any other instrumentations & exporters you need.

```bash
dotnet add package AlbusKavaliro.TraceableLdapClient
```

### 2. Replace `LdapConnection` with `TraceableLdapConnection`

Change your code to use `TraceableLdapConnection` everywhere you would use `LdapConnection`. The API is compatible, so you can simply replace:

```csharp
// Old:
var ldap = new LdapConnection(server);

// New:
var ldap = new TraceableLdapConnection(server);
```

This enables automatic collection of metrics and traces for all LDAP operations performed through the wrapper.

### 3. Enable LDAP Instrumentation at Application Startup

You do not need to call any special registration method for this package. Instead, ensure that your OpenTelemetry configuration includes the following:

- Register the ActivitySource and Meter for `TraceableLdapClient.LdapConnection`.
- Use your application's standard OpenTelemetry setup (for example, via a helper like `ConfigureOpenTelemetry`).

#### Example (using a helper method similar to `ConfigureOpenTelemetry`)

```csharp
const string ldapConnectionInstrumentation = "TraceableLdapClient.LdapConnection";
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(ldapConnectionInstrumentation);
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource(builder.Environment.ApplicationName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource(ldapConnectionInstrumentation);
    });
```

This pattern matches the approach used in .NET Aspire and the OpenTelemetry.Instrumentation packages: you only need to register the ActivitySource and Meter names, and the library will emit telemetry automatically.

## OTEL Metrics

The following metrics are produced by this instrumentation (all emitted from the `TraceableLdapClient.LdapConnection` meter):

| Metric | Instrument | Unit | Description |
|--------|------------|------|-------------|
| `network.client.requests` | Counter | 1 | Total number of LDAP requests started (success + failure). |
| `network.client.errors` | Counter | 1 | Number of LDAP requests that ended in an error (exception thrown). |
| `network.client.duration` | Histogram | ms | End-to-end latency of LDAP requests (request to final response incl. server processing). |
| `ldap.search.entries_returned` | Counter | 1 | Total LDAP search result entries returned (increments per search response). |

### Common Metric Attributes
Depending on operation type and availability:

| Attribute | Example | Applies To | Notes |
|-----------|---------|-----------|-------|
| `ldap.operation` | `search` | all | Lowercase operation name derived from request type. |
| `ldap.response.result_code` | `0` | on response metrics | Numeric (enum int) result code for `ldap.search.entries_returned`. |
| `server.address` | `ldap.example.com` | all | First server specified in `LdapDirectoryIdentifier`. |
| `server.port` | `389` | all | Port number used. (Added only on spans by default; can be added via views if desired.) |

> NOTE: Additional high-cardinality attributes (like DNs or filters) are intentionally excluded from metrics to avoid cardinality explosion. Use span attributes or exemplars if you need drill-down.

## OTEL Traces

Each LDAP request produces a client span (ActivityKind.Client) whose name is:

```
ldap <operation> <target>
```

Example: `ldap search dc=example,dc=com ((objectClass=*))`

### Common Span Attributes

| Attribute | Example | Description |
|-----------|---------|-------------|
| `network.protocol.name` | `ldap` | Protocol fixed value. |
| `network.transport` | `tcp` | Underlying transport. |
| `server.address` | `ldap.example.com` | Target server. |
| `server.port` | `389` | Target port. |
| `ldap.operation` | `search` | Operation inferred from request type. |
| `ldap.request.type` | `SearchRequest` | Concrete request .NET type name. |
| `ldap.response.type` | `SearchResponse` | Concrete response .NET type name. |
| `ldap.response.result_code` | `success` / `invalidCredentials` | String form of `ResultCode` enum. |
| `ldap.response.error_message` | `...` | Server-provided error text (when present). |
| `ldap.response.matched_dn` | `cn=users,dc=example,dc=com` | Matched DN (when provided). |
| `ldap.response.referrals` | `ldap://other.example.com` | Comma‑separated referral URIs. |
| `timeout` | `00:00:30` | Per-request timeout (only when explicitly used overload). |
| `aborted` | `true` | Set when an async request was aborted. |
| `partialResultsCount` | `5` | Count of partial results for async search streaming. |
| `ldap.connection.type` | `ldaps` / `starttls` / `plain` | Inferred connection security type. |
| `ldap.connection.encrypted` | `true` | Whether encryption is enabled (implicit or StartTLS/SSL). |
| `tls.protocol.version` | `Tls12` | When available and encrypted. |

### Search Operation Attributes

| Attribute | Example | Description |
|-----------|---------|-------------|
| `ldap.search.base` | `dc=example,dc=com` | Base DN for the search. |
| `ldap.search.scope` | `subtree` | Scope (base / onelevel / subtree). |
| `ldap.search.filter` | `(objectClass=*)` | LDAP search filter. |
| `ldap.search.attributes` | `cn,mail` | Requested attribute list (comma separated). |
| `ldap.search.size_limit` | `500` | Server-side size limit requested. |
| `ldap.search.time_limit` | `30` | Time limit in seconds. |
| `ldap.search.types_only` | `false` | Whether only type information (no values) requested. |
| `ldap.search.deref_aliases` | `never` | Alias dereference behavior. |
| `ldap.search.entries_returned` | `42` | Returned entry count (response). |

### Bind Operation Attributes

| Attribute | Example | Description |
|-----------|---------|-------------|
| `ldap.bind.type` | `simple` / `anonymous` | Bind method classification. |
| `ldap.bind.dn` | `cn=admin,dc=example,dc=com` | DN used for authentication (if provided). |
| `ldap.auth.result` | `success` / `invalid_credentials` / `insufficient_access` | Outcome of the bind (success or reason). |

### Add Operation Attributes

| Attribute | Example | Description |
|-----------|---------|-------------|
| `ldap.dn` | `cn=user1,ou=people,dc=example,dc=com` | DN of the entry being added. |
| `ldap.add.attribute_count` | `7` | Number of attributes in the add request. |
| `ldap.add.attributes` | `cn,sn,mail` | Attribute names included (comma separated). |

### Modify Operation Attributes

| Attribute | Example | Description |
|-----------|---------|-------------|
| `ldap.dn` | `cn=user1,ou=people,dc=example,dc=com` | Target entry DN. |
| `ldap.modify.attribute_count` | `3` | Count of modifications. |
| `ldap.modify.attributes` | `mail,displayName` | Modified attribute names (comma separated). |
| `ldap.modify.operation` | `replace,add` | Distinct modification verb(s) present. |

### Compare Operation Attributes

| Attribute | Example | Description |
|-----------|---------|-------------|
| `ldap.dn` | `cn=user1,ou=people,dc=example,dc=com` | Entry DN compared. |
| `ldap.compare.attribute` | `uid` | Attribute under comparison. |
| `ldap.compare.result` | `true` / `false` / `error` | Outcome (true/false) or `error` if non-comparison failure. |

### Extended Operation Attributes

| Attribute | Example | Description |
|-----------|---------|-------------|
| `ldap.extended.operation_oid` | `1.3.6.1.4.1.1466.20037` | OID from request/response. |
| `ldap.extended.operation_name` | `start_tls` | Friendly mapped name (when recognized). |

Known OID mappings:

| OID | Name |
|-----|------|
| `1.3.6.1.4.1.1466.20037` | `start_tls` |
| `1.3.6.1.4.1.4203.1.11.1` | `modify_password` |
| `1.3.6.1.4.1.4203.1.11.3` | `who_am_i` |
| `1.3.6.1.1.8` | `cancel` |
| `1.2.840.113556.1.4.319` | `paged_results` |
| `1.2.840.113556.1.4.528` | `notification` |

### Error & Exception Attributes

| Attribute | Example | Description |
|-----------|---------|-------------|
| `exception.type` | `System.DirectoryServices.Protocols.LdapException` | CLR exception type. |
| `exception.message` | `Invalid Credentials` | Message provided by exception. |
| `exception.stacktrace` | (stack) | Captured stack trace. |

These are only set when an exception is thrown by underlying LDAP calls.

### Attribution & Cardinality Guidance

High-cardinality attributes (DNs, filters, attribute name lists) are emitted on spans but intentionally not attached to metrics. Use Views (metrics) or span processors to drop or hash sensitive/high-cardinality values if needed. See Masking Guidance below.

### Masking & Sensitive Data Guidance

Potentially sensitive attributes:

- `ldap.bind.dn`
- `ldap.dn`
- `ldap.search.filter`
- `ldap.search.attributes` / `ldap.add.attributes` / `ldap.modify.attributes`

Recommended approaches:

1. Use an Activity processor to redact or hash DN/filter values (e.g., replace RDN components with `***`).
2. Apply attribute filtering in your exporter (some backends allow drop/include rules).
3. Use sampling or tail-based sampling to limit exposure of full content for high-volume searches.

Example (conceptual) activity enrichment:

```csharp
services.AddOpenTelemetry().WithTracing(b =>
{
    b.AddSource("TraceableLdapClient.LdapConnection")
     .AddProcessor(new SimpleActivityExportProcessor(new FilteringExporter(activity =>
     {
         if (activity.Tags.FirstOrDefault(t => t.Key == "ldap.search.filter").Value is { } filter && filter.Length > 200)
         {
             activity.SetTag("ldap.search.filter", "(redacted)");
         }
     })));
});
```

(You would implement `FilteringExporter` / use an existing processor suitable for your telemetry stack.)

### Enrichment and Filtering

You can customize the telemetry by filtering or enriching activities and metrics in your OpenTelemetry pipeline. For example, you can use processors, views, or enrichers provided by the OpenTelemetry SDK to:

- Add custom tags (e.g., tenant ID, correlation ID) to LDAP activities.
- Filter out requests based on operation, DN, or server.
- Only record failed or slow requests.

See the OpenTelemetry documentation for details on customizing telemetry.

## Activity Duration and Metrics Calculation

`Activity.Duration` and `network.client.duration` measure the elapsed time from immediately before invoking the underlying `LdapConnection` API until the response (or exception) returns. Client-side queuing delays prior to invocation are not included. For asynchronous calls the duration is recorded when the operation completes (EndSendRequest / Abort).

## References

- [OpenTelemetry Project](https://opentelemetry.io/)
- [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet)
- [OpenTelemetry .NET Instrumentation Libraries](https://github.com/open-telemetry/opentelemetry-dotnet-contrib)

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
