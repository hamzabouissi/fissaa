using fissaa.CloudProvidersServices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.domain;

public class DomainCreateCommand:AsyncCommand<DomainSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DomainSettings settings)
    {
        var domainService = new AwsDomainService(settings.AwsSecretKey,settings.AwsAcessKey);
        var nameservers = await domainService.CreateDomain(settings.DomainName);
        foreach (var nameserver in nameservers)
        {
            AnsiConsole.MarkupLine($"[grey]{nameserver}[/]");
        }
        AnsiConsole.MarkupLine("[red] You need to add those nameservers to your dns provider[/]");
        AnsiConsole.MarkupLine("[red] Wait Until dns propagation work, means your dns provider recognize those nameservers, it may take 24h Max[/]");
        return 0;
    }
}