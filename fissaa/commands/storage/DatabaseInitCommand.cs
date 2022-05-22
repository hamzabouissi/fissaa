using fissaa.CloudProvidersServices;
using fissaa.commands.Templates;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.storage;

public class DatabaseInitCommand:AsyncCommand<DatabaseInitSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DatabaseInitSettings settings)
    {
        var awsNetworkService = new AwsNetworkService(settings.AwsSecretKey,settings.AwsAcessKey,settings.ProjectName);
        var awsdb = new AwsStorageStack(settings.AwsSecretKey,settings.AwsAcessKey);
        await AnsiConsole.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Dots9)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync("Creating database started", async ctx =>
            {
                ctx.Status("Create Vpc ");
                var vpcCreateResult =  await awsNetworkService.CreateVpc();
                if (vpcCreateResult.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{vpcCreateResult.Error}[/]");
                    return ;
                }
                AnsiConsole.MarkupLine(":house: Vpc Created :check_mark_button: ");
                ctx.Status("Create Network ");
                var networkServiceResult =  await awsNetworkService.Create();
                if (networkServiceResult.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{networkServiceResult.Error}[/]");
                    return ;
                }
                AnsiConsole.MarkupLine(":chains: Network Created :check_mark_button: ");
                ctx.Status("Create Database ");
                var databaseAuth = new DatabaseAuth
                {
                    dbName = settings.DbName,
                    username = "ghost",
                    password = "ghostPassword",
                    engine =  settings.DbType,
                    storage=settings.DBAllocatedStorage
                };
                var result = await awsdb.InitDb(databaseAuth);
                if (result.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{result.Error}[/]");
                    return ;
                }
                var databaseAuthInfo = await awsdb.DescribeDatabase(settings.DbName);
                foreach (var output in databaseAuthInfo)
                {
                    AnsiConsole.MarkupLine($"[grey]{output.OutputKey}: {output.OutputValue} [/]");
                }
            });
        return 0;
    }
    
    public override ValidationResult Validate(CommandContext context, DatabaseInitSettings settings)
    {
        return settings.Validate();
    }
}