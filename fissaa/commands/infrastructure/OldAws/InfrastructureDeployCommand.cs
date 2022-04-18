using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class InfrastructureDeployCommand:AsyncCommand<AppCreateCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppCreateCommandSettings settings)
    {
        var stack = new AWSoldStack(settings.AwsSecretKey,settings.AwsAcessKey,settings.DomainName);
        await stack.Deploy(settings.DockerfilePath);
        return 0;
    }
}