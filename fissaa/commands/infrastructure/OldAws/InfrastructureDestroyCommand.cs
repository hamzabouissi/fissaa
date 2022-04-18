using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class InfrastructureDestroyCommand:AsyncCommand<AppDestroyCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppDestroyCommandSettings settings)
    {
        var stack = new AWSoldStack(settings.AwsSecretKey,settings.AwsAcessKey,settings.DomainName);
        await stack.Destroy();
        return 0;
    }
}