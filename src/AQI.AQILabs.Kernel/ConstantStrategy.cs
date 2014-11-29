/*
 * Portions Copyright (c) 2011-2013 AQI Capital Advisors Limited.  All Rights Reserved.
 * This file contains Original Code and/or Modifications of Original Code as defined in and that are subject to the AQI Public Source License Version 1.0 (the 'License').
 * You may not use this file except in compliance with the License.  Please obtain a copy of the License at http://www.aqicapital.com/home/open/ and read it before using this file.
 * The Original Code and all software distributed under the License are distributed on an 'AS IS' basis, WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, AND AQI HEREBY DISCLAIMS ALL SUCH WARRANTIES,
 * INCLUDING WITHOUT LIMITATION, ANY WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, QUIET ENJOYMENT OR NON-INFRINGEMENT.
 * Please see the License for the specific language governing rights and limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AQI.AQILabs.Kernel
{
    /// <summary>
    /// Class for a strategy that always has the same NAV.
    /// Can be used for a cash proxy with zero rate.
    /// </summary>
    public class ConstantStrategy : Strategy
    {

        /// <summary>
        /// Constructor
        /// </summary>
        public ConstantStrategy(Instrument instrument)
            : base(instrument) { }

        /// <summary>
        /// Constructor
        /// </summary>
        public ConstantStrategy(int id)
            : base(id) { }

        /// <summary>
        /// Function: Calculate the NAV which is taken as the previous value in order to keep constant.
        /// </summary>
        public override double NAVCalculation(BusinessDay date)
        {
            BusinessDay previousDate = date.AddMilliseconds(-1);

            double val = this[previousDate.DateTime, TimeSeriesType.Last, TimeSeriesRollType.Last];            
            CommitNAVCalculation(date, val, TimeSeriesType.Last);

            return val;
        }

        /// <summary>
        /// Function: Create a Strategy and run startup procedures
        /// </summary>
        /// <param name="instrument">base instrument</param>
        /// <param name="initialDate">starting date</param>
        /// <param name="initialValue">starting NAV</param>
        public static ConstantStrategy CreateStrategy(Instrument instrument, BusinessDay initialDate, double initialValue)
        {
            if (instrument.InstrumentType == InstrumentType.Strategy)
            {
                ConstantStrategy Strategy = new ConstantStrategy(instrument);
                Strategy.Startup(initialDate, initialValue, null);

                return Strategy;
            }
            else
                throw new Exception("Instrument not an Strategy");
        }

        /// <summary>
        /// Function: Create a Strategy and run startup procedures
        /// </summary>
        /// <param name="name">name</param>
        /// <param name="ccy">base currency</param>
        /// <param name="initialDate">starting date</param>
        /// <param name="initialValue">starting NAV</param>
        /// <param name="simulated">false if persistent</param>
        new public static ConstantStrategy CreateStrategy(string name, Currency ccy, BusinessDay initialDate, double initialValue, bool simulated)
        {
            Instrument instrument = Instrument.CreateInstrument(name, InstrumentType.Strategy, name + "ConstantStrategy", ccy, FundingType.TotalReturn, simulated);
            ConstantStrategy strategy = ConstantStrategy.CreateStrategy(instrument, initialDate, 1);
            strategy.SetConstantCarryCost(0.0 / (double)10000.0, 0.0 / (double)10000.0, DayCountConvention.Act360, 360);
            strategy.TimeSeriesRoll = TimeSeriesRollType.Last;
            strategy.Initialize();

            return strategy;
        }
    }
}
