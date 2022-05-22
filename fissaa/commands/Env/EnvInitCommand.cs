using fissaa.CloudProvidersServices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.Env;

public class EnvInitCommand:AsyncCommand<EnvInitAwsCreedentialsSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, EnvInitAwsCreedentialsSettings settings)
    {
        FileUtilFunctions.CreateAwsCreedentailsFile(settings.AwsSecretKey,settings.AwsAcessKey);
        AnsiConsole.MarkupLine("[green]Created .aws_creedentials file[/]");
        AnsiConsole.MarkupLine("[grey]Note:[/] [red] Add .aws_creedentials to .gitigonre[/]");
        return 0;
    }
}