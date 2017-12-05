using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FuturesScanner.DataObjects
{
   public enum TradeType {Sell=0,Buy=1  }
    public class Signal
    {
        public decimal LastTradePrice { get; set; }
        public TradeType signal { get; set; }
        public string script { get; set; }

        public decimal TargetPrice { get; set; }
        public decimal OrderPrice { get; set; }

        public decimal StopLoss { get; set; }
        public string instrumentcode { get; set; }


        public int LotSize { get; set; }


        public decimal MaxProfit { get; set; }

        public decimal MaxLoss { get; set; }

        public string ProfitToLossRatio { get; set; }

        public decimal PointsToTarget { get; set; }

        public decimal Percent3TargetProfit {get;set;}

        public decimal Percent3TargetPL { get; set; }

        public decimal SLPoints { get; set; }




    }
}
