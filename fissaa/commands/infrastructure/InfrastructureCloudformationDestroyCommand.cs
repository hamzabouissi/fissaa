using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class InfrastructureCloudformationDestroyCommand:AsyncCommand<InfrastructureDestroyCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, InfrastructureDestroyCommandSettings settings)
    {
        var stack = new SimpleStack(settings.AwsSecretKey,settings.AwsAcessKey,settings.Project);
        await stack.CloudformationDestroy();
        return 0;
    }
}

