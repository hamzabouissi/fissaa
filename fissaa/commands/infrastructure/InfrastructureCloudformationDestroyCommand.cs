using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class InfrastructureCloudformationDestroyCommand:AsyncCommand<InfrastructureDestroyCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, InfrastructureDestroyCommandSettings settings)
    {
        var stack = new AwsNetworkStack(settings.AwsSecretKey,settings.AwsAcessKey,settings.Project);
        await stack.CloudformationDestroy(settings.only_app is not null);
        return 0;
    }
}

