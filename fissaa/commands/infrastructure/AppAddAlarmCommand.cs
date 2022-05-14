using CSharpFunctionalExtensions;
using fissaa.CloudProvidersServices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class AppAddAlarmCommand:AsyncCommand<AppAddAlarmSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppAddAlarmSettings settings)
    {
        var appService = new AwsEcsService(settings.AwsSecretKey,settings.AwsAcessKey,settings.DomainName);

        await AnsiConsole.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Dots9)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync("Creating Alarm Started", async ctx =>
            {
                var result = await appService.CreateAlarm(settings.Email);
                if (result.IsFailure)
                    AnsiConsole.MarkupLine($"[red]{result.Error}[/]");
                AnsiConsole.MarkupLine($"[green]done[/]");
            });
        return 0;
    }
}