using fissaa.CloudProvidersServices;
using fissaa.TemplatesServices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.Templates;

public class TemplateUseCommand:AsyncCommand<TemplateUseSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, TemplateUseSettings settings)
    {
        var domainService = new AwsDomainService(settings.AwsSecretKey,settings.AwsAcessKey);
        var networkService = new AwsNetworkService(settings.AwsSecretKey, settings.AwsAcessKey, settings.DomainName);
        var appService = new GhostTemplateService(settings.AwsSecretKey,settings.AwsAcessKey,settings.DomainName);
        var appStorage = new AwsStorageStack(settings.AwsSecretKey, settings.AwsAcessKey);
        if (settings.TemplateName == "ghost")
        {
            var escapedDomain = settings.DomainName.Replace(".", "-");
            var s3BucketName = $"{escapedDomain}-bucket".ToLower();
            var databaseAuth = new DatabaseAuth
            {
                dbName = "ghost",
                username = "ghost",
                password = "ghostPassword",
                engine = "mysql",
                storage=20
            };
            await AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(Spinner.Known.Dots9)
                .SpinnerStyle(Style.Parse("yellow bold"))
                .StartAsync("Creating ghost started", async ctx =>
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
                    AnsiConsole.MarkupLine("Network Created :check_mark_button: ");
                    
                    ctx.Status("Creating Database, it may take long time ,grab a drink :beer_mug: ");
                    var createDatabaseResult = await appStorage.InitDb(databaseAuth);
                    if (createDatabaseResult.IsFailure)
                    {
                        AnsiConsole.MarkupLine($"[red]{createDatabaseResult.Error}[/]");
                        return;
                    }
                    AnsiConsole.MarkupLine(" :classical_building: Database Created :check_mark_button: ");
                    
                    ctx.Status("Creating S3 Storage ");
                    var createS3EResult = await appStorage.InitS3(s3BucketName);
                    if (createS3EResult.IsFailure)
                    {
                        AnsiConsole.MarkupLine($"[red]{createS3EResult.Error}[/]");
                        return;
                    }
                    AnsiConsole.MarkupLine(":cloud:	 S3 Bucket Created :check_mark_button: ");
                    var mailAuth = new MailAuth
                    {
                        MailPassword=settings.MailPassword,
                        MailEmail=settings.MailEmail,
                        MailProvider = settings.MailProvider
                    };
                    ctx.Status("Deploying Ghost Template ");
                    var deployAppResult = await appService.CreateGhost(s3BucketName,databaseAuth,mailAuth);
                    if (deployAppResult.IsFailure)
                    {
                        AnsiConsole.MarkupLine($"[red]{deployAppResult.Error}[/]");
                        return;
                    }
                    AnsiConsole.MarkupLine(":ghost:	Ghost Deployed :check_mark_button: ");
                    AnsiConsole.MarkupLine($"Visit https://{settings.DomainName}");
                });
        }
       
        return 0;
    }
    public override ValidationResult Validate(CommandContext context, TemplateUseSettings settings)
    {
        return settings.Validate();
    }
}