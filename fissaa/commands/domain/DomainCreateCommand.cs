using fissaa.CloudProvidersServices;
using Spectre.Console.Cli;

namespace fissaa.commands.domain;

public class DomainCreateCommand:AsyncCommand<DomainSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DomainSettings settings)
    {
        var domainService = new AwsDomainService(settings.AwsSecretKey,settings.AwsAcessKey);
        await domainService.CreateDomain(settings.DomainName);
        return 0;
    }
}