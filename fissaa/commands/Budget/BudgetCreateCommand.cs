using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class BudgetCreateCommand:AsyncCommand<BudgetCreateCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BudgetCreateCommandSettings settings)
    {
        var awsBudgetService = new AwsBudgetService(settings.AwsSecretKey, settings.AwsAcessKey);
        var result = await awsBudgetService.Create(settings.DomainName, settings.Email, settings.Budget, settings.limit);
        AnsiConsole.MarkupLine(result.IsSuccess ? "[green]Done[/]" : $"[red]{result.Error}[/]");
        return 0;
    }
}