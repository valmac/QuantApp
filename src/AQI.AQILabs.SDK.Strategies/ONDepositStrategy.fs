(*
 * Portions Copyright (c) 2011-2013 AQI Capital Advisors Limited.  All Rights Reserved.
 * This file contains Original Code and/or Modifications of Original Code as defined in and that are subject to the AQI Public Source License Version 1.0 (the 'License').
 * You may not use this file except in compliance with the License.  Please obtain a copy of the License at http://www.aqicapital.com/home/open/ and read it before using this file.
 * The Original Code and all software distributed under the License are distributed on an 'AS IS' basis, WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, AND AQI HEREBY DISCLAIMS ALL SUCH WARRANTIES,
 * INCLUDING WITHOUT LIMITATION, ANY WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, QUIET ENJOYMENT OR NON-INFRINGEMENT.
 * Please see the License for the specific language governing rights and limitations under the License.
 *)

namespace AQI.AQILabs.SDK.Strategies

open System
open AQI.AQILabs.Kernel
open AQI.AQILabs.Kernel.Numerics.Util

/// <summary>
/// Class representing a strategy that grows according to a spread or with a funding rate like cash.
/// A zero spread and no funding instrument is a constant NAV strategy.
/// The spread and funding rate are calculated through Act/360 and the funding rate is quoted as 5.0 for 5% per annum.
/// </summary>
type ONDepositStrategy = 
    inherit Strategy    
       
    /// <summary>
    /// Constructor
    /// </summary> 
    new(instrument : Instrument) = { inherit Strategy(instrument); }
    
    /// <summary>
    /// Constructor
    /// </summary> 
    new(id : int) = { inherit Strategy(id); }
    
    /// <summary>
    /// Function: returns a list of names of used memory types.
    /// </summary>     
    override this.MemoryTypeNames() : string[] = System.Enum.GetNames(typeof<MemoryType>)

    /// <summary>
    /// Function: returns a list of ids of used memory types.
    /// </summary>     
    override this.MemoryTypeInt(name : string) = System.Enum.Parse(typeof<MemoryType> , name) :?> int

    /// <summary>
    /// Function: Calculate the NAV according to an Act/360 accrual based on a spread and funding rate.
    /// </summary>
    override this.NAVCalculation(date : BusinessDay) =        
        let previousDate = date.AddMilliseconds(-1)

        let value = this.[previousDate.DateTime, TimeSeriesType.Last, TimeSeriesRollType.Last]        
        let value = if Double.IsNaN(value) then this.[date.DateTime, TimeSeriesType.Last, TimeSeriesRollType.Last] else value

        let spread = this.[date.DateTime, (int)MemoryType.Spread, TimeSeriesRollType.Last]
        let fundingID = this.[date.DateTime, (int)MemoryType.FundingID, TimeSeriesRollType.Last]
        let rate = if Double.IsNaN(fundingID) then 0.0 else Instrument.FindInstrument((int)fundingID).[date.DateTime, TimeSeriesType.Last]
        let newValue = value * (1.0 + (spread + rate / 100.0) * (date.DateTime - previousDate.DateTime).TotalDays / 360.0)

        this.CommitNAVCalculation(date, newValue, TimeSeriesType.Last)
        newValue
           
     /// <summary>
    /// Function: Create a Strategy and run startup procedures
    /// </summary>
    /// <param name="instrument">base instrument</param>
    /// <param name="initialDate">starting date</param>
    /// <param name="initialValue">starting NAV</param>
    /// <param name="spread">reference spread</param>
    /// <param name="funding">instrument quoting the funding rate 5 equals 5pct per annum</param>
    static member public CreateStrategy(instrument : Instrument, initialDate : BusinessDay, initialValue : double, spread : double, funding : Instrument) =    
        match instrument with
        | x when x.InstrumentType = InstrumentType.Strategy ->
            let Strategy = new ONDepositStrategy(instrument)
            Strategy.Startup(initialDate, initialValue, null)
            
            Strategy.AddMemoryPoint(DateTime.MinValue, spread, (int)MemoryType.Spread)
            
            if not (funding = null) then
                Strategy.AddMemoryPoint(initialDate.DateTime, (double)funding.ID, (int)MemoryType.FundingID)
                                            
            Strategy
        | _ -> raise (new Exception("Instrument not a Strategy"))        