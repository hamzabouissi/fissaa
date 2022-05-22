using System.ComponentModel;
using Spectre.Console.Cli;

namespace fissaa.commands.Templates;

public class TemplateSettings:AwsCreedentialsSettings
{
    [CommandArgument(2,"<domain-name>")]
    public string DomainName { get; set; }
}

public class TemplateUseSettings : TemplateSettings
{

    [Description("template name, available options are: ghost")]
    [CommandArgument(3, "<template-name>")]
    public string TemplateName { get; set; }
    
    
    [Description("mailing service, available options are: sendinblue")]
    [CommandArgument(4,"<mail-provider>")]
    public string MailProvider { get;set; }
    
    
    [CommandArgument(5,"<mail-email>")]
    public string MailEmail { get;set;  }
    
    
    [CommandArgument(6,"<mail-password>")]
    public string MailPassword { get; set; }
}