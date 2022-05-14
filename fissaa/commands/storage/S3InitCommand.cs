using Amazon.CDK;
using fissaa.CloudProvidersServices;
using Spectre.Console;
using Spectre.Console.Cli;


namespace fissaa.commands.storage;

public class S3InitCommand:AsyncCommand<S3InitSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, S3InitSettings settings)
    {
        var awsdb = new AwsStorageStack(settings.AwsSecretKey,settings.AwsAcessKey);
        var result = await awsdb.InitS3(settings.BucketName);
        if (result.IsFailure)
        {
            AnsiConsole.MarkupLine($"[red]{result.Error}[/]");
            return 0;
        }
        AnsiConsole.MarkupLine("[green]Done[/]");

        return 0;
    }
  
}