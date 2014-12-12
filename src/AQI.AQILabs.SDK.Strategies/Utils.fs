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
open AQI.AQILabs.Optimization

/// <summary>
/// Enumeration all Memory types used in the SDK.Strategies namespace
/// </summary>
type public MemoryType =

    // PortfolioStrategy
    | TargetVolatility = 11 // Target volatility value
    | IndividialTargetVolatilityFlag = 2 // 1.0 if TargetVolatility is applied individually to the assets in the portfolio
    | GlobalTargetVolatilityFlag = 3 // 1.0 if TargetVolatility is applied to the entire portfolio

    | ConcentrationFlag = 4 // 1.0 if Concentration risk is to be managed by calculating correlations and aiming to balance exposures to risk factors 
       
    | ExposureFlag = 5 // 1.0 if exposure management is to be implemented
    | ExposureThreshold = 22 // Drawdown from peak at which the position is cut

    | TargetVAR = 6 // Maximum VaR level before the portfolio is deleveraged linearly
    | GlobalTargetVARFlag = 7 // 1.0 if a maximum VaR is to be implemented

    | IndividualMaximumLeverage = 8 // Maximum leverage applied per position
    | GlobalMaximumLeverage = 9 // Maximum leverage applied for entire portfolio sum of all position notional values. Note: Spreads 1 - 1 give 0 exposure. Future are notionally and not marginally accounted for

    | FractionContract = 10 // 1.0 if fractional contracts are allowed. 0.0 is more realistic
    | DaysBack = 1  // Number of days used in the volatility and correlation calculations
    | RebalancingFrequency = 12 // Frequency of rebalancings
    | FixedNotional = 13  // 0.0 if no fixed notional else the notional exposure of the positions will reference this value
    | RebalancingThreshhold = 14 // Minimum pct notional value before an order is submitted for a rebalancing
    

    // ONDepositStrategy
    | Spread = 15 // Spread in pct terms applied to deposit rate accrued in Act/360
    | FundingID = 16 // Instrument ID where the values are stored as 5 --> 5% per annum accrued in Act/360

    // RollingFutureStrategy
    | UnderlyingID = 17 // ID of underlying instrument of futures to be rolled
    | Contract = 18 // Contract number in active chain to roll into
    | RollDay = 19 // Business day of the month to roll the contract
    | Sign = 20 // 1.0 if position exposure to the future -1.0 for a negative exposure


type Matrix = AQI.AQILabs.Kernel.Numerics.Math.LinearAlgebra.DenseMatrix
type Vector = AQI.AQILabs.Kernel.Numerics.Math.LinearAlgebra.DenseVector

