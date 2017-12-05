﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace KiteConnect
{
    /// <summary>
    /// The WebSocket client for connecting to Kite Connect's streaming quotes service.
    /// </summary>
    public class Ticker
    {
        // If set to true will print extra debug information
        private bool _debug = false;

        // Root domain for ticker. Can be changed with Root parameter in the constructor.
        private string _root = "wss://websocket.kite.trade/";

        // Configurations to create ticker connection
        private string _apiKey;
        private string _userID;
        private string _publicToken;
        private string _socketUrl = "";
        private bool _isReconnect = false;
        private int _interval = 5;
        private int _retries = 50;
        private int _retryCount = 0;

        // A watchdog timer for monitoring the connection of ticker.
        System.Timers.Timer _timer;
        int _timerTick = 5;

        // Instance of WebSocket class that wraps .Net version
        private WebSocket _ws;

        // Dictionary that keeps instrument_token -> mode data
        private Dictionary<string, string> _subscribedTokens;

        // Delegates for callbacks

        /// <summary>
        /// Delegate for OnConnect event
        /// </summary>
        public delegate void OnConnectHandler();

        /// <summary>
        /// Delegate for OnClose event
        /// </summary>
        public delegate void OnCloseHandler();

        /// <summary>
        /// Delegate for OnTick event
        /// </summary>
        /// <param name="TickData">Tick data</param>
        public delegate void OnTickHandler(Tick TickData);

        /// <summary>
        /// Delegate for OnError event
        /// </summary>
        /// <param name="Message">Error message</param>
        public delegate void OnErrorHandler(string Message);

        /// <summary>
        /// Delegate for OnReconnect event
        /// </summary>
        public delegate void OnReconnectHandler();

        /// <summary>
        /// Delegate for OnNoReconnect event
        /// </summary>
        public delegate void OnNoReconnectHandler();

        // Events that can be subscribed
        /// <summary>
        /// Event triggered when ticker is connected
        /// </summary>
        public event OnConnectHandler OnConnect;

        /// <summary>
        /// Event triggered when ticker is disconnected
        /// </summary>
        public event OnCloseHandler OnClose;

        /// <summary>
        /// Event triggered when ticker receives a tick
        /// </summary>
        public event OnTickHandler OnTick;

        /// <summary>
        /// Event triggered when ticker encounters an error
        /// </summary>
        public event OnErrorHandler OnError;

        /// <summary>
        /// Event triggered when ticker is reconnected
        /// </summary>
        public event OnReconnectHandler OnReconnect;

        /// <summary>
        /// Event triggered when ticker is not reconnecting after failure
        /// </summary>
        public event OnNoReconnectHandler OnNoReconnect;

        /// <summary>
        /// Initialize websocket client instance.
        /// </summary>
        /// <param name="APIKey">API key issued to you</param>
        /// <param name="UserID">Zerodha client id of the authenticated user</param>
        /// <param name="PublicToken">Token obtained after the login flow in 
        /// exchange for the `request_token`.Pre-login, this will default to None,
        /// but once you have obtained it, you should
        /// persist it in a database or session to pass
        /// to the Kite Connect class initialisation for subsequent requests.</param>
        /// <param name="Root">Websocket API end point root. Unless you explicitly 
        /// want to send API requests to a non-default endpoint, this can be ignored.</param>
        /// <param name="Reconnect">Enables WebSocket autreconnect in case of network failure/disconnection.</param>
        /// <param name="ReconnectInterval">Interval (in seconds) between auto reconnection attemptes. Defaults to 5 seconds.</param>
        /// <param name="ReconnectTries">Maximum number reconnection attempts. Defaults to 50 attempts.</param>
        public Ticker(string APIKey, string UserID, string PublicToken, string Root = null, bool Reconnect = false, int ReconnectInterval = 5, int ReconnectTries = 50)
        {
            _apiKey = APIKey;
            _userID = UserID;
            _publicToken = PublicToken;
            _subscribedTokens = new Dictionary<string, string>();
            _interval = ReconnectInterval;
            _timerTick = ReconnectInterval;
            _retries = ReconnectTries;
            if (!String.IsNullOrEmpty(Root))
                _root = Root;
            _isReconnect = Reconnect;
            _socketUrl = _root + String.Format("?api_key={0}&user_id={1}&public_token={2}", _apiKey, _userID, _publicToken);

            // initialize websocket
            _ws = new WebSocket(_socketUrl);
            _ws.OnConnect += _onConnect;
            _ws.OnData += _onData;
            _ws.OnClose += _onClose;
            _ws.OnError += _onError;

            // initializing  watchdog timer
            _timer = new System.Timers.Timer();
            _timer.Elapsed += _onTimerTick;
            _timer.Interval = 1000; // checks connection every second
        }

        private void _onError(string Message)
        {
            // pipe the error message from ticker to the events
            OnError?.Invoke(Message);
        }

        private void _onClose()
        {
            // stop the timer while normally closing the connection
            _timer.Stop();
            OnClose?.Invoke();
        }

        /// <summary>
        /// Reads 2 byte short int from byte stream
        /// </summary>
        private ushort ReadShort(byte[] b, ref int offset)
        {
            ushort data = (ushort)(b[offset + 1] + (b[offset] << 8));
            offset += 2;
            return data;
        }

        /// <summary>
        /// Reads 4 byte int32 from byte stream
        /// </summary>
        private UInt32 ReadInt(byte[] b, ref int offset)
        {
            UInt32 data = (UInt32)BitConverter.ToUInt32(new byte[] { b[offset + 3], b[offset + 2], b[offset + 1], b[offset + 0] }, 0);
            offset += 4;
            return data;
        }

        /// <summary>
        /// Reads an ltp mode tick from raw binary data
        /// </summary>
        private Tick ReadLTP(byte[] b, ref int offset)
        {
            Tick tick = new Tick();
            tick.Mode = "ltp";
            tick.InstrumentToken = ReadInt(b, ref offset);

            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

            tick.Tradable = (tick.InstrumentToken & 0xff) != 9;
            tick.LastPrice = ReadInt(b, ref offset) / divisor;
            return tick;
        }

        /// <summary>
        /// Reads a index's quote mode tick from raw binary data
        /// </summary>
        private Tick ReadIndex(byte[] b, ref int offset)
        {
            Tick tick = new Tick();
            tick.Mode = "quote";
            tick.InstrumentToken = ReadInt(b, ref offset);

            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

            tick.Tradable = (tick.InstrumentToken & 0xff) != 9;
            tick.LastPrice = ReadInt(b, ref offset) / divisor;
            tick.High = ReadInt(b, ref offset) / divisor;
            tick.Low = ReadInt(b, ref offset) / divisor;
            tick.Open = ReadInt(b, ref offset) / divisor;
            tick.Close = ReadInt(b, ref offset) / divisor;
            tick.Change = ReadInt(b, ref offset) / divisor;
            return tick;
        }

        /// <summary>
        /// Reads a quote mode tick from raw binary data
        /// </summary>
        private Tick ReadQuote(byte[] b, ref int offset)
        {
            Tick tick = new Tick();
            tick.Mode = "quote";
            tick.InstrumentToken = ReadInt(b, ref offset);

            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

            tick.Tradable = (tick.InstrumentToken & 0xff) != 9;
            tick.LastPrice = ReadInt(b, ref offset) / divisor;
            tick.LastQuantity = ReadInt(b, ref offset);
            tick.AveragePrice = ReadInt(b, ref offset) / divisor;
            tick.Volume = ReadInt(b, ref offset);
            tick.BuyQuantity = ReadInt(b, ref offset);
            tick.SellQuantity = ReadInt(b, ref offset);
            tick.Open = ReadInt(b, ref offset) / divisor;
            tick.High = ReadInt(b, ref offset) / divisor;
            tick.Low = ReadInt(b, ref offset) / divisor;
            tick.Close = ReadInt(b, ref offset) / divisor;

            return tick;
        }

        /// <summary>
        /// Reads a full mode tick from raw binary data
        /// </summary>
        private Tick ReadFull(byte[] b, ref int offset)
        {
            Tick tick = new Tick();
            tick.Mode = "full";
            tick.InstrumentToken = ReadInt(b, ref offset);

            decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

            tick.Tradable = (tick.InstrumentToken & 0xff) != 9;
            tick.LastPrice = ReadInt(b, ref offset) / divisor;
            tick.LastQuantity = ReadInt(b, ref offset);
            tick.AveragePrice = ReadInt(b, ref offset) / divisor;
            tick.Volume = ReadInt(b, ref offset);
            tick.BuyQuantity = ReadInt(b, ref offset);
            tick.SellQuantity = ReadInt(b, ref offset);
            tick.Open = ReadInt(b, ref offset) / divisor;
            tick.High = ReadInt(b, ref offset) / divisor;
            tick.Low = ReadInt(b, ref offset) / divisor;
            tick.Close = ReadInt(b, ref offset) / divisor;

            tick.Bids = new DepthItem[5];
            for (int i = 0; i < 5; i++)
            {
                tick.Bids[i].Quantity = ReadInt(b, ref offset);
                tick.Bids[i].Price = ReadInt(b, ref offset) / divisor;
                tick.Bids[i].Orders = ReadShort(b, ref offset);
                offset += 2;
            }

            tick.Offers = new DepthItem[5];
            for (int i = 0; i < 5; i++)
            {
                tick.Offers[i].Quantity = ReadInt(b, ref offset);
                tick.Offers[i].Price = ReadInt(b, ref offset) / divisor;
                tick.Offers[i].Orders = ReadShort(b, ref offset);
                offset += 2;
            }
            return tick;
        }

        private void _onData(byte[] Data, int Count, WebSocketMessageType MessageType)
        {
            _timerTick = _interval;
            if (MessageType == WebSocketMessageType.Binary)
            {
                if (Count == 1)
                {                    
                    if (_debug) Console.WriteLine(DateTime.Now.ToLocalTime() + " Heartbeat");
                }
                else
                {
                    int offset = 0;
                    ushort count = ReadShort(Data, ref offset); //number of packets
                    if(_debug) Console.WriteLine("No of packets: " + count);
                    if (_debug) Console.WriteLine("No of bytes: " + Count);

                    for (ushort i = 0; i < count; i++)
                    {                         
                        ushort length = ReadShort(Data, ref offset); // length of the packet
                        if (_debug) Console.WriteLine("Packet Length " + length);
                        Tick tick = new Tick();
                        if (length == 8) // ltp
                            tick = ReadLTP(Data, ref offset);
                        else if (length == 28) // index quote
                            tick = ReadIndex(Data, ref offset);
                        else if (length == 44) // quote
                            tick = ReadQuote(Data, ref offset);
                        else if (length == 164) // marketdepth
                            tick = ReadFull(Data, ref offset);
                        // If the number of bytes got from stream is less that that is required
                        // data is invalid. This will skip that wrong tick
                        if(tick.InstrumentToken != 0 && IsConnected && offset <= Count)
                        {
                            OnTick(tick);
                        }
                    }
                }
            }
            else if(MessageType == WebSocketMessageType.Text)
            {
                if (_debug) Console.WriteLine("WS Message: " + (Encoding.UTF8.GetString(Data.Take(Count).ToArray())));
            }
            else if(MessageType == WebSocketMessageType.Close)
            {
                Close();
            }

        }

        private void _onTimerTick(object sender, System.Timers.ElapsedEventArgs e)
        {
            // For each timer tick count is reduced. If count goes below 0 then reconnection is triggered.
            _timerTick--;
            if (_timerTick < 0)
            {
                _timer.Stop();
                if (_isReconnect)
                    Reconnect();
            }
            if (_debug) Console.WriteLine(_timerTick);
        }
        
        private void _onConnect()
        {
            // Reset timer and retry counts and resubscribe to tokens.
            _retryCount = 0;
            _timerTick = _interval;
            _timer.Start();
            if (_subscribedTokens.Count > 0)
                ReSubscribe();
            OnConnect?.Invoke();
        }

        /// <summary>
        /// Tells whether ticker is connected to server not.
        /// </summary>
        public bool IsConnected
        {
            get { return _ws.IsConnected(); }
        }

        /// <summary>
        /// Start a WebSocket connection
        /// </summary>
        public void Connect()
        {
            if (!IsConnected)
                _ws.Connect();
            _timerTick = _interval;
            _timer.Start();
        }
    
        /// <summary>
        /// Close a WebSocket connection
        /// </summary>
        public void Close()
        {
            _timer.Stop();
            _ws.Close();
        }

        /// <summary>
        /// Reconnect WebSocket connection in case of failures
        /// </summary>
        private void Reconnect()
        {
            if (IsConnected)
                _ws.Close(true);

            if (_retryCount > _retries)
            {
                _ws.Close(true);
                DisableReconnect();
                OnNoReconnect?.Invoke();
            }
            else
            {
                OnReconnect?.Invoke();
                _retryCount += 1;
                _ws.Close(true);
                Connect();
                _timerTick = _interval;
                _timer.Start();
            }
        }

        /// <summary>
        /// Subscribe to a list of instrument_tokens.
        /// </summary>
        /// <param name="Tokens">List of instrument instrument_tokens to subscribe</param>
        public void Subscribe(string[] Tokens)
        {
            string msg = "{\"a\":\"subscribe\",\"v\":[" + String.Join(",", Tokens) + "]}";
            if (_debug) Console.WriteLine(msg);

            if (IsConnected)
                _ws.Send(msg);
            foreach (string token in Tokens)
                if (!_subscribedTokens.ContainsKey(token))
                    _subscribedTokens.Add(token, "quote");
        }

        /// <summary>
        /// Unsubscribe the given list of instrument_tokens.
        /// </summary>
        /// <param name="Tokens">List of instrument instrument_tokens to unsubscribe</param>
        public void UnSubscribe(string[] Tokens)
        {
            string msg = "{\"a\":\"unsubscribe\",\"v\":[" + String.Join(",", Tokens) + "]}";
            if (_debug) Console.WriteLine(msg);

            if (IsConnected)
                _ws.Send(msg);
            foreach (string token in Tokens)
                if (_subscribedTokens.ContainsKey(token))
                    _subscribedTokens.Remove(token);
        }

        /// <summary>
        /// Set streaming mode for the given list of tokens.
        /// </summary>
        /// <param name="Tokens">List of instrument tokens on which the mode should be applied</param>
        /// <param name="Mode">Mode to set. It can be one of the following: ltp, quote, full.</param>
        public void SetMode(string[] Tokens, string Mode)
        {
            string msg = "{\"a\":\"mode\",\"v\":[\"" + Mode + "\", [" + String.Join(",", Tokens) + "]]}";
            if (IsConnected)
                _ws.Send(msg);
            foreach (string token in Tokens)
                if (_subscribedTokens.ContainsKey(token))
                    _subscribedTokens[token] = Mode;
        }

        /// <summary>
        /// Resubscribe to all currently subscribed tokens. Used to restore all the subscribed tokens after successful reconnection.
        /// </summary>
        public void ReSubscribe()
        {
            if (_debug) Console.WriteLine("Resubscribing");
            string[] all_tokens = _subscribedTokens.Keys.ToArray();

            string[] ltp_tokens = all_tokens.Where(key => _subscribedTokens[key] == "ltp").ToArray();
            string[] quote_tokens = all_tokens.Where(key => _subscribedTokens[key] == "quote").ToArray();
            string[] full_tokens = all_tokens.Where(key => _subscribedTokens[key] == "full").ToArray();

            UnSubscribe(all_tokens);
            Subscribe(all_tokens);

            SetMode(ltp_tokens, "ltp");
            SetMode(quote_tokens, "quote");
            SetMode(full_tokens, "full");
        }

        /// <summary>
        /// Enable WebSocket autreconnect in case of network failure/disconnection.
        /// </summary>
        /// <param name="Interval">Interval between auto reconnection attemptes. `onReconnect` callback is triggered when reconnection is attempted.</param>
        /// <param name="Retries">Maximum number reconnection attempts. Defaults to 50 attempts. `onNoReconnect` callback is triggered when number of retries exceeds this value.</param>
        public void EnableReconnect(int Interval = 5, int Retries = 50)
        {
            _isReconnect = true;
            _interval = Interval;
            _retries = Retries;

            _timerTick = _interval;
            if (IsConnected)
                _timer.Start();
        }

        /// <summary>
        /// Disable WebSocket autreconnect.
        /// </summary>
        public void DisableReconnect()
        {
            _isReconnect = false;
            if (IsConnected)
                _timer.Stop();
            _timerTick = _interval;
        }
    }
}
