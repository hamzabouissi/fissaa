using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.storage;


public class StorageSettings:AwsCreedentialsSettings
{
   
    
    
    [Description("project-name must be unique across your aws account")]
    [CommandArgument( 2,"<project-name>")]
    public string ProjectName { get; set; }
    
   
}


public class DatabaseInitSettings:StorageSettings
{
    
    
    [Description("different db type allowed values are:  mysql, mariadb, postgres")]
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


public class S3InitSettings:StorageSettings
{
    
    
    [Description("s3 bucket name")]
    [CommandArgument( 0,"<bucket-name>")]
    public string BucketName { get; set; }
    
    
   


}