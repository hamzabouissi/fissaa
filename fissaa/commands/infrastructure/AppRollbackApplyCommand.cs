using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class AppRollbackApplyCommand:AsyncCommand<AppRollbackApplySetting>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppRollbackApplySetting settings)
    {
        var appService = new AwsEcsService(settings.AwsSecretKey,settings.AwsAcessKey,settings.DomainName);
        await appService.RollBackApply(settings.Latest,settings.ImageVersion);
        return 0;
    }
}

