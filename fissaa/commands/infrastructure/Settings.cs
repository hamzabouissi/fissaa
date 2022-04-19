using System.ComponentModel;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;


public class CommandSettings<T> : CommandSettings
{
    public override ValidationResult Validate()
    {
        var type = typeof(T);
        foreach (var p in type.GetProperties())
        {
            Console.WriteLine(p.Name);
        }

        return ValidationResult.Success();
    }
}


public class InfrastructureSettings : CommandSettings
{
    
    
    [CommandArgument(0,"<aws-secret-key>")]
    public string AwsSecretKey { get; set; }
    
    [CommandArgument(1,"<aws-access-key>")]
    public string AwsAcessKey { get; set; }
    
    [Description("domainName must the base , if you specifiy subdomain like sub.example.com, example.com will be considered as domain")]
    [CommandArgument(2,"<domain>")]
    public string DomainName { get; set; }

}


public sealed class NetworkCreateCommandSettings : InfrastructureSettings
{
}

public sealed class AppLogsommandSettings : InfrastructureSettings
{
    [Description("format M/dd/yyyy hh:mm")]
    [CommandOption("--start-date")]
    public string StartDate { get; set; } =(DateTime.Now-TimeSpan.FromMinutes(15)).ToString("M/dd/yyyy hh:mm");

    [CommandOption("--hour")] public int Hour { get; set; } = 1;


    public override ValidationResult Validate()
    {
        var parsed = DateTime.TryParseExact(StartDate, formats,null,
            System.Globalization.DateTimeStyles.AllowWhiteSpaces |
            System.Globalization.DateTimeStyles.AdjustToUniversal,out var parsedStartDate);
        if (!parsed)
            return ValidationResult.Error("--start-date isn't valid");
        if (Hour>5 || Hour <0)
            return ValidationResult.Error("--hour range 0,5");
        return ValidationResult.Success();
    }

    public readonly string[] formats = 
    {
        "M/dd/yyyy hh:mm"
    };
}

public sealed class AppDestroyCommandSettings : InfrastructureSettings
{

    [CommandOption("--all")] public bool All { get; set; } = false;

}

public sealed class AppCreateCommandSettings : InfrastructureSettings
{

    [CommandOption("--add-monitor")]
    [DefaultValue(false)]
    public bool AddMonitor { get; set; } = false;
    
    [CommandOption("--dockerfile-path")]
    [DefaultValue("./")]
    public string DockerfilePath { get; set; }
   
    [CommandOption("--create-dockerfile")]
    [DefaultValue(false)]
    public bool CreateDockerfile { get; set; } = false;
    [CommandOption("--project-type")] public string? ProjectType { get; set; } = null;

    
    public override ValidationResult Validate()
    {
        var validationResult = base.Validate();
        if (!validationResult.Successful)
            return validationResult;
        if (CreateDockerfile)
        {
            return ProjectTypeChoices.Exists(p => p == ProjectType)
                ? ValidationResult.Success()
                : ValidationResult.Error("--project-type choice is wrong");
        }
        return ValidationResult.Success();
       
    }
    
    private readonly List<string> ProjectTypeChoices = new()
    {
        "Fastapi", "AspNetCore", "NodeJs",
    };
    
    
}

