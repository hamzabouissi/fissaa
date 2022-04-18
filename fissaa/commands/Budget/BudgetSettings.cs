using System.ComponentModel;
using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class BudgetSettings : CommandSettings
{
    
    
    [CommandArgument(0,"<aws-secret-key>")]
    public string AwsSecretKey { get; set; }
    
    [CommandArgument(1,"<aws-access-key>")]
    public string AwsAcessKey { get; set; }
    
    [Description("domainName must the base , if you specifiy subdomain like sub.example.com, example.com will be considered as domain")]
    [CommandArgument(2,"<domain>")]
    public string DomainName { get; set; }

}

public class BudgetCreateCommandSettings : BudgetSettings
{
    [CommandArgument(3,"<budget-amount>")]
    public decimal Budget { get; set; }
    
    [CommandArgument(4,"<budget-amount-limit>")]
    public decimal limit { get; set; }
    
    [CommandArgument(5,"<email>")]
    public string Email { get; set; }
}

public class BudgetDeleteCommandSettings : BudgetSettings
{
 
}