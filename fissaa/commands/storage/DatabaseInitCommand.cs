using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.storage;

public class DatabaseInitCommand:AsyncCommand<DatabaseInitSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DatabaseInitSettings settings)
    {
        var awsdb = new AwsDBStack(settings.AwsSecretKey,settings.AwsAcessKey,settings.Project);
        await awsdb.init("database",settings.DbName,settings.DbType ,settings.DBUsername,settings.DBPassword);
        return 0;
    }
    
    public override ValidationResult Validate(CommandContext context, DatabaseInitSettings settings)
    {
        return settings.Validate();
    }
}