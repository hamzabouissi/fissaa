using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class AppDestroyCommand:AsyncCommand<AppDestroyCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppDestroyCommandSettings settings)
    {
        var networkService = new AwsNetworkService(settings.AwsSecretKey, settings.AwsAcessKey,settings.DomainName);
        var appService = new AwsEcsService(settings.AwsSecretKey,settings.AwsAcessKey,settings.DomainName);
        await AnsiConsole.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Aesthetic)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync("Deleting App Started", async ctx =>
            {
                await appService.Destroy();
                if (settings.All)
                {
                    ctx.Status("Deleting Network ");
                    await networkService.Destroy();
                }
                AnsiConsole.MarkupLine("[green]Done[/]");
            });
        
        return 0;
    }
}

