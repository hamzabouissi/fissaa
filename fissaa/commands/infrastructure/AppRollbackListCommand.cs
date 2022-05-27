using fissaa.CloudProvidersServices;
using Spectre.Console;
using Spectre.Console.Cli;

namespace fissaa.commands.infrastructure;

public class AppRollbackListCommand:AsyncCommand<AppRollbackListSetting>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AppRollbackListSetting settings)
    {
        var appService = new AwsEcsService(settings.AwsSecretKey,settings.AwsAcessKey,settings.DomainName);
        var imageDetails = await appService.RollBackList();
        foreach (var image in imageDetails.OrderByDescending(p=>p.ImagePushedAt))
            AnsiConsole.MarkupLine($"DateTime: {image.ImagePushedAt}, ImageTag: {string.Join(' ',image.ImageTags)} ");
        return 0;
    }
    
    public override ValidationResult Validate(CommandContext context, AppRollbackListSetting settings)
    {
        return settings.Validate();
    }
}

