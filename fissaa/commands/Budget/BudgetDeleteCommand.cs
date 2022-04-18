using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class BudgetDeleteCommand:AsyncCommand<BudgetDeleteCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BudgetDeleteCommandSettings settings)
    {
        var awsBudgetService = new AwsBudgetService(settings.AwsSecretKey, settings.AwsAcessKey);
        var result = await awsBudgetService.Delete(settings.DomainName);
        AnsiConsole.MarkupLine(result.IsSuccess ? "[green]Done[/]" : $"[red]{result.Error}[/]");
        return 0;
    }
}