// See https://aka.ms/new-console-template for more information
using fissaa;
using fissaa.commands;
using fissaa.commands.Budget;
using fissaa.commands.domain;
using fissaa.commands.Env;
using fissaa.commands.infrastructure;
using fissaa.commands.storage;
using fissaa.commands.Templates;
using Spectre.Console.Cli;

var app = new CommandApp();


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
        budg.AddCommand<BudgetShowCommand>("cost-list");
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
            storageInit.AddCommand<S3InitCommand>("init");
        });

    });
    config.AddBranch<TemplateSettings>("template", command =>
    {
        command.AddCommand<TemplateUseCommand>("use");
    });
    config.AddBranch("env", command =>
    {
        command.AddCommand<EnvInitCommand>("init");
    });
});
return await app.RunAsync(args);
