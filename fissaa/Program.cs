// See https://aka.ms/new-console-template for more information
using fissaa;
using fissaa.commands.infrastructure;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.AddBranch<InfrastructureSettings>("infrastructure", infr =>
    {
        // infr.AddCommand<InfrastructureInitCommand>("init");
        // infr.AddCommand<InfrastructureDestroyCommand>("destroy");
        // infr.AddCommand<InfrastructureDeployCommand>("deploy");
        
        infr.AddCommand<InfrastructureCloudformationInitCommand>("init");
        infr.AddCommand<InfrastructureCloudformationDestroyCommand>("destroy");
        infr.AddCommand<InfrastructureCloudformationDeployCommand>("deploy");

    });
});
return await app.RunAsync(args);


