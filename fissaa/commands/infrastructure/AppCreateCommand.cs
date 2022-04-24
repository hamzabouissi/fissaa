using Amazon.CloudFormation;
using CSharpFunctionalExtensions;
using fissaa.Decorator;
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
            .StartAsync("Creating App Started", async ctx =>
            {
                ctx.Status("Create TLS Certificate");
                var addHttpsResult = await domainService.AddHttps(settings.DomainName);
                if (addHttpsResult.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{addHttpsResult.Error}[/]");
                    return;
                }
                AnsiConsole.MarkupLine("TLS Certificate Added :check_mark_button: ");
                
                ctx.Status("Create Network infrastructure");
                var networkResult = await networkService.Create();
                if (networkResult.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{networkResult.Error}[/]");
                    return;
                }        
                Thread.Sleep(8000);
                AnsiConsole.MarkupLine("Network Created :check_mark_button: ");
                ctx.Spinner(Spinner.Known.Monkey);
                ctx.Status("Deploy App");
                Thread.Sleep(15000);
                var appResult = await appService.Create(settings.CreateDockerfile, settings.ProjectType, settings.DockerfilePath,settings.AddMonitor);
                if (appResult.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{appResult.Error}[/]");
                    return;
                }    
                AnsiConsole.MarkupLine("App Deployed :check_mark_button: ");
            });
        return 0;
    }
    public override ValidationResult Validate(CommandContext context, AppCreateCommandSettings settings)
    {
        return settings.Validate();
    }
}