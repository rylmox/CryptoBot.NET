namespace Telegram;

using Engine.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

public sealed class TelegramBot(IConfiguration configuration, ILogger<TelegramBot> logger, IHostApplicationLifetime hostApplicationLifetime, IServiceProvider provider) : BackgroundService
{
    private CancellationToken _stoppingAppToken = CancellationToken.None;
    private TelegramBotClient? _client;
    private Chat? _chat;
    private StrategyService? _strategyService;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _strategyService = provider.GetServices<IHostedService>().OfType<StrategyService>().FirstOrDefault();

        string? token = configuration.GetValue<string>("Telegram:Token");

        if (string.IsNullOrEmpty(token))
        {
            // TODO revisit the logger class
            Logger.LogInvalidToken(logger);
            return;
        }

        try
        {
            _stoppingAppToken = ct;
            ct.Register(Shutdown);
            await InitializeClient(token, ct);
        }
        catch(OperationCanceledException)
        {
            // TODO log bot has stopped
        }
    }

    private async Task InitializeClient(string token, CancellationToken ct)
    {
        _client = new(token, cancellationToken: ct);
        await ConfigureBotCommands(ct);
        _client.OnMessage += OnMessageReceived;
    }

    private void Shutdown()
    {
        try
        {
            CloseClient().GetAwaiter().GetResult();
        }
        catch(Exception)
        {
            // TODO log exception
        }
    }

    private async Task CloseClient()
    {
        await (_client?.Close(_stoppingAppToken) ?? Task.CompletedTask);
    }

    private async Task ConfigureBotCommands(CancellationToken ct)
    {
        if (_client == null)
        {
            return;
        }

        var commands = new[]
        {
            // TODO implement commands
            // new BotCommand { Command = "stats", Description = "Show trading statistics" },
            // new BotCommand { Command = "info", Description = "Get bot info" },
            // new BotCommand { Command = "balance", Description = "Get account balance" },
            // new BotCommand { Command = "price", Description = "Get asset command" },
            new BotCommand { Command = "state", Description = "Get strategy state" },
            new BotCommand { Command = "stop", Description = "Stop the strategy" },
            new BotCommand { Command = "start", Description = "Start the strategy" },
            new BotCommand { Command = "shutdown", Description = "Shutdown the bot" },
        };

        await _client.SetMyCommands(commands, cancellationToken: ct);
    }

    public async Task SendMessage(string? message)
    {
        if (_client == null || _chat == null || string.IsNullOrEmpty(message))
        {
            return;
        }

        if (message.Length > 512)
        {
            message = message[..(4096 - 4)] + "\n...";
        }

        await _client.SendMessage(_chat, message, cancellationToken: _stoppingAppToken);
    }

    private async Task OnMessageReceived(Message message, UpdateType updateType)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            return;
        }

        _chat ??= message.Chat;

        string command = message.Text.Split(' ')[0];
        string[] parameters = message.Text.Split(' ').Skip(1).ToArray();

        await ProcessCommand(command, parameters);
    }

    private async Task ProcessCommand(string command, string[] parameters)
    {
        switch (command.ToLower())
        {
            case "/ping":
                await SendMessage("pong");
                break;

            case "/stop":
                await StopStrategy();
                break;

            case "/start":
                await StartStrategy();
                break;

            case "/shutdown":
                await ShutdownApplication();
                break;

            case "/state":
                await StrategyState();
                break;

            default:
                await SendMessage($"Unknown command {command}");
                break;
        }
    }

    private async Task StrategyState()
    {
        await SendMessage(_strategyService?.Strategy.GetState());
    }

    private async Task StartStrategy()
    {
        await (_strategyService?.StartStrategy() ?? Task.CompletedTask);
        await SendMessage(_strategyService?.Strategy.GetState());
    }

    private async Task StopStrategy()
    {
        await (_strategyService?.StopStrategy() ?? Task.CompletedTask);
        await SendMessage(_strategyService?.Strategy.GetState());
    }

    private async Task ShutdownApplication()
    {
        await SendMessage("Shuting down the bot...");
        hostApplicationLifetime.StopApplication();
    }
}
