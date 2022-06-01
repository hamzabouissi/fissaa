using System.ComponentModel;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;


public class InfrastructureSettings : AwsCreedentialsSettings
{
    
    
    [Description("it must be registred on route53 or any domain registrar")]
    [CommandArgument(2,"<domain>")]
    public string DomainName { get; set; }

}


public sealed class NetworkCreateCommandSettings : InfrastructureSettings
{
}

public sealed class AppLogsommandSettings : InfrastructureSettings
{
    [Description("format yyyy/MM/dd HH:mm:ss")]
    [CommandOption("--start-date")]
    public string StartDate { get; set; } = (DateTime.Now-TimeSpan.FromMinutes(60)).ToString("yyyy/MM/dd HH:mm:ss");
    
    [Description("hour must be between 0 and 5")]
    [CommandOption("--hour")] public int Hour { get; set; } = 2;


    public override ValidationResult Validate()
    {
        
        
        var parsed = DateTime.TryParseExact(StartDate, formats,null,
            System.Globalization.DateTimeStyles.AllowWhiteSpaces,out var parsedStartDate);
        if (!parsed)
            return ValidationResult.Error("--start-date isn't valid");
        if (Hour>5 || Hour <0)
            return ValidationResult.Error("--hour range 0,5");
        return base.Validate();
    }

    public readonly string[] formats = 
    {
        "yyyy/MM/dd HH:mm:ss"
    };
}

public sealed class AppDestroyCommandSettings : InfrastructureSettings
{
    [Description("this will delete both network, app infrastructure")]
    [CommandOption("--all")] 
    public bool All { get; set; } = false;

}

public sealed class AppCreateCommandSettings : InfrastructureSettings
{
    
    [Description("Environment are: Dev, Prod")]
    [CommandOption("--environment")]
    [DefaultValue("Dev")]
    public string Environment { get; set; } = "Dev"; 
    
    
    [Description("Models are: pay-as-you-go,static")]
    [CommandOption("--pricing-model")]
    [DefaultValue("pay-as-you-go")]
    public string PricingModel { get; set; } = "pay-as-you-go"; 
    
    [Description("Add AWS X-Ray to track your user requests")]
    [CommandOption("--add-monitor")]
    [DefaultValue(false)]
    public bool AddMonitor { get; set; } = false;


    [CommandOption("--dockerfile-path")]
    [DefaultValue("./")]
    public string DockerfilePath { get; set; } = "./";
   


    public override ValidationResult Validate()
    {
        var validationResult = base.Validate();
        if (!validationResult.Successful)
            return validationResult;
        if (Environment.ToLower() != "dev" && Environment.ToLower() != "prod")
           return ValidationResult.Error("Environment must be: Dev or Prod");
        return ValidationResult.Success();
       
    }
    
    private readonly List<string> ProjectTypeChoices = new()
    {
        "Fastapi", "AspNetCore", "NodeJs",
    };
    
    
}


public class AppRollbackApplySetting : InfrastructureSettings
{
    [Description("rollback to version prior to the current version, if no version nothing happen")]
    [CommandOption("--latest")] public bool? Latest { get; set; } = true;
    [Description("Image version for container registry")]
    [CommandOption("--image-version")]
    public string ImageVersion { get; set; } = string.Empty;
}

public class AppRollbackListSetting : InfrastructureSettings
{
}

public class AppAddAlarmSettings : InfrastructureSettings
{
    [Description("email to recieve notification")]
    [CommandOption("--email")]
    public string Email { get; set; }
}