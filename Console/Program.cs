using System.CommandLine;
using Application;

namespace Console;

public sealed class Program
{
    private static async Task<int> ParseCommandLine(string[] args)
    {
        Option<string> configFilePath = new("--config", "Config path") { IsRequired = true };

        var rootCommand = new RootCommand("CryptoBot.NET🚀")
        {
            configFilePath,
        };

        rootCommand.SetHandler(async configFilePath =>
        {
            await ApplicationHost.Run(configFilePath);
        }, configFilePath);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> Main(string[] args)
    {
        return await ParseCommandLine(args);
    }
}