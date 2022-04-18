using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class AppRollbackCommand:AsyncCommand<AppCreateCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppCreateCommandSettings settings)
    {
        var appService = new AwsEcsService(settings.AwsSecretKey,settings.AwsAcessKey,settings.DomainName);
        return 0;
    }
}