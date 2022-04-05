using Spectre.Console.Cli;
using Spectre.Console;

namespace fissaa.commands.infrastructure;

public class InfrastructureCloudformationDeployCommand:AsyncCommand<InfrastructureDeployCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, InfrastructureDeployCommandSettings settings)
    {
        var stack = new AwsNetworkStack(settings.AwsSecretKey,settings.AwsAcessKey,settings.DomainName);
        await stack.CloudformationDeploy(settings.CreateDockerfile,settings.ProjectType, settings.DockerfilePath);
        return 0;
    }
    public override ValidationResult Validate(CommandContext context, InfrastructureDeployCommandSettings settings)
    {
        return settings.Validate();
    }
}