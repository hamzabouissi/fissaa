using Spectre.Console;

namespace fissaa.Decorator;

public static class ConsoleDisplayDecorator
{

    public static void Display(string message)
    {
       AnsiConsole.MarkupLine($"[grey]Log: {message}[/]");
    }
}