using System.DirectoryServices.Protocols;
using AlbusKavaliro.TraceableLdapClient;

namespace WorkerService;

internal class Worker : BackgroundService
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
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    DirectoryResponse r = ldapConnection.SendRequest(new SearchRequest(
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
