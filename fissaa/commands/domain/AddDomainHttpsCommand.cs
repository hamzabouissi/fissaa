using fissaa.CloudProvidersServices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.domain;

public class AddDomainHttps:AsyncCommand<DomainSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DomainSettings settings)
    {
        var domainService = new AwsDomainService(settings.AwsSecretKey,settings.AwsAcessKey);
        await AnsiConsole.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Dots9)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync(":locked: Creating TLS Certificate started",
        async ctx =>
            {
                var result = await domainService.AddHttps(settings.DomainName);
                if (result.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{result.Error}[/]");
                    return;
                }
                AnsiConsole.MarkupLine(":locked: TLS Certificate Added :check_mark_button: ");
                
            });
        return 0;
    }
}