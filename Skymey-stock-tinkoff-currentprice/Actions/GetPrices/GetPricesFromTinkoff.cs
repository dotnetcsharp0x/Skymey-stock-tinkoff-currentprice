using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Skymey_main_lib.Models.Prices.StockPrices;
using Skymey_stock_tinkoff_currentprice.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

namespace Skymey_stock_tinkoff_currentprice.Actions.GetPrices
{
    public class GetPricesFromTinkoff
    {
        private MongoClient _mongoClient;
        private ApplicationContext _db;
        private string _apiKey;
        public GetPricesFromTinkoff()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);

            IConfiguration config = builder.Build();

            _apiKey = config.GetSection("ApiKeys:Tinkoff").Value;
            _mongoClient = new MongoClient("mongodb://127.0.0.1:27017");
            _db = ApplicationContext.Create(_mongoClient.GetDatabase("skymey"));
        }
        public void GetCurrentPricesFromTinkoff()
        {
            var client = InvestApiClientFactory.Create(_apiKey);
            var request = new GetLastPricesRequest
            {
                Figi = {  }
            };
            var response = client.MarketData.GetLastPrices(request);
            var find_shares = (from i in _db.Shares select i).AsNoTracking();
            var find_bonds = (from i in _db.Bonds select i).AsNoTracking();
            var find_etf = (from i in _db.Etf select i).AsNoTracking();
            var find_futures = (from i in _db.Futures select i).AsNoTracking();
            var find_prices = (from i in _db.StockPrices select i);
            foreach (var item in response.LastPrices)
            {
                var find_in_db = (from i in find_prices where i.Figi == item.Figi select i).FirstOrDefault();
                if (find_in_db == null)
                {
                    StockPrices sp = new StockPrices();
                    sp._id = ObjectId.GenerateNewId();
                    sp.Figi = item.Figi;
                    if (sp.Ticker == null)
                    {
                        var ticker = (from i in find_shares where i.figi == item.Figi select i).FirstOrDefault();
                        if (ticker != null) sp.Ticker = ticker.ticker;
                    }
                    if (sp.Ticker == null)
                    {
                        var ticker = (from i in find_bonds where i.figi == item.Figi select i).FirstOrDefault();
                        if (ticker != null) sp.Ticker = ticker.ticker;
                    }
                    if (sp.Ticker == null)
                    {
                        var ticker = (from i in find_etf where i.figi == item.Figi select i).FirstOrDefault();
                        if (ticker != null) sp.Ticker = ticker.ticker;
                    }
                    if (sp.Ticker == null)
                    {
                        var ticker = (from i in find_futures where i.figi == item.Figi select i).FirstOrDefault();
                        if (ticker != null) sp.Ticker = ticker.ticker;
                    }
                    if (sp.Ticker != null)
                    {
                        sp.Price = Convert.ToDouble(item.Price);
                        sp.Update = DateTime.UtcNow;
                        _db.StockPrices.Add(sp);
                    }
                }
                else
                {
                    find_in_db.Price = (Convert.ToDouble(item.Price) + find_in_db.Price) / 2;
                    find_in_db.Update = DateTime.UtcNow;
                }
            }
            _db.SaveChanges();
        }
    }
}
