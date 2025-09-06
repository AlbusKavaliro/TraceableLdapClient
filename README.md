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

The following metrics are produced by this instrumentation:

- `network.client.requests` (Counter): Total number of LDAP requests made
- `network.client.errors` (Counter): Number of failed LDAP requests
- `network.client.duration` (Histogram, milliseconds): Duration of LDAP requests
- `ldap.search.entries_returned` (Counter): Number of entries returned by LDAP search operations

Each metric includes relevant attributes from the LDAP operation context, such as server address, operation type, and result codes.

## OTEL Traces

Each LDAP request is traced as an activity/span with the following base attributes:

- `network.protocol.name` — Always set to "ldap"
- `network.transport` — Always set to "tcp"
- `server.address` — The LDAP server address
- `server.port` — The LDAP server port
- `ldap.operation` — The LDAP operation type
- `requestType` — The specific type of LDAP request
- `distinguishedName` — The DN involved in the request (where applicable)
- `ldap.response.type` — Type of response received
- `ldap.response.result_code` — Result code from the server
- `ldap.response.error_message` — Error message if any

For search operations, additional attributes are included:
- `ldap.search.base` — Search base DN
- `ldap.search.scope` — Search scope
- `ldap.search.filter` — Search filter
- `ldap.search.attributes` — Requested attributes
- `ldap.search.size_limit` — Size limit
- `ldap.search.time_limit` — Time limit

For errors, exception details are added:
- `exception.type` — Type of the exception
- `exception.message` — Exception message
- `exception.stacktrace` — Stack trace

### Enrichment and Filtering

You can customize the telemetry by filtering or enriching activities and metrics in your OpenTelemetry pipeline. For example, you can use processors, views, or enrichers provided by the OpenTelemetry SDK to:

- Add custom tags (e.g., tenant ID, correlation ID) to LDAP activities.
- Filter out requests based on operation, DN, or server.
- Only record failed or slow requests.

See the OpenTelemetry documentation for details on customizing telemetry.

## Activity Duration and Metrics Calculation

- `Activity.Duration` and `network.client.duration` represent the time taken to complete the LDAP request, up to receiving the server response.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)
- [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet)
- [OpenTelemetry .NET Instrumentation Libraries](https://github.com/open-telemetry/opentelemetry-dotnet-contrib)

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
