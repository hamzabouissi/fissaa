// See https://aka.ms/new-console-template for more information
using fissaa;
using fissaa.commands.domain;
using fissaa.commands.infrastructure;
using fissaa.commands.storage;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();

// Synchronous
// AnsiConsole.Status()
//     .Start("Thinking...", ctx => 
//     {
//         // Simulate some work
//         AnsiConsole.MarkupLine("Doing some work...");
//         Thread.Sleep(1000);
//         
//         // Update the status and spinner
//         ctx.Status("Thinking some more");
//         ctx.Spinner(Spinner.Known.Star);
//         ctx.SpinnerStyle(Style.Parse("green"));
//
//         // Simulate some work
//         AnsiConsole.MarkupLine("Doing some more work...");
//         Thread.Sleep(2000);
//     });




app.Configure(config =>
{
    config.AddBranch<InfrastructureSettings>("infrastructure", infr =>
    {
        
        infr.AddCommand<NetworkCreateCommand>("init");
        infr.AddCommand<AppDestroyCommand>("destroy");
        infr.AddCommand<AppCreateCommand>("deploy");
        infr.AddCommand<AppLogsCommand>("logs");
        infr.AddCommand<AppAddAlarmCommand>("add-alarm");
        infr.AddBranch("rollback" , command =>
        {
            command.AddCommand<AppRollbackListCommand>("list-image-tags");
            command.AddCommand<AppRollbackApplyCommand>("apply");
        });
        
    });
    config.AddBranch<BudgetSettings>("budget", budg =>
    {
        budg.AddCommand<BudgetCreateCommand>("create");
        budg.AddCommand<BudgetDeleteCommand>("delete");
    });
    config.AddBranch<DomainSettings>("domain", command =>
    {
        command.AddCommand<DomainCreateCommand>("create");
        command.AddCommand<AddDomainHttps>("add-https");
    });
    config.AddBranch<StorageSettings>("storage", infr =>
    {
        infr.AddBranch("db", storageInit =>
        {
            storageInit.AddCommand<DatabaseInitCommand>("init");
            storageInit.AddCommand<DatabaseInitCommand>("destroy");
        });
        infr.AddBranch("s3", storageInit =>
        {
            storageInit.AddCommand<DatabaseInitCommand>("init");
            storageInit.AddCommand<DatabaseInitCommand>("destroy");
        });

    });
});
return await app.RunAsync(args);
