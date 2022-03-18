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
    [Description("project-name must be unique across your aws account")]
    [CommandArgument( 0,"<project-name>")]
    public string Project { get; set; }
    
    [CommandArgument(0,"<aws-secret-key>")]
    public string AwsSecretKey { get; set; }
    
    [CommandArgument(1,"<aws-access-key>")]
    public string AwsAcessKey { get; set; }
    

}


public sealed class InfrastructureInitCommandSettings : InfrastructureSettings
{
    [CommandOption("--create-dockerfile")]
    [DefaultValue(false)]
    public bool CreateDockerfile { get; set; } = false;

    [CommandOption("--project-type")] public string? ProjectType { get; set; } = null;
    
    public override ValidationResult Validate()
    {
        return string.IsNullOrEmpty(ProjectType) || ProjectTypeChoices.Exists(p => p == ProjectType)
            ? ValidationResult.Success()
            : ValidationResult.Error("--project-type choice is wrong");
    }
    public readonly List<string> ProjectTypeChoices=new () {
        "Fastapi", "AspNetCore", "NodeJs", 
    };
    public InfrastructureInitCommandSettings(bool createDockerfile,string projectType)
    {
        CreateDockerfile = createDockerfile;
        if (createDockerfile && string.IsNullOrEmpty(projectType))
        {
            ProjectType = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What's your Framework ?")
                    .AddChoices(ProjectTypeChoices));
        }
        else
            ProjectType = projectType;
    
    }
}

public sealed class InfrastructureDestroyCommandSettings : InfrastructureSettings
{
    
}




public sealed class InfrastructureDeployCommandSettings : InfrastructureSettings
{
    [CommandOption("--dockerfile-path")]
    [DefaultValue("./")]
    public string DockerfilePath { get; set; }
   

}