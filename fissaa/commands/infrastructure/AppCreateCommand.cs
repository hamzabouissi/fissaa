using CSharpFunctionalExtensions;
using fissaa.CloudProvidersServices;
using Spectre.Console.Cli;
using Spectre.Console;

namespace fissaa.commands.infrastructure;

public class AppCreateCommand:AsyncCommand<AppCreateCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppCreateCommandSettings settings)
    {
        var domainService = new AwsDomainService(settings.AwsSecretKey,settings.AwsAcessKey);
        var networkService = new AwsNetworkService(settings.AwsSecretKey, settings.AwsAcessKey,settings.DomainName);
        var appService = new AwsEcsService(settings.AwsSecretKey,settings.AwsAcessKey,settings.DomainName);
        if (settings.AddMonitor)
        {
            Console.WriteLine("Please visit https://docs.aws.amazon.com/xray/latest/devguide/xray-ruby.html first,to check your integration with monitoring sdk");
            Console.WriteLine("Wanna Continue y/N");
            var decision = Console.ReadLine();
            if (decision is null || decision.ToLower() != "y")
                return 0;
            Console.WriteLine("Hint: We gonna add monitoring");    
        }
        
        await AnsiConsole.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Dots9)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync("Creating App started", async ctx =>
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
                
                ctx.Status("Create Load Balancer ");
                Result albCreateResult = await networkService.CreateAlb();
                if (albCreateResult.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{albCreateResult.Error}[/]");
                    return;
                }
                AnsiConsole.MarkupLine(":bridge_at_night: Load Balancer Created :check_mark_button: ");
                ctx.Spinner(Spinner.Known.Monkey);
                ctx.Status("Deploy App");
                var appEnvironment = AppEnvironment.Dev;
                if (settings.Environment == "Prod")
                     appEnvironment = AppEnvironment.Prod;
                
                var appResult = await appService.Create(ContainerTemplate.App,settings.DockerfilePath,settings.AddMonitor,String.Empty,appEnvironment);
                if (appResult.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{appResult.Error}[/]");
                    return;
                }    
                AnsiConsole.MarkupLine("App Deployed :check_mark_button: ");
                AnsiConsole.MarkupLine($"[grey]Visit https://{settings.DomainName}[/]");
            });
        return 0;
    }
    public override ValidationResult Validate(CommandContext context, AppCreateCommandSettings settings)
    {
        return settings.Validate();
    }
}