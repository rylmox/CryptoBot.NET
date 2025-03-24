# CryptoBot.NET 🚀

**WIP - Pull requests welcome!**

Minimalistic .NET trading bot

Planned strategies:
* Triangular Arbitrage
* Dollar Cost Averaging
* Smart Order Routing

Multi-Exchange support
* Binance
* ByBit
* ...

Risk Management
* API failure
* Stop loss

Fast Execution
* Low latency using WebSockets when available

## Disclaimer

This software is provided as-is with no guarantees. Crypto trading is risky — **you may lose money**. Use this bot at your own risk and comply with all laws. The developers are not liable for any losses or damages.

## Triangular Arbitrage

Path is limited to 3 steps direct arbitrage at the moment (See StrategyTrading.ComputeOrderQuantities).
The orders quantities are spread for the 3 steps. The quantities take into account the fees and a gross profit is estimated.
The bot start trading only when the cross rate ration and the gross arbitrage spread are profitable.

## Configuration

The main configuration is defined in `appsettings.json`. Another optional configuration can be specified using the `--config` paramters
and will be combined with the main config. It's recommended to store sensitive data, such as exchange and Telegram bot API keys, in `appsettings-dev.json` instead 
of the main config file to avoid accidentally comitting them to repository.

Test your strategy on a test net first. Binance has a Live (`live`) and TestNet (`testnet`) environment. 

Asset pairs must be specified using the slash format (e.g. BTC/USDT).

``` JSON
        "ApiCredentials": {
            "Key": "your-api-key",
            "Secret": "your-api-secret"
        },
        "Environment": { "Name": "testnet" },
        "AllowAppendingClientOrderId": true,
        "Rest": {
            "OutputOriginalData": true,
            "ReceiveWindow": "00:00:05"
        },
        "Socket": {
            "OutputOriginalData": false,
            "SpotOptions":{
                "SocketNoDataConnection": "00:01:00"
            }
        }
```

## Symbol format
Configuration file (appsettings.json) use slash format to represent asset pairs (e.g. BTC/USDT). 
The bot API uses the SharedSymbol abstraction record and exchange clients use specific format (e.g. binance API use no-separator format).


## Telegram bot

Telegram Bot to allow remote control of the crypto bot.

* Start, stop, and monitor the bot directly from Telegram
* Check current balances and trades stats