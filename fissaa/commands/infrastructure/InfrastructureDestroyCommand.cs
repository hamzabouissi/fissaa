using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class InfrastructureDestroyCommand:AsyncCommand<InfrastructureDestroyCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, InfrastructureDestroyCommandSettings settings)
    {
        var stack = new AWSoldStack(settings.AwsSecretKey,settings.AwsAcessKey,settings.Project);
        await stack.Destroy();
        return 0;
    }
}