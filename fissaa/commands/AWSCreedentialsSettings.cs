using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands;

public class AwsCreedentialsSettings: CommandSettings
{
    public string AwsSecretKey { get; set; }
    
    public string AwsAcessKey { get; set; }


    public override ValidationResult Validate()
    {
        try
        {
            var creedentials = File.ReadAllText(".aws_creedentials.txt").Split("\n");
            var secretKey = creedentials[0].Split("=").Last();
            var accessKey = creedentials[1].Split("=").Last();
            if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
                return ValidationResult.Error("you forget to run [green]fissaa env init [/] to initialize aws creedentials");
            AwsAcessKey = accessKey;
            AwsSecretKey = secretKey;
            return ValidationResult.Success();
        }
        catch (FileNotFoundException e)
        {
            return ValidationResult.Error("you forget to run [green]fissaa env init[/] to initialize aws creedentials");
        }
        
     
    }
}


public class EnvInitAwsCreedentialsSettings: CommandSettings
{
    [CommandArgument(0,"<aws-secret-key>")]
    public string AwsSecretKey { get; set; }
    
    [CommandArgument(1,"<aws-access-key>")]
    public string AwsAcessKey { get; set; }
}