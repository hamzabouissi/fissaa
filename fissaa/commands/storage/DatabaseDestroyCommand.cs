using Spectre.Console.Cli;

namespace fissaa.commands.storage;

public class DatabaseDestroyCommand:AsyncCommand<DbDestroySetting>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DbDestroySetting settings)
    {
        return 0;
    }
}