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
            .Spinner(Spinner.Known.Aesthetic)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync("Creating App Started", async ctx =>
            {
                ctx.Status("Create Https Certificate");
                var addHttpsResult = await domainService.AddHttps(settings.DomainName);
                if (addHttpsResult.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{addHttpsResult.Error}[/]");
                    return;
                }
                   
                
                ctx.Status("Network Create");
                var networkResult = await networkService.Create();
                if (networkResult.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{networkResult.Error}[/]");
                    return;
                }               
                
                ctx.Status("Deploy App");
                var appResult = await appService.Create(settings.CreateDockerfile, settings.ProjectType, settings.DockerfilePath,settings.AddMonitor);
                if (appResult.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{appResult.Error}[/]");
                    return;
                }    
                AnsiConsole.MarkupLine("[green]Done[/]");
            });
        return 0;
    }
    public override ValidationResult Validate(CommandContext context, AppCreateCommandSettings settings)
    {
        return settings.Validate();
    }
}