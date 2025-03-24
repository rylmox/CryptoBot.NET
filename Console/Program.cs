using System.CommandLine;
using Application;

namespace Console;

public sealed class Program
{
    private static async Task<int> ParseCommandLine(string[] args)
    {
        Option<string> configFilePath = new("--config", "Config path") { IsRequired = true };

        // Example of option validator
        // var ageOption = new Option<int>("--age", "Your age");
        // ageOption.AddValidator(result =>
        // {
        //     if (result.GetValueOrDefault<int>() < 18)
        //     {
        //         result.ErrorMessage = "❌ Age must be 18 or older.";
        //     }
        // });

        var rootCommand = new RootCommand("🚀 CryptoBot.NET")
        {
            configFilePath,
            // ageOption
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