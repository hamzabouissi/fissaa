using fissaa.CloudProvidersServices;
using Spectre.Console;
using Spectre.Console.Cli;


namespace fissaa.commands.storage;

public class S3InitCommand:AsyncCommand<S3InitSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, S3InitSettings settings)
    {
        var awsdb = new AwsStorageStack(settings.AwsSecretKey,settings.AwsAcessKey);
        await AnsiConsole.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Dots9)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync("Creating S3 Bucket started", async ctx =>
            {
                var result = await awsdb.InitS3(settings.BucketName);
                if (result.IsFailure)
                {
                    AnsiConsole.MarkupLine($"[red]{result.Error}[/]");
                    return 0;
                }
                AnsiConsole.MarkupLine($"[green]S3 Bucket {settings.BucketName} Created :check_mark_button: [/]");
                
                return 0;
            });

        return 0;
    }
    
    public override ValidationResult Validate(CommandContext context, S3InitSettings settings)
    {
        return settings.Validate();
    }
  
}