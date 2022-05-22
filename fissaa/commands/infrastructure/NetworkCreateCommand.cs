using fissaa.CloudProvidersServices;
using Spectre.Console.Cli;
using Spectre.Console;

namespace fissaa.commands.infrastructure;

public class NetworkCreateCommand:AsyncCommand<NetworkCreateCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NetworkCreateCommandSettings settings)
    {
        var domainService = new AwsDomainService(settings.AwsSecretKey,settings.AwsAcessKey);
        var networkService = new AwsNetworkService(settings.AwsSecretKey, settings.AwsAcessKey,settings.DomainName);
        await AnsiConsole.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Dots9)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync("", async ctx =>
            {
                ctx.Status("Create Vpc ");
                var vpcResult = await networkService.CreateVpc();
                if (vpcResult.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{vpcResult.Error}[/]");
                    return;
                }

                AnsiConsole.MarkupLine(":house: Vpc Created :check_mark_button: ");

                ctx.Status("Create TLS Certificate");
                var addHttpsResult = await domainService.AddHttps(settings.DomainName);
                if (addHttpsResult.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{addHttpsResult.Error}[/]");
                    return;
                }

                AnsiConsole.MarkupLine(":locked: TLS Certificate Added :check_mark_button: ");

                ctx.Status("Create Network infrastructure");
                var networkResult = await networkService.Create();
                if (networkResult.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{networkResult.Error}[/]");
                    return;
                }
                AnsiConsole.MarkupLine(":chains: Network Created :check_mark_button: ");
            });
            return 0;
    }
    public override ValidationResult Validate(CommandContext context, NetworkCreateCommandSettings settings)
    {
        return settings.Validate();
    }
}