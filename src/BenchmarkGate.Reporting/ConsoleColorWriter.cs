namespace Bijecta.BenchmarkGate.Reporting;

internal static class ConsoleColorWriter
{
    internal static void WriteLine(TextWriter writer, ConsoleColor color, string text)
    {
        var canUseColor = ReferenceEquals(writer, Console.Out) && !Console.IsOutputRedirected;

        if (!canUseColor)
        {
            writer.WriteLine(text);
            return;
        }

        var previous = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            writer.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = previous;
        }
    }
}