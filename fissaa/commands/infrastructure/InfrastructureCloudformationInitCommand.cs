using Spectre.Console.Cli;
using Spectre.Console;

namespace fissaa.commands.infrastructure;

public class InfrastructureCloudformationInitCommand:AsyncCommand<InfrastructureInitCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, InfrastructureInitCommandSettings settings)
    {
        var stack = new SimpleStack(settings.AwsSecretKey, settings.AwsAcessKey, settings.Project);
        await stack.CloudformationInit(settings.CreateDockerfile,settings.ProjectType);
        return 0;
    }
    public override ValidationResult Validate(CommandContext context, InfrastructureInitCommandSettings settings)
    {
        return settings.Validate();
    }
}