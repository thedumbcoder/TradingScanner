using System;
using System.Web.Script.Serialization;
using KiteConnect;
using System.Collections.Generic;
using System.Net;

namespace KiteConnectSample
{
    class Program
    {
        // instances of Kite and Ticker
        static Ticker ticker;
        static Kite kite;
        
        // Initialize key and secret of your app
        static string MyAPIKey = "jtp1yaoxkmnmdyu0";
        static string MySecret = "9g9pr4vfi2zicbjk08ocycz722d8oz9v";
        static string MyUserId = "ZS6149";

        // persist these data in settings or db or file
        static string MyPublicToken = "6d48e39ca68c975685b6968ef6720958";
        static string MyAccessToken = "icak2z2teo8anig653nhfz3uz2qsvzxk";

        static void Main(string[] args)
        {
            kite = new Kite(MyAPIKey, Debug: true);

            // For handling 403 errors

            kite.SetSessionHook(onTokenExpire);

            // Initializes the login flow

            try
            {
                initSession();
            }
            catch (Exception e)
            {
                // Cannot continue without proper authentication
                Console.WriteLine(e.Message);
                Console.ReadKey();
                Environment.Exit(0);
            }

            kite.SetAccessToken(MyAccessToken);

            // Initialize ticker
            
            initTicker();

            // Positions

           

            kite.ModifyProduct("NSE", "ASHOKLEY", "BUY", "day", "1", "MIS", "CNC");

            // Holdings

          //  List<Holding> holdings = kite.GetHoldings();
         //   Console.WriteLine(JsonSerialize(holdings[0]));

            // Instruments

           // List<Instrument> instruments = kite.GetInstruments();
           // Console.WriteLine(JsonSerialize(instruments[0]));

            // Quote

           
           
            

            // Trades

     

            // Margins

      

            // Historical Data With Dates

            List<Historical> historical = kite.GetHistorical("5633", "2015-12-28", "2016-01-01", "minute");
            //Console.WriteLine(JsonSerialize(historical[0]));

            //// Historical Data With Timestamps

            string today = DateTime.Today.ToString("yyyy-MM-dd");
            List<Historical> historical_timestam2p = kite.GetHistorical("5633", today, today, "15minute");

            List<Historical> historical_timestamp = kite.GetHistorical("2928385", "2017-11-18", "2017-11-18", "15minute");
            //Console.WriteLine(JsonSerialize(historical_timestamp[0]));

            // Continuous Historical Data

           // var x = kite.GetHistorical("5633", "2015-12-28", "2016-01-01", "15minute",true);
           
            //Console.WriteLine(JsonSerialize(historical_continuous[0]));

            // Mutual Funds Instruments

            
          
         

            Console.ReadKey();

            // Disconnect from ticker

            Console.ReadKey();
            ticker.Close();
		}

        private static void initSession()
        {
            Console.WriteLine("Goto " + kite.GetLoginURL());
            Console.WriteLine("Enter request token: ");
            string requestToken = Console.ReadLine();
            User user = kite.RequestAccessToken(requestToken, MySecret);
           
            
            Console.WriteLine(JsonSerialize(user));

           // MyAccessToken = user.AccessToken;
           // MyPublicToken = user.PublicToken;
        }

        private static void initTicker()
        {
            ticker = new Ticker(MyAPIKey, MyUserId, MyPublicToken);

            ticker.OnTick += onTick;
            ticker.OnReconnect += onReconnect;
            ticker.OnNoReconnect += oNoReconnect;
            ticker.OnError += onError;
            ticker.OnClose += onClose;
            ticker.OnConnect += onConnect;

            ticker.EnableReconnect(Interval: 5,Retries: 50);
            ticker.Connect();

            // Subscribing to NIFTY50 and setting mode to LTP
            ticker.Subscribe(Tokens: new string[] { "256265", "2928385" });
            ticker.SetMode(Tokens: new string[] { "256265", "2928385" }, Mode: "ltp");
        }

        private static void onTokenExpire()
        {
            Console.WriteLine("Need to login again");
        }

        private static void onConnect()
        {
            Console.WriteLine("Connected ticker");
        }

        private static void onClose()
        {
            Console.WriteLine("Closed ticker");
        }

        private static void onError(string Message)
        {
            Console.WriteLine("Error: " + Message);
        }

        private static void oNoReconnect()
        {
            Console.WriteLine("Not reconnecting");
        }

        private static void onReconnect()
        {
            Console.WriteLine("Reconnecting");
        }

        private static void onTick(Tick TickData)
        {
            Console.WriteLine("Tick " + JsonSerialize(TickData));
            Console.ReadKey();
        }

        // helper funtion to get json from nested string dictionaries
        static string JsonSerialize(object x)
        {
            var jss = new JavaScriptSerializer();
            return jss.Serialize(x);
        }
    }
}
