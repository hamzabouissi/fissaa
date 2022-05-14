using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class BudgetCreateCommand:AsyncCommand<BudgetCreateCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BudgetCreateCommandSettings settings)
    {
        var awsBudgetService = new AwsBudgetService(settings.AwsSecretKey, settings.AwsAcessKey);
        AnsiConsole.MarkupLine("You need to activate cost allocation before, wanna continue y/N");
        var answer = Console.ReadLine();
        if (answer?.ToLower() != "y")
            return 0;
        var result = await awsBudgetService.Create(settings.DomainName, settings.Email, settings.Budget, settings.limit);
        AnsiConsole.MarkupLine(result.IsSuccess ? "[green]Done[/]" : $"[red]{result.Error}[/]");
        return 0;
    }
}