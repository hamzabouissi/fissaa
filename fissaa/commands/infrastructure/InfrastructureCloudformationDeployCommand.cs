using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class InfrastructureCloudformationDeployCommand:AsyncCommand<InfrastructureDeployCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, InfrastructureDeployCommandSettings settings)
    {
        var stack = new SimpleStack(settings.AwsSecretKey,settings.AwsAcessKey,settings.Project);
        await stack.CloudformationDeploy(settings.DockerfilePath);
        return 0;
    }
}