using System.ComponentModel;
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
    [CommandArgument(1,"<domain>")]
    public string DomainName { get; set; }

}


public sealed class InfrastructureInitCommandSettings : InfrastructureSettings
{
   
}

public sealed class InfrastructureDestroyCommandSettings : InfrastructureSettings
{
    
    [CommandOption("--only-app")]
    public bool? only_app { get; set; }

}




public sealed class InfrastructureDeployCommandSettings : InfrastructureSettings
{
    
    
    [CommandOption("--dockerfile-path")]
    [DefaultValue("./")]
    public string DockerfilePath { get; set; }
   
    [CommandOption("--create-dockerfile")]
    [DefaultValue(false)]
    public bool CreateDockerfile { get; set; } = false;
    [CommandOption("--project-type")] public string? ProjectType { get; set; } = null;

    
    public override ValidationResult Validate()
    {
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