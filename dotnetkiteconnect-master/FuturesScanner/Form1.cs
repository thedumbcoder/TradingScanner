using FuturesScanner.DataObjects;
using KiteConnect;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FuturesScanner
{
    public partial class Form_FuturesScanner : Form
    {

        // instances of Kite and Ticker
        static Ticker ticker;
        static Kite kite;
  
        // Initialize key and secret of your app
        static string MyAPIKey = "jtp1yaoxkmnmdyu0";
        static string MySecret = "9g9pr4vfi2zicbjk08ocycz722d8oz9v";
        static string MyUserId = "ZS6149";

        static Form_FuturesScanner frm;
        // persist these data in settings or db or file
        static string MyPublicToken = "abcdefghijklmnopqrstuvwxyz";
        static string MyAccessToken = "abcdefghijklmnopqrstuvwxyz";
        public static readonly TimeSpan _whenTimeIsOver = new TimeSpan(9, 15, 00);
        public static List<DataObjects.Subscriptions> longSubs = new List<DataObjects.Subscriptions>();
        public static List<DataObjects.Subscriptions> shortSubs = new List<DataObjects.Subscriptions>();

        public static BindingList<DataObjects.Signal> signals = new BindingList<DataObjects.Signal>();

        public Form_FuturesScanner()
        {
           
            InitializeComponent();
            frm = this;
            Control.CheckForIllegalCrossThreadCalls = false;
            GetLongSubscriptions();
            GetShortSubscriptions();
        }

      

      
        public void GetLongSubscriptions()
        {
            var lines = File.ReadAllLines("InputData/Longs.csv");

            foreach (string item in lines.Skip(1))
            {
                var values = item.Split(',');
                DataObjects.Subscriptions sub = new DataObjects.Subscriptions();
                sub.Company = values[1];
                sub.InstrumentCode = values[2];
                sub.Symbol = values[0];
                sub.OrderPrice = Convert.ToDecimal(values[3]);
                sub.TargetPrice = Convert.ToDecimal(values[4]);
                sub.StopLoss = values[5]==string.Empty?0: Convert.ToDecimal(values[5]);
                sub.LotSize = values[6] == string.Empty ? 0 : Convert.ToInt32(values[6]);
          
                longSubs.Add(sub);

            }

        }


        public void GetShortSubscriptions()
        {
            var lines = File.ReadAllLines("InputData/Shorts.csv");

            foreach (string item in lines.Skip(1))
            {
                var values = item.Split(';');
                DataObjects.Subscriptions sub = new DataObjects.Subscriptions();
                sub.Company = values[1];
                sub.InstrumentCode = values[2];
                sub.Symbol = values[0];
                sub.OrderPrice = Convert.ToDecimal(values[3]);
                sub.TargetPrice= Convert.ToDecimal(values[4]);
                sub.StopLoss = values[5]==string.Empty ? 0 : Convert.ToDecimal(values[5]);
                sub.LotSize = values[6] == string.Empty ? 0 : Convert.ToInt32(values[6]);
                shortSubs.Add(sub);

            }
        }



        private static void initSession()
        {

            Console.WriteLine("Goto " + kite.GetLoginURL());

            if( Properties.Settings.Default.MyAccessToken!=String.Empty && Properties.Settings.Default.MyPublicToken !=String.Empty)
            {

                MyAccessToken = Properties.Settings.Default.MyAccessToken;
                MyPublicToken = Properties.Settings.Default.MyPublicToken;
            }
            else
            {
                FormEnterToken getTokenForm = new FormEnterToken();
                getTokenForm.ShowDialog();


                User user = kite.RequestAccessToken(DataObjects.Token.apitoken, MySecret);
                MyAccessToken = user.AccessToken;
                MyPublicToken = user.PublicToken;
          
     
            }


          

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
            

            ticker.EnableReconnect(Interval: 5, Retries: 50);
            ticker.Connect();

            // Subscribing to NIFTY50 and setting mode to LTP

            //ticker.Subscribe(Tokens: new string[] { "256265", "2928385" });
            // ticker.SetMode(Tokens: new string[] { "256265", "2928385" }, Mode: "ltp");
            string[] Longs = longSubs.Select(xe => xe.InstrumentCode).ToArray();
            string[] shorts = shortSubs.Select(xe => xe.InstrumentCode).ToArray();
            string[] finalSubs = Longs.Concat(shorts).ToArray();
            ticker.Subscribe(Tokens: finalSubs);
            ticker.SetMode(Tokens: finalSubs, Mode: "ltp");
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

        private static  void onReconnect()
        {
            Console.WriteLine("Reconnecting");
        }

        private static void onTick(Tick TickData)
        {
           

            DataObjects.Subscriptions subLongsinstrument = longSubs.Where(x => x.InstrumentCode == TickData.InstrumentToken.ToString()).FirstOrDefault();

            if (subLongsinstrument != null)
            {
                if (TickData.LastPrice >= subLongsinstrument.OrderPrice && TickData.LastPrice< subLongsinstrument.TargetPrice && TickData.LastPrice+(TickData.LastPrice*3)/100 < subLongsinstrument.TargetPrice)
                {

                    string today = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
                  // List<Historical> historical_timestam2p = kite.GetHistorical(TickData.InstrumentToken.ToString(), "2017-11-10", "2017-11-10", "15minute");
                  ////  List<Historical> historical_timestamp = kite.GetHistorical(TickData.InstrumentToken.ToString(), today, today, "15minute");
                    if (signals.Count > 0)
                    {
                        DataObjects.Signal signal = signals.Where(x => x.instrumentcode == TickData.InstrumentToken.ToString()).FirstOrDefault();
                        if (signal != null)
                        {
                           
                            signal.LastTradePrice = TickData.LastPrice;
                  
                            signal.MaxProfit = subLongsinstrument.LotSize * Convert.ToDecimal(signal.TargetPrice - signal.LastTradePrice);
                            signal.PointsToTarget = signal.TargetPrice - signal.LastTradePrice;
                            signal.ProfitToLossRatio = ((signal.MaxProfit / signal.MaxLoss) * 100).ToString() + "%";
                            signal.Percent3TargetProfit = (signal.LastTradePrice * 3) / 100;
                            signal.SLPoints = (signal.LastTradePrice * 1) / 100;
                            signal.Percent3TargetPL = subLongsinstrument.LotSize * Convert.ToDecimal(signal.Percent3TargetProfit);
                            signal.ProfitToLossRatio = ((signal.MaxProfit / signal.MaxLoss) * 100).ToString() + "%";
                        }
                        else
                        {
                            signal = new DataObjects.Signal();
                            signal.LastTradePrice = TickData.LastPrice;
                            signal.script = subLongsinstrument.Symbol;
                            signal.signal = DataObjects.TradeType.Buy;
                            signal.instrumentcode = TickData.InstrumentToken.ToString();
                            signal.OrderPrice = subLongsinstrument.OrderPrice;
                            signal.LastTradePrice = Convert.ToInt32(TickData.LastPrice);
                            signal.StopLoss = subLongsinstrument.StopLoss;
                            signal.TargetPrice = subLongsinstrument.TargetPrice;
                            signal.LotSize = subLongsinstrument.LotSize;
                            signal.MaxLoss = subLongsinstrument.LotSize*Convert.ToDecimal(subLongsinstrument.OrderPrice/100);
                            signal.MaxProfit = subLongsinstrument.LotSize * Convert.ToDecimal(signal.TargetPrice - signal.LastTradePrice);
                            signal.Percent3TargetProfit = (signal.LastTradePrice*3)/100;
                            signal.Percent3TargetPL = subLongsinstrument.LotSize * Convert.ToDecimal(signal.TargetPrice - signal.LastTradePrice);
                            signal.SLPoints = (signal.LastTradePrice * 1) / 100;
                            signal.PointsToTarget = signal.TargetPrice - signal.LastTradePrice;
                            signal.ProfitToLossRatio = ((signal.MaxProfit / signal.MaxLoss) * 100).ToString() + "%";
                            signal.Percent3TargetPL = subLongsinstrument.LotSize * Convert.ToDecimal(signal.Percent3TargetProfit);
                            signals.Add(signal);
                        }


                    }
                    else
                    {
                      
                        DataObjects.Signal signal = new DataObjects.Signal();
                        signal.LastTradePrice = TickData.LastPrice;
                        signal.script = subLongsinstrument.Symbol;
                        signal.signal = DataObjects.TradeType.Buy;
                        signal.instrumentcode = TickData.InstrumentToken.ToString();
                        signal.OrderPrice = subLongsinstrument.OrderPrice;
                        signal.LastTradePrice = Convert.ToInt32(TickData.LastPrice);
                        signal.StopLoss = subLongsinstrument.StopLoss;
                        signal.TargetPrice = subLongsinstrument.TargetPrice;
                        signal.LotSize = subLongsinstrument.LotSize;
                        signal.MaxLoss = subLongsinstrument.LotSize * Convert.ToDecimal(subLongsinstrument.OrderPrice / 100);
                        signal.MaxProfit = subLongsinstrument.LotSize * Convert.ToDecimal(signal.TargetPrice - signal.LastTradePrice);
                        signal.PointsToTarget = signal.TargetPrice - signal.LastTradePrice;
                        signal.ProfitToLossRatio = ((signal.MaxProfit / signal.MaxLoss) * 100).ToString() + "%";
                        signal.Percent3TargetProfit = (signal.LastTradePrice * 3) / 100;
                        signal.SLPoints = (signal.LastTradePrice * 1) / 100;
                        signal.Percent3TargetPL = subLongsinstrument.LotSize * Convert.ToDecimal(signal.Percent3TargetProfit);
                        signal.ProfitToLossRatio = ((signal.MaxProfit / signal.MaxLoss) * 100).ToString() + "%";
                        signals.Add(signal);
                    }


                }
                else
                {
                    if (signals.Count > 0)
                    {
                        DataObjects.Signal signal = signals.Where(x => x.instrumentcode == TickData.InstrumentToken.ToString()).FirstOrDefault();
                        if (signal != null)
                        {
                            signals.Remove(signal);
                        }


                    }
                }
            }



            DataObjects.Subscriptions subShortssinstrument = shortSubs.Where(x => x.InstrumentCode == TickData.InstrumentToken.ToString()).FirstOrDefault();
            if (subShortssinstrument != null)
            {
                string today = string.Empty;
                if(DateTime.Now.TimeOfDay < _whenTimeIsOver)
                {
                    DateTime yesterday = DateTime.Now.AddDays(-1);
                    today =yesterday.Date.ToString("yyyy-MM-dd");
                }
                else
                {
                     today = DateTime.Now.ToString("yyyy-MM-dd");
               
          
                }
              
              
              
                if (TickData.LastPrice <= subShortssinstrument.OrderPrice && TickData.LastPrice > subShortssinstrument.TargetPrice && TickData.LastPrice-(TickData.LastPrice*3)/100 > subShortssinstrument.TargetPrice)
                {
                    if (signals.Count > 0)
                    {
                        DataObjects.Signal sellsignal = signals.Where(x => x.instrumentcode == TickData.InstrumentToken.ToString()).FirstOrDefault();
                        if (sellsignal != null)
                        {
                            sellsignal.LastTradePrice = TickData.LastPrice;
                            sellsignal.MaxProfit = subShortssinstrument.LotSize * Convert.ToDecimal(sellsignal.LastTradePrice - sellsignal.TargetPrice);
                            sellsignal.PointsToTarget =  sellsignal.LastTradePrice- sellsignal.TargetPrice;
                            sellsignal.ProfitToLossRatio = ((sellsignal.MaxProfit / sellsignal.MaxLoss) * 100).ToString() + "%";
                            sellsignal.Percent3TargetProfit = (sellsignal.LastTradePrice * 3) / 100;
                            sellsignal.SLPoints = (sellsignal.LastTradePrice * 1) / 100;
                            sellsignal.Percent3TargetPL = subShortssinstrument.LotSize * Convert.ToDecimal(sellsignal.Percent3TargetProfit);
                        }
                        else
                        {
                            sellsignal = new DataObjects.Signal();
                            sellsignal.LastTradePrice = TickData.LastPrice;
                            sellsignal.script = subShortssinstrument.Symbol;
                            sellsignal.signal = DataObjects.TradeType.Sell;
                            sellsignal.instrumentcode = TickData.InstrumentToken.ToString();
                            sellsignal.OrderPrice = subShortssinstrument.OrderPrice;
                            sellsignal.StopLoss = subShortssinstrument.StopLoss;
                            sellsignal.TargetPrice = subShortssinstrument.TargetPrice;
                            sellsignal.LotSize = subShortssinstrument.LotSize;
                            sellsignal.MaxLoss = subShortssinstrument.LotSize * Convert.ToDecimal(subShortssinstrument.OrderPrice / 100);
                            sellsignal.MaxProfit = subShortssinstrument.LotSize * Convert.ToDecimal(sellsignal.LastTradePrice- sellsignal.TargetPrice);
                            sellsignal.PointsToTarget = sellsignal.LastTradePrice - sellsignal.TargetPrice;
                            sellsignal.ProfitToLossRatio = ((sellsignal.MaxProfit / sellsignal.MaxLoss) * 100).ToString() + "%";
                             sellsignal.Percent3TargetProfit = (sellsignal.LastTradePrice * 3) / 100;
                            sellsignal.SLPoints = (sellsignal.LastTradePrice * 1) / 100;
                            sellsignal.Percent3TargetPL = subShortssinstrument.LotSize * Convert.ToDecimal(sellsignal.Percent3TargetProfit);

                            signals.Add(sellsignal);
                        }

                    }
                    else
                    {
                       
                            DataObjects.Signal sellsignal = new DataObjects.Signal();
                            sellsignal.LastTradePrice = TickData.LastPrice;
                            sellsignal.script = subShortssinstrument.Symbol;
                            sellsignal.signal = DataObjects.TradeType.Sell;
                            sellsignal.instrumentcode = TickData.InstrumentToken.ToString();
                            sellsignal.OrderPrice = subShortssinstrument.OrderPrice;
                            sellsignal.StopLoss = subShortssinstrument.StopLoss;
                        sellsignal.TargetPrice = subShortssinstrument.TargetPrice;
                        sellsignal.LotSize = subShortssinstrument.LotSize;
                        sellsignal.MaxLoss = subShortssinstrument.LotSize * Convert.ToDecimal(subShortssinstrument.OrderPrice / 100);
                        sellsignal.MaxProfit = subShortssinstrument.LotSize * Convert.ToDecimal(sellsignal.LastTradePrice - sellsignal.TargetPrice);
                        sellsignal.PointsToTarget = sellsignal.LastTradePrice - sellsignal.TargetPrice;
                        sellsignal.ProfitToLossRatio = ((sellsignal.MaxProfit / sellsignal.MaxLoss) * 100).ToString() + "%";
                        signals.Add(sellsignal);
                        
                    }
                }
                else
                {
                    DataObjects.Signal signal = signals.Where(x => x.instrumentcode == TickData.InstrumentToken.ToString()).FirstOrDefault();
                    if (signal != null)
                    {
                        signals.Remove(signal);
                    }
                }
            }
            var source = new BindingSource();
            source.DataSource = signals.Where(x => x.signal == TradeType.Buy).OrderByDescending(x => x.ProfitToLossRatio).ToList();
            source.ResetBindings(true);
            source.CurrencyManager.Refresh();
            Action action = () => frm.dataGridView1.DataSource = source;
            
            frm.dataGridView1.Invoke(action);


            var sellsource = new BindingSource();
            sellsource.DataSource = signals.Where(x => x.signal == TradeType.Sell).OrderByDescending(x=>x.ProfitToLossRatio).ToList();
            sellsource.ResetBindings(true);
            sellsource.CurrencyManager.Refresh();
            Action action2 = () => frm.dataGridView2.DataSource = sellsource;

            frm.dataGridView2.Invoke(action2);




        }





        // helper funtion to get json from nested string dictionaries
        static string JsonSerialize(object x)
        {
            var jss = new JavaScriptSerializer();
            return jss.Serialize(x);
        }

        private void Form_FuturesScanner_Load(object sender, EventArgs e)
        {

            Timer timer = new Timer();
            timer.Interval = (45 * 1000); // 10 secs
            timer.Tick += Timer_Tick;
            timer.Start();

            kite = new Kite(MyAPIKey, Debug: true);

            // For handling 403 errors

            kite.SetSessionHook(onTokenExpire);

            // Initializes the login flow

            try
            {
                initSession();
            }
            catch (Exception ex)
            {
                // Cannot continue without proper authentication
                Console.WriteLine(ex.Message);

                Environment.Exit(0);
            }

            kite.SetAccessToken(MyAccessToken);

            // Initialize ticker

            initTicker();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            ticker.Close();
            ticker.Connect();
           
          
        }

        private void dataGridView1_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["signal"].Value != null)
                {
                    DataObjects.TradeType signal = (TradeType)row.Cells["signal"].Value;
                    switch (signal)
                    {
                        case DataObjects.TradeType.Buy:
                            row.Cells["signal"].Style.BackColor = Color.Green;
                            break;
                        case DataObjects.TradeType.Sell:
                            row.Cells["signal"].Style.BackColor = Color.Red;
                            break;

                    }
                }


            }
        }

        private void dataGridView2_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            foreach (DataGridViewRow row in dataGridView2.Rows)
            {
                if (row.Cells["signal"].Value != null)
                {
                    DataObjects.TradeType signal = (TradeType)row.Cells["signal"].Value;
                    switch (signal)
                    {
                        case DataObjects.TradeType.Buy:
                            row.Cells["signal"].Style.BackColor = Color.Green;
                            break;
                        case DataObjects.TradeType.Sell:
                            row.Cells["signal"].Style.BackColor = Color.Red;
                            break;

                    }
                }


            }
        }
    }
}
   

