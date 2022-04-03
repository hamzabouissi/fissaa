using Spectre.Console.Cli;

namespace fissaa.commands.domain;

public class AddDomainHttps:AsyncCommand<DomainSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DomainSettings settings)
    {
        var domainService = new AwsDomainService(settings.AwsSecretKey,settings.AwsAcessKey);
        await domainService.AddHttps(settings.DomainName);
        return 0;
    }
}