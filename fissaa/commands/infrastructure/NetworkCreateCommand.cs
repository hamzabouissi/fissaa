using Spectre.Console.Cli;
using Spectre.Console;

namespace fissaa.commands.infrastructure;

public class NetworkCreateCommand:AsyncCommand<NetworkCreateCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NetworkCreateCommandSettings settings)
    {
        var stack = new AwsNetworkService(settings.AwsSecretKey, settings.AwsAcessKey,settings.DomainName);
        await stack.Create();
        return 0;
    }
    public override ValidationResult Validate(CommandContext context, NetworkCreateCommandSettings settings)
    {
        return settings.Validate();
    }
}