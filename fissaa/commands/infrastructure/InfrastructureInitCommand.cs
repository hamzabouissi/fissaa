using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class InfrastructureInitCommand: AsyncCommand<InfrastructureInitCommandSettings>
{
  
    public override  async Task<int> ExecuteAsync(CommandContext context, InfrastructureInitCommandSettings settings)
    {
        var stack = new AWSoldStack(settings.AwsSecretKey,settings.AwsAcessKey,settings.Project);
        await stack.Init();
        // try
        // {
        //     await stack.Init();
        // }
        // catch (Exception exception)
        // {
        //     
        //     await stack.Destroy();
        //     throw;
        // }

        return 0;
    }
   
}