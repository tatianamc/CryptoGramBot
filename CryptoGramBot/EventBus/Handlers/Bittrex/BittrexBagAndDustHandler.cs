﻿using System;
using System.Threading.Tasks;
using CryptoGramBot.Configuration;
using CryptoGramBot.EventBus.Events;
using CryptoGramBot.Helpers;
using CryptoGramBot.Models;
using CryptoGramBot.Services;
using CryptoGramBot.Services.Exchanges;
using CryptoGramBot.Services.Pricing;
using Enexure.MicroBus;

namespace CryptoGramBot.EventBus.Handlers.Bittrex
{
    public class BittrexBagAndDustHandler : IEventHandler<BagAndDustEvent>
    {
        private readonly BagConfig _bagConfig;
        private readonly BittrexService _bittrexService;
        private readonly IMicroBus _bus;
        private readonly DatabaseService _databaseService;
        private readonly DustConfig _dustConfig;
        private readonly GeneralConfig _generalConfig;
        private readonly LowBtcConfig _lowBtcConfig;
        private readonly PriceService _priceService;

        public BittrexBagAndDustHandler(
            IMicroBus bus,
            GeneralConfig generalConfig,
            BittrexService bittrexService,
            PriceService priceService,
            DatabaseService databaseService,
            BagConfig bagConfig,
            DustConfig dustConfig,
            LowBtcConfig lowBtcConfig)
        {
            _bus = bus;
            _generalConfig = generalConfig;
            _bittrexService = bittrexService;
            _priceService = priceService;
            _databaseService = databaseService;
            _bagConfig = bagConfig;
            _dustConfig = dustConfig;
            _lowBtcConfig = lowBtcConfig;
        }

        public async Task Handle(BagAndDustEvent @event)
        {
            var balanceInformation = await _bittrexService.GetBalance();

            foreach (var walletBalance in balanceInformation.WalletBalances)
            {
                if (walletBalance.Currency == "BTC")
                {
                    if (_lowBtcConfig.Enabled)
                    {
                        if (walletBalance.BtcAmount <= _lowBtcConfig.LowBtcAmount)
                        {
                            await SendBtcLowNotification(walletBalance.BtcAmount);
                        }
                    }
                }

                if (walletBalance.Currency != "BTC" && walletBalance.Currency != "USDT" &&
                    walletBalance.Currency != "USD")
                {
                    var averagePrice = await _databaseService.GetBuyAveragePrice(_generalConfig.TradingCurrency, walletBalance.Currency, Constants.Poloniex, walletBalance.Available);
                    var currentPrice = await _priceService.GetPrice(_generalConfig.TradingCurrency, walletBalance.Currency);

                    if (_bagConfig.Enabled)
                    {
                        await BagManagement(currentPrice, averagePrice, walletBalance);
                    }

                    if (_dustConfig.Enabled)
                    {
                        await DustManagement(walletBalance);
                    }
                }
            }
        }

        private async Task BagManagement(decimal currentPrice, decimal averagePrice, WalletBalance walletBalance)
        {
            var percentageDrop = ProfitCalculator.PriceDifference(currentPrice, averagePrice);
            if (percentageDrop < -_bagConfig.PercentageDrop)
            {
                await SendBagNotification(walletBalance, averagePrice, currentPrice, percentageDrop);
            }
        }

        private async Task DustManagement(WalletBalance walletBalance)
        {
            var bagDetected = walletBalance.BtcAmount <= _dustConfig.BtcAmount;
            if (bagDetected)
            {
                await SendDustNotification(walletBalance);
            }
        }

        private async Task SendBagNotification(WalletBalance walletBalance, decimal averagePrice, decimal currentPrice, decimal percentageDrop)
        {
            var lastBought =
                await _databaseService.GetLastBoughtAsync(_generalConfig.TradingCurrency, walletBalance.Currency, Constants.Bittrex);

            var message =
                $"<strong>{Constants.Bittrex}</strong>: {DateTime.Now:g}\n" +
                $"<strong>Bag detected for {walletBalance.Currency}</strong>\n" +
                $"Average bought price: {averagePrice:#0.#############}\n" +
                $"Current price: {currentPrice:#0.#############}\n" +
                $"Percentage: {percentageDrop}%\n" +
                $"Bought on: {lastBought:g}\n" +
                $"Value: {walletBalance.Balance * currentPrice:#0.#####} {_generalConfig.TradingCurrency}";

            await _bus.SendAsync(new SendMessageCommand(message));
        }

        private async Task SendBtcLowNotification(decimal walletBalanceBtcAmount)
        {
            var message =
                $"<strong>{Constants.Bittrex}</strong>: {DateTime.Now:g}\n" +
                $"<strong>Low {_generalConfig.TradingCurrency} detected</strong>\n" +
                $"{_generalConfig.TradingCurrency} Amount: {walletBalanceBtcAmount:#0.#############}\n";
            await _bus.SendAsync(new SendMessageCommand(message));
        }

        private async Task SendDustNotification(WalletBalance walletBalance)
        {
            var message =
                $"<strong>{Constants.Bittrex}</strong>: {DateTime.Now:g}\n" +
                $"<strong>Dust detected for {walletBalance.Currency}</strong>\n" +
                $"{_generalConfig.TradingCurrency} Amount: {walletBalance.BtcAmount:#0.#############}\n";
            await _bus.SendAsync(new SendMessageCommand(message));
        }
    }
}