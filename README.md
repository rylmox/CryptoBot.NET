# CryptoBot.NET 

**WIP - Pull requests welcome!**

Minimalistic .NET trading bot

Planned strategies:
* Dollar Cost Averaging
* Smart Order Routing
* Triangular Arbitrage
* ...

Multi-Exchange support
* Binance
* ByBit
* ...

Risk Management
* API failure
* Stop loss

Fast Execution
* Low latenbcy using WebSockets when available

## Disclaimer

This software is provided as-is with no guarantees. Crypto trading is risky — **you may lose money**. Use this bot at your own risk and comply with all laws. The developers are not liable for any losses or damages.

## Triangular Arbitrage

Support multiple paths but limited to 3 steps direct arbitrage at the moment (See StrategyTrading.ComputeOrderQuantities).
The orders quantities are spread for the 3 steps. The quantities take into account fees and a gross profit is estimated.
The bot start trading only when the cross rate ration and the gross arbitrage spread are profitable.

## Configuration

Binance has Live ("live") or TestNet ("testnet") environment. Pairs must be specified using slash format (e.g. BTC/USDT).

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
