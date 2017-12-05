using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FuturesScanner.DataObjects
{
    public class Subscriptions
    {
        public string Symbol { get; set; }
        public string Company { get; set; }
        public string InstrumentCode { get; set; }

        public decimal OrderPrice { get; set; }
        public decimal TargetPrice { get; set; }

        public decimal StopLoss { get; set; }

        public int LotSize { get; set; }
    }
}
