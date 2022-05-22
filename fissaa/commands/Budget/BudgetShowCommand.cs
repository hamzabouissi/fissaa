using fissaa.commands.infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.Budget;

public class BudgetShowCommand:AsyncCommand<BudgetShowCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BudgetShowCommandSettings settings)
    {
        
        var awsBudgetService = new AwsBudgetService(settings.AwsSecretKey, settings.AwsAcessKey);
        var listCost = await awsBudgetService.ListCost(settings.DomainName);
        var baseDomain = string.Join(".",settings.DomainName.Split(".")[^2..]);
        AnsiConsole.MarkupLine($"[red]This show prices under base domain {baseDomain} [/] :money_bag:");
        AnsiConsole.MarkupLine($"[grey]Total[/]: [green]{listCost.Values.Sum()}[/]");
        foreach (var (date,price) in listCost)
        {
            AnsiConsole.MarkupLine($"[grey]{date} [/] => [red]{price}[/]");
        }
        return 0;
    }
}