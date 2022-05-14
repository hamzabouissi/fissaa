using fissaa.CloudProvidersServices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class AppRollbackApplyCommand:AsyncCommand<AppRollbackApplySetting>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppRollbackApplySetting settings)
    {
        var appService = new AwsEcsService(settings.AwsSecretKey,settings.AwsAcessKey,settings.DomainName);
        await AnsiConsole.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Dots9)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync("Rolling back  started", async ctx =>
            {
                var result = await appService.RollBackApply(settings.Latest, settings.ImageVersion);
                if (result.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{result.Error}[/]");
                    return;
                }
                AnsiConsole.MarkupLine("App Rolled Back :check_mark_button: ");
            });
        return 0;
    }
}

