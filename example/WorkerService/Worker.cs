using System.DirectoryServices.Protocols;
using System.Net;
using TraceableLdapClient;

namespace WorkerService;

internal class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
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
                    using TraceableLdapConnection c = new(new LdapDirectoryIdentifier("ldap", 389));
                    c.Credential = new NetworkCredential("admin", "admin");
                    DirectoryResponse r = c.SendRequest(new SearchRequest(
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
