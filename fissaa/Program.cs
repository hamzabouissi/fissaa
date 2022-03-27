// See https://aka.ms/new-console-template for more information
using fissaa;
using fissaa.commands.infrastructure;
using fissaa.commands.storage;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.AddBranch<InfrastructureSettings>("infrastructure", infr =>
    {
        
        infr.AddCommand<InfrastructureCloudformationInitCommand>("init");
        infr.AddCommand<InfrastructureCloudformationDestroyCommand>("destroy");
        infr.AddCommand<InfrastructureCloudformationDeployCommand>("deploy");

    });
    
    config.AddBranch("storage", infr =>
    {
        infr.AddBranch<StorageSettings>("db", storageInit =>
        {
            storageInit.AddCommand<DatabaseInitCommand>("init");
            storageInit.AddCommand<DatabaseInitCommand>("destroy");
        });
        infr.AddBranch<StorageSettings>("s3", storageInit =>
        {
            storageInit.AddCommand<DatabaseInitCommand>("init");
            storageInit.AddCommand<DatabaseInitCommand>("destroy");
        });

    });
});
return await app.RunAsync(args);


