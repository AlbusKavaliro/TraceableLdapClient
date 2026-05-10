using System.DirectoryServices.Protocols;
using AlbusKavaliro.TraceableLdapClient;

namespace WorkerService;

#pragma warning disable CA1812 // False-positive: Class is instantiated by the generic host
internal sealed partial class Worker : BackgroundService
#pragma warning restore CA1812 // False-positive: Class is instantiated by the generic host
{
    private readonly ILogger<Worker> _logger;

    private readonly IServiceProvider _serviceProvider;

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                try
                {
                    using IServiceScope scope = _serviceProvider.CreateScope();
                    ILdapConnection ldapConnection = scope.ServiceProvider.GetRequiredService<ILdapConnection>();
                    Log.WorkerRunning(_logger, DateTimeOffset.Now);
                    DirectoryResponse r = ldapConnection.SendRequest(new SearchRequest(
                        "dc=example,dc=com",
                        "(objectClass=*)",
                        SearchScope.Subtree,
                        "cn", "sn", "mail"));
                    Log.LdapResponse(_logger, r);
                }
#pragma warning disable CA1031 // Catching general exception to log it
                catch (Exception ex)
#pragma warning restore CA1031 // Catching general exception to log it
                {
                    Log.LdapError(_logger, ex);
                }
            }

            await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
        }
    }

    internal static partial class Log
    {
        [LoggerMessage(
            EventId = 0,
            Level = LogLevel.Information,
            Message = "Worker running at: {Time}")]
        public static partial void WorkerRunning(
            ILogger logger, DateTimeOffset time);

        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "LDAP response: {Response}")]
        public static partial void LdapResponse(
            ILogger logger, [LogProperties] DirectoryResponse response);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Error,
            Message = "An error occurred while connecting to LDAP")]
        public static partial void LdapError(ILogger logger, Exception? exception = null);
    }
}
