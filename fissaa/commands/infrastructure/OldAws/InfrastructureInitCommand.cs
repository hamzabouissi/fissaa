using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class InfrastructureInitCommand: AsyncCommand<NetworkCreateCommandSettings>
{
  
    public override  async Task<int> ExecuteAsync(CommandContext context, NetworkCreateCommandSettings settings)
    {
        var stack = new AWSoldStack(settings.AwsSecretKey,settings.AwsAcessKey,string.Empty);
        await stack.Init();

        return 0;
    }
   
}