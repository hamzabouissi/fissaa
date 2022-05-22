
using Spectre.Console.Cli;

namespace fissaa.commands.domain;

public class DomainSettings:AwsCreedentialsSettings
{
    [CommandArgument(2,"<domain-name>")]
    public string DomainName { get; set; }
}

