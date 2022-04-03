
using Spectre.Console.Cli;

namespace fissaa.commands.domain;

public class DomainSettings:CommandSettings
{
    [CommandArgument(0,"<aws-secret-key>")]
    public string AwsSecretKey { get; set; }
    
    [CommandArgument(1,"<aws-access-key>")]
    public string AwsAcessKey { get; set; }
    [CommandArgument(2,"<domain-name>")]
    public string DomainName { get; set; }
}

public class CreateDomainSettings : DomainSettings
{
    
}