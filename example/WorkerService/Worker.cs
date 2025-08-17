using System.DirectoryServices.Protocols;
using TraceableLdapClient;

namespace WorkerService;

internal class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    private readonly ILdapConnection _ldapConnection;

    public Worker(ILogger<Worker> logger, ILdapConnection ldapConnection)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(ldapConnection);
        _logger = logger;
        _ldapConnection = ldapConnection;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                try
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    DirectoryResponse r = _ldapConnection.SendRequest(new SearchRequest(
                        "dc=example,dc=com",
                        "(objectClass=*)",
                        SearchScope.Subtree,
                        "cn", "sn", "mail"));
                    _logger.LogInformation("LDAP response: {response}", r);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while connecting to LDAP");
                }
            }

            await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
        }
    }
}
