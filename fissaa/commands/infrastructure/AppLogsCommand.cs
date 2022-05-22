using fissaa.CloudProvidersServices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class AppLogsCommand:AsyncCommand<AppLogsommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppLogsommandSettings settings)
    {
        var appService = new AwsEcsService(settings.AwsSecretKey,settings.AwsAcessKey,settings.DomainName);
        await appService.ListLogs(settings.StartDate,settings.Hour);
        return 0;
    }
    
    public override ValidationResult Validate(CommandContext context, AppLogsommandSettings settings)
    {
        return settings.Validate();
    }

    
    
    
}