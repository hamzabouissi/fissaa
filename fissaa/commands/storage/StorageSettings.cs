using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.storage;


public class StorageSettings:CommandSettings
{
   
    
    [CommandArgument(0,"<aws-secret-key>")]
    public string AwsSecretKey { get; set; }
    
    [CommandArgument(1,"<aws-access-key>")]
    public string AwsAcessKey { get; set; }
    
    [Description("project-name must be unique across your aws account")]
    [CommandArgument( 0,"<project-name>")]
    public string Project { get; set; }
    
   
}


public class DatabaseInitSettings:StorageSettings
{
    
    
    [Description("your database size on GB minumum: 20GB")]
    [CommandArgument( 0,"<db-type>")]
    public string DbType { get; set; }
    
    
    [CommandArgument( 1,"<db-username>")]
    public string DBUsername { get; set; }
    
    [CommandArgument(2, "<db-name>")]
    public string DbName { get; set; }
    
    [CommandArgument( 3,"<db-password>")]
    public string DBPassword { get; set; }
    
    [Description("your database size on GB minumum: 20GB")]
    [CommandOption( "--db-storage")]
    public float DBAllocatedStorage { get; set; } = 20;


}

public class DbDestroySetting : StorageSettings
{
    
}