/// <summary>
/// Utility module with a set of functions used by all strategies in this namespace
/// </summary>
module Utils =

    /// <summary>
    /// Function: Correlation calculator. Ensure all timeseries have the same length and data frequency.
    /// </summary>
    /// <param name="tlist">List of timeseries for calculation</param>
    let Correlation(tlist : List<TimeSeries>) =
        let length = List.length tlist
        let correlation = new Matrix(length , length)
        [0 .. (length - 1)]
        |> List.iter (fun i -> 
            [0 .. (length - 1)]
            |> List.iter (fun j -> 
                match j with                    
                | x when x = i -> 
                    correlation.[i, j] <- 1.0
                | _ -> 
                    correlation.[i, j] <- AQI.AQILabs.Kernel.Numerics.Math.Statistics.Statistics.Covariance(tlist.[i].Data, tlist.[j].Data) / (tlist.[i].StdDev * tlist.[j].StdDev)
                    correlation.[j, i] <- correlation.[i, j]))
        correlation


    /// <summary>
    /// Function: Constrained MV-Optimize a list of volatility-normalized timeseries.
    /// All timeseries should have a volatility of 1.0. 
    /// Information ratios can be used to affect MV.
    /// If Information ratios are 1.0 then weights are risk-parity weights.
    /// </summary>
    /// <param name="tsl">List of timeseries for calculation</param>
    /// <param name="informationratio">List of respective information ratios</param>
    let Optimize(tsl : List<TimeSeries> , informationratio : List<double>) =
        let tsl_count = List.length tsl
        let optimal_wgts = new Vector(tsl_count, 1.0 / (double)tsl_count)
        if tsl_count <= 2 then
            optimal_wgts
        else        
            try
                let correlation = Correlation tsl
                let lower_bound_wgts = new Vector(tsl_count, 0.1 / (double)tsl_count)
                let upper_bound_wgts = new Vector(tsl_count, 1.0)
                let parameter_wgts = Parameter.getParameters(optimal_wgts)

                let rets = new Vector(tsl_count, 1.0)
                [0 .. tsl_count - 1] |> List.iter (fun i -> rets.[i] <- informationratio.[i])

                let variance_function = System.Func<float>(fun () ->
                    let er = optimal_wgts * rets
                    let vol = sqrt(optimal_wgts * correlation * optimal_wgts)
                    -er / vol)

                let weight_eql_function = System.Func<float>(fun () -> (optimal_wgts.Sum() - 1.0))

                let qnb = new BFGS(variance_function, parameter_wgts, null, [|weight_eql_function|], lower_bound_wgts.Data, upper_bound_wgts.Data)
                qnb.BracketingMethod <- null
                qnb.RegionEliminationMethod <- new IntervalHalvingRegionElimination()

                let tol = 1e-5
                let mutable previousValue = 0.0
                let mutable currentValue = Double.PositiveInfinity
                let mutable counter_opt = 0;
                while (Math.Abs(currentValue - previousValue) > tol && counter_opt <= 5) do                
                    previousValue <- currentValue
                    qnb.Iterate() |> ignore
                    currentValue <- qnb.FunctionValue;
                    counter_opt <- counter_opt + 1                
            with
                | e -> SystemLog.Write(e)
            optimal_wgts


    /// <summary>
    /// Function: Calculate notional adjustments for sub-strategies used when calculating the individual maximum leverage.
    /// Also used when calculating rebalancing threshholds.
    /// This value is the transform for a notional from a parent AUM base to the sub-strategy AUM base.
    /// In parent AUM base of 100M 100% exposure is 200% if the sub-strategy has 50M in AUM.
    /// </summary>
    /// <param name="instrument">sub-strategy</param>
    /// <param name="orderDate">reference date</param>
    /// <param name="reference_aum">aum of parent strategy</param>
    let Notional_Strategy_Adjustment (instrument : Instrument , orderDate : BusinessDay , reference_aum : double) =
        match instrument.InstrumentType with
            | AQI.AQILabs.Kernel.InstrumentType.Strategy ->
                let strategy = (instrument :?> Strategy)
                let strategy_aum =  strategy.GetNextAUM(orderDate.DateTime, TimeSeriesType.Last)
                let counter = Seq.toList (strategy.Portfolio.AggregatedPositionOrders(orderDate.DateTime).Values)
                                |> List.filter (fun position -> 
                                    let sttype = if position.Instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || position.Instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose elif position.Instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close
                                    let sub_ts = position.Instrument.GetTimeSeries(sttype)                                
                                    let idx = if sub_ts = null || sub_ts.Count = 0 then 0 else sub_ts.GetClosestDateIndex(orderDate.DateTime, TimeSeries.DateSearchType.Previous)
                                    (not (strategy.Portfolio.IsReserve(position.Instrument))) && (not (position.Unit = 0.0)) && (idx > 1))
                                |> List.length

                if counter = 0 || strategy_aum = 0.0 then 1.0 else strategy_aum / reference_aum
            | _ -> 1.0
        

    /// <summary>
    /// Function: Generates a map of cash difference timeseries for the sub-strategies and positions in a given strategies portfolio.
    /// If the position is in a strategy, a synthetic timeseries is generated by aggregating the sub-strategies positions and timeseries.
    /// The volatility of these timeseries is an absolute cash volatility
    /// </summary>
    /// <param name="strategy">parent strategy</param>
    /// <param name="orderDate">reference date</param>
    /// <param name="reference_aum">aum of parent strategy</param>
    /// <param name="days_back">number of days used in the timeseries</param>
    let TimeSeriesMap (strategy : Strategy, orderDate: BusinessDay, reference_aum : double, days_back : int) =

        let instruments = strategy.Instruments(DateTime.Today, false)        
        let timeSeriesMapDirty = instruments.Values
                                |> Seq.filter (fun instrument -> // filter out reserve instruments and instruments without timeseries data
                                    let ttype = if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose else if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close
                                    
                                    match instrument with
                                    | x when x.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy && (strategy.Portfolio.IsReserve(instrument)) -> false
                                    | x when x.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy && (not ((x :?> Strategy).Portfolio = null)) ->                                        
                                        Seq.toList ((instrument :?> Strategy).Instruments(orderDate.DateTime, true).Values)
                                        |> List.filter (fun sub_instrument -> 
                                            let sttype = if sub_instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || sub_instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose elif sub_instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close
                                            let sub_ts = sub_instrument.GetTimeSeries(sttype)   
                                                                                                                     
                                            let idx = if sub_ts = null || sub_ts.Count = 0 then 0 else sub_ts.GetClosestDateIndex(orderDate.DateTime, TimeSeries.DateSearchType.Previous)                                            
                                            (not (strategy.Portfolio.IsReserve(sub_instrument))) && (idx > 5)) |> List.length > 0
                                    | _ -> 
                                        let ts_s = instrument.GetTimeSeries(ttype)
                                        let ts_s_count = if not (ts_s = null) then ts_s.Count else 0
                                        let idx_s = if ts_s_count > 0 then ts_s.GetClosestDateIndex(orderDate.DateTime, TimeSeries.DateSearchType.Previous) else 0                                                                
                                        (not (strategy.Portfolio.IsReserve(instrument)) && idx_s > 5))
                        
                                |> Seq.groupBy (fun instrument -> instrument.ID)
                                |> Map.ofSeq
                                |> Map.map (fun id tuple ->                                                 // Generate timeseries
                                    let instrument = Instrument.FindInstrument(id)
                                    
                                    match instrument with
                                    | x when x.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy && (not ((x :?> Strategy).Portfolio = null)) ->
                                        let strategy = x :?> Strategy
                                        let portfolio = strategy.Portfolio
                                        let strategy_aum = strategy.GetNextAUM(orderDate.DateTime, TimeSeriesType.Last)                                        
                                        let positions = portfolio.AggregatedPositionOrders(orderDate.DateTime)
                                        
                                        Seq.toList (strategy.Instruments(orderDate.DateTime, true))                                        
                                        |> List.filter (fun value -> 
                                            let instrument = value.Value
                                                                                        
                                            let sttype = if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose elif instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close
                                            let sub_ts = instrument.GetTimeSeries(sttype)                                
                                            let idx = if sub_ts = null || sub_ts.Count = 0 then 0 else sub_ts.GetClosestDateIndex(orderDate.DateTime, TimeSeries.DateSearchType.Previous)                                            
                                            
                                            (not (portfolio.IsReserve(instrument))) && ((idx > 1)))
                                                                                        
                                        |> List.fold (fun (acc : TimeSeries) value ->
                                            let sub_instrument = value.Value
                                            
                                            let sttype = if sub_instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || sub_instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose elif sub_instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close
                                            let sub_ts_all = sub_instrument.GetTimeSeries(sttype)
                                            let idx = if sub_ts_all = null || sub_ts_all.Count = 0 then 0 else sub_ts_all.GetClosestDateIndex(orderDate.DateTime, TimeSeries.DateSearchType.Previous)
                                            
                                            let sub_ts_range = sub_ts_all.GetRange(Math.Max(1, idx - days_back), idx)
                                            let sub_ts_norm = sub_ts_range / sub_ts_range.[sub_ts_range.Count - 1]

                                            let fx = CurrencyPair.Convert(1.0, orderDate.DateTime, TimeSeriesType.Close, DataProvider.DefaultProvider, TimeSeriesRollType.Last, strategy.Portfolio.Currency, sub_instrument.Currency)
                                            let sub_ts = sub_ts_norm * sub_instrument.[orderDate.DateTime, TimeSeriesType.Last, TimeSeriesRollType.Last] * (if Double.IsNaN(fx) then 1.0 else fx) * (if sub_instrument.InstrumentType = InstrumentType.Future then (sub_instrument :?> Future).PointSize else 1.0)
                                            let sub_ts_diff = sub_ts.DifferenceReturn().ReplaceNaN(0.0) * (if positions.ContainsKey(sub_instrument.ID) then positions.[sub_instrument.ID].Unit else 0.0)
                                            
                                            match acc with
                                            | x when x =  null -> sub_ts_diff
                                            | x when x.Count = sub_ts_diff.Count -> acc + sub_ts_diff
                                            | _ -> acc)  null                                                                                                      
                                    | _ -> 
                                        let ttype = if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose else if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close
                                        let ts_s = instrument.GetTimeSeries(ttype)
                                        let ts_s_count = if not (ts_s = null) then ts_s.Count else 0
                                        let idx_s = if ts_s_count > 0 then ts_s.GetClosestDateIndex(orderDate.DateTime, TimeSeries.DateSearchType.Previous) else 0
                                        let ts_s = instrument.GetTimeSeries(ttype).GetRange(1 , idx_s)

                            
                                        let fx = CurrencyPair.Convert(1.0, orderDate.DateTime, TimeSeriesType.Close, DataProvider.DefaultProvider, TimeSeriesRollType.Last, strategy.Portfolio.Currency, instrument.Currency)
                                        let normalized_ts = (reference_aum * (if Double.IsNaN(fx) then 1.0 else fx) * ts_s.GetRange(Math.Max(0 , ts_s.Count - 1 - days_back) , ts_s.Count - 1) / ts_s.[ts_s.Count - 1])
                            
                                        normalized_ts.DifferenceReturn().ReplaceNaN(0.0))
                                |> Map.filter (fun id tuple -> not (tuple = null))
        
        let firstSeries = if timeSeriesMapDirty.Count = 0 then null else timeSeriesMapDirty |> Map.toList |> List.map (fun (k,v) -> v) |> List.sortBy (fun (v) -> -v.Count) |> List.head
        let timeSeriesMap = timeSeriesMapDirty
                            |> Map.map (fun k timeseries ->
                                let mutable strategy_ts = timeseries
                                if not (strategy_ts = null) then                        
                                    if not (timeSeriesMapDirty.Count = 0) then                                                                    
                                        let tss = ref (new TimeSeries(firstSeries.Count, firstSeries.DateTimes))
                                        [0 .. firstSeries.Count - 1] |> List.iter (fun i -> (!tss).[firstSeries.DateTimes.[i]] <- timeseries.[firstSeries.DateTimes.[i], TimeSeries.DateSearchType.Previous])
                                        strategy_ts <- (!tss).ReplaceNaN(0.0)
                                strategy_ts)
        
        timeSeriesMap