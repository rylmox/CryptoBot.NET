namespace Application;

using Engine.Strategies;
using Engine.Clients;
using Engine.Orders;
using Engine.Strategies.TriangularArbitrage;
using Engine.Strategies.TriangularArbitrage.Signals;
using Engine.Strategies.TriangularArbitrage.Workers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class ApplicationHost()
{
    public static async Task Run(string configFilePath)
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureHostOptions(options =>
            {
                options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
                // Sets the maximum time allowed for hosted services to start or stop before failing.
                // options.ShutdownTimeout = TimeSpan.FromSeconds(1);
                // options.StartupTimeout = TimeSpan.FromSeconds(5);
                options.ServicesStartConcurrently = false;
                options.ServicesStopConcurrently = false;
            })
            .ConfigureAppConfiguration(config =>
            {
                config.AddJsonFile(configFilePath, optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddLogging();
                RegisterApplicationServices(context, services);
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
            })
            .Build();

        await host.RunAsync();
    }

    private static void RegisterApplicationServices(HostBuilderContext context, IServiceCollection services)
    {
        RegisterStrategyServices(context.Configuration, services);
        RegisterTelegramBotService(services);
    }
    
    private static void RegisterStrategyServices(IConfiguration configuration, IServiceCollection services)
    {
        // Load strategies statically for now for simplicity and avoid reflection for performance reasons
        // To load strategies dynamically, use reflection as shown below:
        //
        // if (Type.GetType(typeName, false, true) is Type strategyType)
        // {
        //     services.AddSingleton(typeof(Engine.Strategies.IStrategy), strategyType);
        // }

        string strategy = configuration.GetValue("Strategy:Name", "TriangularArbitrage");

        services.AddHostedService<Engine.Service.StrategyService>();
        services.AddSingleton<ISpotOrderManager, SpotOrderManager>();
        // TODO SymbolManager should be a singleton and handles all price updates
        services.AddTransient<ISymbolPriceManager, SymbolPriceManager>();

        switch (strategy)
        {
            case nameof(TriangularArbitrageStrategy):
                RegisterTriangularArbitrageStrategy(services, configuration);
                break;

            default:
                throw new NotSupportedException($"The strategy '{strategy}' is not supported.");
        }
    }

    private static void RegisterTriangularArbitrageStrategy(IServiceCollection services, IConfiguration configuration)
    {
        // Register the exchange client and strategy worker
        // The exchange client is used to interact with the exchange API
        // The strategy worker is used to execute the strategy
        // The strategy signals is used to send signals to the strategy
        // The strategy worker implementation is exchange-specific and should be implemented for each exchange but the signals are generic for now

        string exchange = configuration.GetValue("Exchange:Name", "Binance");

        services.AddSingleton<IStrategy, TriangularArbitrageStrategy>();
        services.AddTransient<IArbitrageSignals, ArbitrageSignals>();

        switch (exchange)
        {
            case "Binance":
                services.AddBinance(configuration.GetSection("Client"));
                services.AddSingleton<IExchangeClient, BinanceClient>();
                services.AddTransient<IArbitrageWorker, BinanceArbitrageWorker>();
                break;

            default:
                throw new NotSupportedException($"The exchange client '{exchange}' is not supported.");
        }
    }

    private static void RegisterTelegramBotService(IServiceCollection services)
    {
        services.AddHostedService<Telegram.TelegramBot>();
    }
}
