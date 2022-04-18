using Spectre.Console;

namespace fissaa.Decorator;

public static class ConsoleDisplayDecorator
{


    public static async Task DisplayStatus(Func<Task> func,string startStatus)
    {
        await AnsiConsole.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Aesthetic)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync(startStatus, async ctx =>
            {
                await func();
            });
        
    }
}