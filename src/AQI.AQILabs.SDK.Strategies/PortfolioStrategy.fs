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
/// Delegate function called by this strategy during the logic execution in order to decide
/// if the portfolio's should have exposure to a specific instrument.
/// </summary>
type Exposure = delegate of Strategy * BusinessDay * Instrument -> double

/// <summary>
/// Delegate function called by this strategy during the logic execution in order to measure
/// the risk of a specific timeseries used in the risk targeting process
/// </summary>
type Risk = delegate of Strategy * BusinessDay * TimeSeries * double -> double

/// <summary>
/// Delegate function called by this strategy during the logic execution in order to measure
/// the information ratio of a specific instrument used in the optimisation process
/// </summary>
type InformationRatio = delegate of Strategy * BusinessDay * Instrument -> double

/// <summary>
/// Class representing a strategy that optimises a portfolio in a multistep process:
/// 1) Target volatility for each underlying asset. This step sets an equal volatility weight to all assets.
/// 2) Concentration risk and Information Ratio management. This step mitigates the risk of an over exposure to a set of highly correlated assets. 
///    Furthermore, a tilt in the weight is also implemented based on the information ratios for each asset.
///    This entire step is achieved through the implementation of a Mean-Variance optimisation where all volatilities are equal to 1.0, the expected returns are normalised and transformed to information ratios.
/// 3) Target volatility for the entire portfolio. After steps 1 and 2, the portfolio will probably have a lower risk level than the target due to diversification.
///    This step adjusts the strategy's overall exposure in order to achieve the desired target volatility for the entire portfolio.
/// 4) Maximum individual exposure to each asset is implemented
/// 5) Maximum exposure to the entire portfolio is implemented
/// 6) Deleverage the portfolio if the portfolio's Value at Risk exceeds a given level. The exposure is changed linearly such that the new VaR given by the new weights is the limit VaR.
///    The implemented VaR measure is the UCITS calculation based on the 99 percentile of the distribution of the 20 day rolling return for the entire portfolio where each return is based on the current weights.
/// 7) Only rebalance if the notional exposure to a position changes by more than a given threshhold.
/// The class also allows developers to specify a number of custom functions:
///     a) Risk: risk measure for each asset.
///     b) Exposure: defines if the portfolio should have a long (1.0) / short (-1.0) or neutral (0.0) exposure to a given asset.
///     c) InformationRatio: measure of risk-neutral expectation for each asset. This affectes the MV optimisation of the concentration risk management.
/// </summary>
type PortfolioStrategy = 
    inherit Strategy    
        
    val mutable _exposureFunction : Exposure    
    val mutable _riskFunction : Risk
    val mutable _informationRatioFunction : InformationRatio

    /// <summary>
    /// Constructor
    /// </summary> 
    new(instrument : Instrument) = 
                                    { 
                                        inherit Strategy(instrument); 
                                        _exposureFunction = Exposure(fun this orderDate instrument -> PortfolioStrategy.ExposureDefault(this :?> PortfolioStrategy, orderDate, instrument));
                                        _riskFunction = Risk(fun this orderDate timeSeries reference_aum -> PortfolioStrategy.RiskDefault(this :?> PortfolioStrategy, orderDate, timeSeries, reference_aum));                                        
                                        _informationRatioFunction = InformationRatio(fun this orderDate instrument -> PortfolioStrategy.InformationRatioDefault(this :?> PortfolioStrategy, orderDate, instrument)) 
                                    }

    /// <summary>
    /// Constructor
    /// </summary> 
    new(instrument : Instrument, className : string) = 
                                    { 
                                        inherit Strategy(instrument, className); 
                                        _exposureFunction = Exposure(fun this orderDate instrument -> PortfolioStrategy.ExposureDefault(this :?> PortfolioStrategy, orderDate, instrument)); 
                                        _riskFunction = Risk(fun this orderDate timeSeries reference_aum -> PortfolioStrategy.RiskDefault(this :?> PortfolioStrategy, orderDate, timeSeries, reference_aum));                                        
                                        _informationRatioFunction = InformationRatio(fun this orderDate instrument -> PortfolioStrategy.InformationRatioDefault(this :?> PortfolioStrategy, orderDate, instrument)) 
                                    }

    /// <summary>
    /// Constructor
    /// </summary> 
    new(id : int) = 
                    { 
                        inherit Strategy(id); 
                        _exposureFunction = Exposure(fun this orderDate instrument -> PortfolioStrategy.ExposureDefault(this :?> PortfolioStrategy, orderDate, instrument));
                        _riskFunction = Risk(fun this orderDate timeSeries reference_aum -> PortfolioStrategy.RiskDefault(this :?> PortfolioStrategy, orderDate, timeSeries, reference_aum));                        
                        _informationRatioFunction = InformationRatio(fun this orderDate instrument -> PortfolioStrategy.InformationRatioDefault(this :?> PortfolioStrategy, orderDate, instrument)) 
                    }
        
    /// <summary>
    /// Function: returns a list of names of used memory types.
    /// </summary>  
    override this.MemoryTypeNames() :  string[] = System.Enum.GetNames(typeof<MemoryType>)

    /// <summary>
    /// Function: returns a list of ids of used memory types.
    /// </summary>
    override this.MemoryTypeInt(name : string) = System.Enum.Parse(typeof<MemoryType> , name) :?> int

    /// <summary>
    /// Function: Initialize the strategy during runtime.
    /// </summary>
    override this.Initialize() =        
        match this.Initialized with
        | true -> ()
        | _ -> 
            let FractionContract = this.[DateTime.Now, (int)MemoryType.FractionContract, TimeSeriesRollType.Last]
            
            if (FractionContract = 0.0) then
                this.Portfolio.OrderUnitCalculation <- Portfolio.OrderUnitCalculationEvent (fun instrument unit -> 
                    match instrument.InstrumentType with
                    | InstrumentType.Strategy -> unit
                    | _ -> unit |> round)

            base.Initialize()
           

    /// <summary>
    /// Function: returns the high water mark which is used by the default exposure and information ratio functions.    
    /// </summary>
    member this.HighLowMark(instrument : Instrument, orderDate : BusinessDay) = 
        let ts_s = match instrument with
                    | x when x.GetType() = (typeof<PortfolioStrategy>) ->
                        let strategy = instrument :?> PortfolioStrategy
                        let instruments = strategy.Instruments(orderDate.DateTime, false)
                        if instruments.Count = 1 && not((Seq.head instruments.Values).InstrumentType = InstrumentType.Strategy) then
                            let ins = (Seq.head instruments.Values)
                            let ttype = if ins.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || ins.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose else if ins.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close
                            ins.GetTimeSeries(ttype)
                        else
                            strategy.GetTimeSeries(TimeSeriesType.Last)                
                    | _ -> 
                        let ttype = if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose else if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close
                        instrument.GetTimeSeries(ttype)
                
        let ts_s_count = if not (ts_s = null) then ts_s.Count else 0
        let idx_s = if ts_s_count > 0 then ts_s.GetClosestDateIndex(orderDate.DateTime, TimeSeries.DateSearchType.Previous) else 0
        let ts_s = ts_s.GetRange(1 , idx_s)
        let instrument_t_1 = if idx_s = 0 then 0.0 else ts_s.[Math.Max(0, ts_s.Count - 2)]
        let instrument_t = if idx_s = 0 then 0.0 else ts_s.[Math.Max(0, ts_s.Count - 1)]
        let hwm = if ts_s.Count = 0 then instrument_t_1 else ts_s.Maximum

        let lwm =                                 
                let mutable i = ts_s.Count - 1
                if i > 0 then
                    let mutable v = ts_s.[i]
                    while i - 1 >= 0 && not (ts_s.[i] = hwm) do
                        i <- i - 1
                        let v_i = ts_s.[i]
                        if v_i < v then
                            v <- v_i
                    v    
                else
                    hwm
                        
        (hwm, lwm , instrument_t, instrument_t_1, ts_s)


    /// <summary>
    /// Function: called by this strategy during the logic execution in order to
    /// measuring the information ratio of a specific asset.    
    /// </summary>
    member this.InformationRatio(orderDate : BusinessDay, instrument : Instrument) = 
        this.InformationRatioFunction.Invoke(this, orderDate, instrument)           

    /// <summary>
    /// Delegate function used to set the information ratio function
    /// </summary>
    member this.InformationRatioFunction
        with get() : InformationRatio = this._informationRatioFunction
        and set(value) = this._informationRatioFunction <- value

    /// <summary>
    /// Function: default exposure measure implemented as the information ratio.
    /// The information ratio is set as 1.0 - distance from the asset's high water mark to penalise asset's that are far from their highest point and have not yet started recovering.
    /// </summary>
    static member InformationRatioDefault(this : PortfolioStrategy, orderDate : BusinessDay, instrument : Instrument) = 
        let (hwm, lwm, instrument_t, instrument_t_1, ts_s) = this.HighLowMark(instrument, orderDate)
        let days_back = (int)this.[orderDate.DateTime, (int)MemoryType.DaysBack, TimeSeriesRollType.Last]
                
        let maxdd = Math.Min(instrument_t / hwm - 1.0, 0.0)
        let recovery = instrument_t / lwm - 1.0
        (1.0 + maxdd + recovery)


    /// <summary>
    /// Function: called by this strategy during the logic execution in order to
    /// measuring the risk of a specific asset.
    /// The risk measure is quoted in cash terms.
    /// </summary>
    member this.Risk(orderDate : BusinessDay, timeSeries : TimeSeries , reference_aum : double) = 
        this.RiskFunction.Invoke(this, orderDate, timeSeries, reference_aum)    

    /// <summary>
    /// Delegate function used to set the risk measure function
    /// </summary>
    member this.RiskFunction
        with get() : Risk = this._riskFunction
        and set(value) = this._riskFunction <- value

    /// <summary>
    /// Function: default risk measure defined as the realised quadratic variation * Sqrt(252) in cash terms
    /// </summary>
    static member RiskDefault(this : PortfolioStrategy, orderDate : BusinessDay, timeSeries : TimeSeries , reference_aum : double) =        
        timeSeries.QuadraticVariation * Math.Sqrt(252.0) / reference_aum
    

    /// <summary>
    /// Function: called by this strategy during the logic execution in order to
    /// define the exposure (1.0, 0.0 or -1.0) to a given instrument.
    /// </summary>
    member this.ExposureFunction
        with get() : Exposure = this._exposureFunction
        and set(value) = this._exposureFunction <- value

    /// <summary>
    /// Delegate function used to set the exposure function
    /// </summary>
    member this.Exposure(orderDate : BusinessDay, instrument : Instrument) = 
        this.ExposureFunction.Invoke(this, orderDate, instrument)    

    /// <summary>
    /// Function: default exposure measure implemented as a stop-loss mechanism.
    /// The stop-loss is implemented if the asset is below a certain threshhold from the previous high-watermark.
    /// The threshhold is the difference between the High water mark and a volatility scaled level. The effect is that the
    /// stop-loss changes with the level of volatility making it more adaptive and less prone to locking in losses.
    /// </summary>
    static member ExposureDefault(this : PortfolioStrategy, orderDate : BusinessDay, instrument : Instrument) =        
        let (hwm, lwm, instrument_t, instrument_t_1, ts_s) = this.HighLowMark(instrument, orderDate)
        let days_back = (int)this.[orderDate.DateTime, (int)MemoryType.DaysBack, TimeSeriesRollType.Last]
        let exp_threshhold = this.[orderDate.DateTime, (int)MemoryType.ExposureThreshold, TimeSeriesRollType.Last]
        let ttype = if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose elif instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close
        let ts = ts_s.GetRange(Math.Max(0,ts_s.Count - 1 - days_back) , Math.Max(0,ts_s.Count - 1)).LogReturn().ReplaceNaN(0.0)
        let vol = ts.StdDev * Math.Sqrt(252.0)
               
        (if (hwm * (1.0 - vol * exp_threshhold)  > instrument_t_1) then (if (lwm * (1.0 + vol)  < instrument_t_1) then 1.0 else 0.0)  else 1.0)


    /// <summary>
    /// <summary>
    /// Function: Logic for the optimisation process.
    /// 1) Target volatility for each underlying asset. This step sets an equal volatility weight to all assets.
    /// 2) Concentration risk and Information Ratio management. This step mitigates the risk of an over exposure to a set of highly correlated assets. 
    ///    Furthermore, a tilt in the weight is also implemented based on the information ratios for each asset.
    ///    This entire step is achieved through the implementation of a Mean-Variance optimisation where all volatilities are equal to 1.0, the expected returns are normalised and transformed to information ratios.
    /// 3) Target volatility for the entire portfolio. After steps 1 and 2, the portfolio will probably have a lower risk level than the target due to diversification.
    ///    This step adjusts the strategy's overall exposure in order to achieve the desired target volatility for the entire portfolio.
    /// 4) Maximum individual exposure to each asset is implemented
    /// 5) Maximum exposure to the entire portfolio is implemented
    /// 6) Deleverage the portfolio if the portfolio's Value at Risk exceeds a given level. The exposure is changed linearly such that the new VaR given by the new weights is the limit VaR.
    ///    The implemented VaR measure is the UCITS calculation based on the 99 percentile of the distribution of the 20 day rolling return for the entire portfolio where each return is based on the current weights.
    /// 7) Only rebalance if the notional exposure to a position changes by more than a given threshhold.
    /// The class also allows developers to specify a number of custom functions:
    ///     a) Risk: risk measure for each asset.
    ///     b) Exposure: defines if the portfolio should have a long (1.0) / short (-1.0) or neutral (0.0) exposure to a given asset.
    ///     c) InformationRatio: measure of risk-neutral expectation for each asset. This affectes the MV optimisation of the concentration risk management.    
    /// </summary> 
    /// <param name="ctx">Context containing relevant environment information for the logic execution
    /// </param>           
    override this.ExecuteLogic(ctx : ExecutionContext) =
        let master_calendar = this.Calendar        
        let orderDate = ctx.OrderDate
        let executionDate = orderDate

        let FixedNotional = this.[orderDate.DateTime, (int)MemoryType.FixedNotional, TimeSeriesRollType.Last]
        let reference_aum = if ((not (Double.IsNaN(FixedNotional))) && FixedNotional > 0.0) then FixedNotional else (ctx.ReferenceAUM)
        
        match ctx.ReferenceAUM with
        | 0.0 -> () // Stop calculations
        | _ ->      // Run calculations
            let threshhold_rounding = 5
            
            let TargetVolatility = this.[orderDate.DateTime, (int)MemoryType.TargetVolatility, TimeSeriesRollType.Last]
            let IndividualVolatilityTargetFlag = (int)this.[orderDate.DateTime, (int)MemoryType.IndividialTargetVolatilityFlag, TimeSeriesRollType.Last]
            let GlobalVolatilityTargetFlag = (int)this.[orderDate.DateTime, (int)MemoryType.GlobalTargetVolatilityFlag, TimeSeriesRollType.Last]
            let ConcetrationFlag = (int)this.[orderDate.DateTime, (int)MemoryType.ConcentrationFlag, TimeSeriesRollType.Last]
            let ExposureFlag = (int)this.[orderDate.DateTime, (int)MemoryType.ExposureFlag, TimeSeriesRollType.Last]
            let rebalancing = (int)this.[orderDate.DateTime, (int)MemoryType.RebalancingFrequency, TimeSeriesRollType.Last]
            let days_back = (int)this.[orderDate.DateTime, (int)MemoryType.DaysBack, TimeSeriesRollType.Last]
            let instruments = this.Instruments(DateTime.Today, false)

            
            match (days_back = Int32.MinValue || Double.IsNaN(TargetVolatility) || TargetVolatility = 0.0) with
            | true -> // Create positions if they don't exist because there is not logic to run
                let timeSeriesMap = Utils.TimeSeriesMap(this, orderDate, reference_aum, days_back)
                Seq.toList instruments.Values
                |> List.filter (fun instrument -> timeSeriesMap.ContainsKey(instrument.ID))                                     // Filter out reserve instruments and instruments without timeseries data
                                        
                |> List.iter (fun instrument ->
                    let position = this.Portfolio.FindPosition(instrument, orderDate.DateTime)
                    let size = if not (instrument.InstrumentType = InstrumentType.Strategy) then reference_aum / instrument.[orderDate.DateTime, TimeSeriesType.Last, TimeSeriesRollType.Last] else reference_aum
                    let itype = if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose else TimeSeriesType.Last

                    if not(Double.IsNaN(size)) then
                        if ExposureFlag = 1 then
                            if instruments.Count = 1 then          
                                let instrument = instruments.Values |> Seq.head
                                if timeSeriesMap.ContainsKey(instrument.ID) && (timeSeriesMap.[instrument.ID].Count >= 5) then
                                    let timeSeries = timeSeriesMap.[instrument.ID]                                    
                                    let exposureWeight = this.Exposure(orderDate, instrument)
                                    if not (position = null) && exposureWeight = 0.0 then
                                        this.Portfolio.CreateTargetMarketOrder(instrument, orderDate.DateTime, 0.0) |> ignore
                                    elif position = null then
                                        this.Portfolio.CreateTargetMarketOrder(instrument, orderDate.DateTime, Math.Abs(size) * exposureWeight) |> ignore
                                            
                            elif position = null then
                                this.Portfolio.CreateTargetMarketOrder(instrument, orderDate.DateTime, size) |> ignore

                        elif position = null then
                            this.Portfolio.CreateTargetMarketOrder(instrument, orderDate.DateTime, size) |> ignore)

            | _ -> // Run logic
                let rebalanceDayCheck = match rebalancing with
                                        | 0 -> true //Every day
                                        | -1 -> executionDate.DateTime.DayOfWeek > executionDate.AddBusinessDays(1).DateTime.DayOfWeek //Last day of the week
                                        | x when (x > 0 && x < 32) -> executionDate.DayMonth = rebalancing //x Business Day of the month
                                        | 32 -> executionDate.DayMonth > executionDate.AddBusinessDays(1).DayMonth //Last day of the month
                                        | 33 -> executionDate.DayMonth > executionDate.AddBusinessDays(1).DayMonth && (executionDate.DateTime.Month = 1 || executionDate.DateTime.Month = 3 || executionDate.DateTime.Month = 6 || executionDate.DateTime.Month = 9 || executionDate.DateTime.Month = 12)
                                        | 34 -> executionDate.DayMonth > executionDate.AddBusinessDays(1).DayMonth && executionDate.DateTime.Month = 12 // Last day of the year
                                        | _ -> false

                
                let riskPositions = this.Portfolio.RiskPositions(orderDate.DateTime, true)
                let openOrders = this.Portfolio.OpenOrders(orderDate.DateTime, true)                
                let rebalance = if (not (openOrders = null) && not (openOrders.Count = 0)) then true else rebalanceDayCheck
                
                let weightMap = if rebalance then                    
                                    let max_ind_levarge = this.[orderDate.DateTime, (int)MemoryType.IndividualMaximumLeverage, TimeSeriesRollType.Last]
                                    let timeSeriesMap = Utils.TimeSeriesMap(this, orderDate, reference_aum, days_back)                                    
                                    instruments.Values
                                    |> Seq.filter (fun instrument -> timeSeriesMap.ContainsKey(instrument.ID))                  // Filter out reserve instruments and instruments without timeseries data                                      
                                    |> Seq.groupBy (fun instrument -> instrument.ID)
                                    |> Map.ofSeq
                                    |> Map.map (fun id tuple -> 1.0 / (double) instruments.Count)                               // Individual Equal Weight Notional Weights
                                    |> Map.map (fun id oldWeight ->                                                             // Neutral Individual Volatility Weight (Step 1)
                                        let instrument = Instrument.FindInstrument(id)
                                        let strategy_ts = if timeSeriesMap.ContainsKey(id) then timeSeriesMap.[id] else null                        
                                        let dp_notional = match instrument with
                                                            | x when x.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy && (not ((x :?> Strategy).Portfolio = null)) ->
                                                                let strategy = x :?> Strategy                                                                
                                                                let strategy_aum = strategy.GetNextAUM(orderDate.DateTime, TimeSeriesType.Last)                                                                
                                                                let vol = if strategy_aum = 0.0 || strategy_ts = null then 0.0 elif strategy_ts.Count < 5 then TargetVolatility else this.Risk(orderDate, strategy_ts, strategy_aum)                                                                
                                                                let notional_allocation = if strategy_ts = null then 1.0 elif strategy_aum = 0.0 then 0.0 else reference_aum / strategy_aum
                                                                let dp_unfiltered = if vol < 1e-5 || IndividualVolatilityTargetFlag = 0 then 1.0 else TargetVolatility / vol                                                                
                                                                let dp = if dp_unfiltered < 1e-5 then 0.0 else dp_unfiltered
                                                                dp * (if notional_allocation = 0.0 then 1.0 / (double)instruments.Count else notional_allocation)                                                        
                                                            | _ -> 
                                                                let vol = if strategy_ts = null || strategy_ts.Count < 5 then TargetVolatility else this.Risk(orderDate, strategy_ts, reference_aum)                                                                                                                                
                                                                let exposureWeight = if ExposureFlag = 1 then this.Exposure(orderDate, instrument) else 1.0                                                                
                                                                let dp = if vol < 1e-5 || IndividualVolatilityTargetFlag = 0 then 1.0 else TargetVolatility / vol
                                                                dp * exposureWeight    
                                        
                                        dp_notional)
                                    |> (fun weightMap ->                                                                        // Neutral Covariance Weight (Step 2)
                                        if weightMap.Count = 0 then
                                            weightMap
                                        else
                                            let weights = weightMap |> Map.toList |> List.map (fun (k,v) -> v)                                            
                                            let ids = weightMap |> Map.toList |> List.map (fun (k,v) -> k)
                                            let optimizationTuple = weightMap
                                                                    |> Map.map (fun id weight ->
                                                                        let strategy_ts = timeSeriesMap.[id]
                                                                        let instrument = Instrument.FindInstrument(id)
                                                                        let ret = this.InformationRatio(orderDate, instrument)
                                                                        (ret , if strategy_ts = null then null else strategy_ts * weight))

                                            let (returns, weightTimeSeries) = (optimizationTuple |> Map.toList |> List.map (fun (k,v) -> fst v) , optimizationTuple |> Map.toList |> List.map (fun (k,v) -> snd v))                                            
                                            let optimal_wgts = if ConcetrationFlag = 1 then Utils.Optimize(weightTimeSeries, returns) else new Vector(weightMap.Count, 1.0 / (double)weightMap.Count)                                            
                                            let newWeights = optimal_wgts |> Seq.toList |> List.mapi(fun i w -> w * weights.[i])                                            
                                            let newWeightMap = ids |> List.mapi (fun i element -> (element, newWeights.[i]))
                                            newWeightMap |> Map.ofList)

                                    |> (fun weightMap ->                                                                        // Neutral Portfolio Volatility Weight (Step 3)
                                        if weightMap.Count = 0 then
                                            weightMap
                                        else
                                            let weights = weightMap |> Map.toList |> List.map (fun (k,v) -> v)                                                                                       
                                            let ids = weightMap |> Map.toList |> List.map (fun (k,v) -> k)
                                            let aggregatedTimeSeries = weightMap |> Map.toList |> List.map (fun (k,v) -> (v, timeSeriesMap.[k])) |> List.fold (fun acc element -> if acc = null then ((fst element) * (snd element)) else acc + ((fst element) * (snd element))) null
                                            let portfolio_vol = if aggregatedTimeSeries = null then TargetVolatility else this.Risk(orderDate, aggregatedTimeSeries, reference_aum)
                                            let dpp = if portfolio_vol < 1e-5 || GlobalVolatilityTargetFlag = 0 then 1.0 else TargetVolatility / portfolio_vol
                                            let newWeights = weights |> List.mapi(fun i w -> w * dpp)                                            
                                            let newWeightMap = ids |> List.mapi (fun i element -> (element, newWeights.[i]))
                                            newWeightMap |> Map.ofList)

                                    |> Map.map (fun id weight ->                                                                // Individually Capped Weights (Step 4)                                  
                                        let instrument = Instrument.FindInstrument(id)
                                        let notional_strategy_adjustment = Utils.Notional_Strategy_Adjustment(instrument, orderDate, reference_aum)
                                        (if Double.IsNaN(TargetVolatility) then 1.0 else Math.Min(max_ind_levarge, weight * notional_strategy_adjustment) / notional_strategy_adjustment))

                                    |> (fun weightMap ->                                                                        // Global Leverage Constrained Weight (Step 5)
                                        if weightMap.Count = 0 then
                                            weightMap
                                        else
                                            let weights = weightMap |> Map.toList |> List.map (fun (k,v) -> v)                                            
                                            let ids = weightMap |> Map.toList |> List.map (fun (k,v) -> k)
                                            let sum_value = weightMap
                                                            |> Map.fold (fun acc id weight ->
                                                                let instrument = Instrument.FindInstrument(id)
                                                                let ttype = if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose elif instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close
                        
                                                                match instrument with
                                                                | x when x.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy && (not ((x :?> Strategy).Portfolio = null)) ->
                                                                    let strategy = x :?> Strategy
                                                                    let cc = Seq.toList (strategy.Portfolio.AggregatedPositionOrders(orderDate.DateTime).Values)
                                                                            |> List.filter (fun order -> not (strategy.Portfolio.IsReserve(order.Instrument)))
                                                                            |> List.fold (fun bcc order ->
                                                                                let ttype_sub = if order.Instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || order.Instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose elif order.Instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close
                                                                                bcc + weight * CurrencyPair.Convert(order.Instrument.[orderDate.DateTime, ttype_sub, TimeSeriesRollType.Last] * order.Unit * (if order.Instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Future then (order.Instrument :?> Future).PointSize else 1.0), orderDate.DateTime, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, this.Portfolio.Currency, order.Instrument.Currency)) 0.0
                                                                    acc + cc
                                                                | _ -> 
                                                                    acc + weight * reference_aum) 0.0

                                            let max_global_levarge = this.[orderDate.DateTime, (int)MemoryType.GlobalMaximumLeverage, TimeSeriesRollType.Last]
                                            let max_leverage_scale = if Math.Round(sum_value, threshhold_rounding) > Math.Round(max_global_levarge * reference_aum, threshhold_rounding) then max_global_levarge * reference_aum / sum_value else 1.0
                                            let newWeights = weights |> List.mapi(fun i w -> w * max_leverage_scale)                                            
                                            let newWeightMap = ids |> List.mapi (fun i element -> (element, newWeights.[i]))

                                            newWeightMap |> Map.ofList)

                                    |> (fun weightMap ->                                                                        // Maximum VaR Constrained Weight (Step 6)
                                        if weightMap.Count = 0 then
                                            weightMap
                                        else
                                            let weights = weightMap |> Map.toList |> List.map (fun (k,v) -> v)                                            
                                            let ids = weightMap |> Map.toList |> List.map (fun (k,v) -> k)
                                            let TargetVAR = this.[orderDate.DateTime, (int)MemoryType.TargetVAR, TimeSeriesRollType.Last]
                                            let GlobalVARTargetFlag = (int)this.[orderDate.DateTime, (int)MemoryType.GlobalTargetVARFlag, TimeSeriesRollType.Last]
                                            if (GlobalVARTargetFlag = 1 && not (TargetVAR = 0.0)) then // VaR Calculation
                                                let returnsList = 
                                                    Seq.toList instruments.Values
                                                    |> List.filter (fun instrument -> weightMap.ContainsKey(instrument.ID))
                                                    |> List.map (fun instrument ->                                                
                                                        match instrument with
                                                        | x when x.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy && (not ((x :?> Strategy).Portfolio = null)) -> // Generate aggregated list of returns for each instrument in portfolio of this strategy
                                                            let strategy = x :?> Strategy

                                                            Seq.toList (strategy.Portfolio.AggregatedPositionOrders(orderDate.DateTime).Values)
                                                            |> List.filter (fun order -> not (strategy.Portfolio.IsReserve(order.Instrument)))
                                                            |> List.fold (fun acc position ->
                                                                let ttype_sub = if position.Instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || position.Instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose elif position.Instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close;
                                                                let ts = position.Instrument.GetTimeSeries(ttype_sub)
                                                                let orders = this.Portfolio.FindOpenOrder(position.Instrument, orderDate.DateTime, true)
                                                                let order = if orders.Count = 0 then null else orders.Values  |> Seq.toList |> List.filter (fun o -> o.Type = OrderType.Market) |> List.reduce (fun acc o -> o) 
                                                                let idx = ts.GetClosestDateIndex(orderDate.DateTime, TimeSeries.DateSearchType.Previous)
                                                                let weight = position.Unit * (if position.Instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Future then (position.Instrument :?> Future).PointSize else 1.0);
                                                                let rets = [1 .. 252] 
                                                                        |> List.map (fun i ->
                                                                            let first = Math.Max(0, idx - 252 + i - 20)
                                                                            let last = Math.Max(0, idx - 252 + i)
                                                                            let fx_t = CurrencyPair.Convert(1.0, ts.DateTimes.[last], TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, this.Portfolio.Currency, position.Instrument.Currency)
                                                                            let fx_0 = CurrencyPair.Convert(1.0, ts.DateTimes.[first], TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, this.Portfolio.Currency, position.Instrument.Currency)
                                                                            let ret = (ts.[last] * fx_t) - (ts.[first] * fx_0)
                                                                            ret * weight * weightMap.[instrument.ID])
                                                                [1 .. 252] |> List.map (fun i -> acc.[i - 1] + rets.[i - 1])) (Array.zeroCreate 252 |> Array.toList)
                                        
                                                        | _ ->  // Generate list of returns for this instrument
                                                            let ttype = if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose elif instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close              
                                                            let ts = instrument.GetTimeSeries(ttype)                                
                                                            let idx = ts.GetClosestDateIndex(orderDate.DateTime, TimeSeries.DateSearchType.Previous)

                                                            [1 .. 252] |> List.map (fun i ->
                                                                let first = Math.Max(0, idx - 252 + i - 20)
                                                                let last = Math.Max(0, idx - 252 + i)
                                                                let fx_t = CurrencyPair.Convert(1.0, ts.DateTimes.[last], TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, this.Portfolio.Currency, instrument.Currency)
                                                                let fx_0 = CurrencyPair.Convert(1.0, ts.DateTimes.[first], TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, this.Portfolio.Currency, instrument.Currency)
                                                                let ret = (ts.[last] * fx_t) - (ts.[first] * fx_0)
                                    
                                                                ret * (weightMap.[instrument.ID] * reference_aum) / (fx_t * ts.[last] * (if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Future then ((instrument :?> Future).PointSize) else 1.0))))
                                    
                                                let rets = returnsList|> List.fold (fun (acc : float list) rets -> [1 .. 252] |> List.map( fun j -> acc.[j - 1] + rets.[j - 1])) (Array.zeroCreate 252 |> Array.toList) |> List.sort
                                                let pctl = 0.01 * (double (rets.Length - 1)) + 1.0;
                                                let pctl_n = (int)pctl
                                                let pctl_d = pctl - (double)pctl_n
                                                let VaR = (rets.[pctl_n] + pctl_d * (rets.[pctl_n + 1] - rets.[pctl_n])) / reference_aum
                                                if (VaR <= TargetVAR) then                        
                                                    let max_var_scale = TargetVAR / VaR
                                                    let newWeights = weights |> List.mapi(fun i w -> w * max_var_scale)                                            
                                                    let newWeightMap = ids |> List.mapi (fun i element -> (element, newWeights.[i]))

                                                    newWeightMap |> Map.ofList
                                                else
                                                    weightMap
                                            else
                                                weightMap)

                                else
                                    [(0,0.0)] |> Map.ofList
            
                Seq.toList instruments.Values  //generate orders
                    |> List.filter (fun instrument -> weightMap.ContainsKey(instrument.ID))
                    |> List.iter (fun instrument ->
                        let position = this.Portfolio.FindPosition(instrument, orderDate.DateTime)
                        let orders = this.Portfolio.FindOpenOrder(instrument, orderDate.DateTime, false)
                        let order = if orders.Count = 0 then null else orders.Values  |> Seq.toList |> List.filter (fun o -> o.Type = OrderType.Market) |> List.reduce (fun acc o -> o) 
                        let notional_strategy_adjustment = Utils.Notional_Strategy_Adjustment(instrument, orderDate, reference_aum)
                        
                        let weight = Math.Abs(weightMap.[instrument.ID])
                        let size = (if instrument.InstrumentType = InstrumentType.Strategy && not ((instrument :?> Strategy).Portfolio = null) then (instrument :?> Strategy).Direction(orderDate.DateTime, if weight > 0.0 then DirectionType.Long else DirectionType.Short); Math.Abs(weight) else weight) * reference_aum * notional_strategy_adjustment

                        let rebalancing_threshhold = this.[orderDate.DateTime, (int)MemoryType.RebalancingThreshhold, TimeSeriesRollType.Last]                      
                        let ttype_sub = if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.ETF || instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Equity then TimeSeriesType.AdjClose elif instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy then TimeSeriesType.Last else TimeSeriesType.Close                        

                        if not (order = null) then // if order exists then update
                            let instrumentValue =   if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy && (not ((instrument :?> Strategy).Portfolio = null)) then 
                                                        (instrument :?> Strategy).GetNextAUM(orderDate.DateTime, TimeSeriesType.Last) 
                                                    else 
                                                        instrument.[orderDate.DateTime, ttype_sub, TimeSeriesRollType.Last] * (if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Future then (instrument :?> Future).PointSize else 1.0)

                            let posValue = (if instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Strategy && (not ((instrument :?> Strategy).Portfolio = null)) then 1.0 else (order.Unit + (if not (position = null) then position.Unit else 0.0))) * instrumentValue
                            let notionalDiff = Math.Abs(weight * notional_strategy_adjustment - posValue / reference_aum)

                            if (notionalDiff > rebalancing_threshhold) then                                                                                                 // (Step 7)
                                order.UpdateTargetMarketOrder(orderDate.DateTime, size, UpdateType.OverrideNotional) |> ignore

                        elif not (position = null) then // if position exists then update
                            let posValue = position.Value(orderDate.DateTime, ttype_sub, DataProvider.DefaultProvider, TimeSeriesRollType.Last)
                            let notionalDiff = Math.Abs(weight  * notional_strategy_adjustment - posValue / reference_aum)

                            if (notionalDiff > rebalancing_threshhold) then
                                position.UpdateTargetMarketOrder(orderDate.DateTime, size, UpdateType.OverrideNotional) |> ignore

                        else
                            this.Portfolio.CreateTargetMarketOrder(instrument, orderDate.DateTime, size / (if instrument.InstrumentType = InstrumentType.Strategy then 1.0 else instrument.[orderDate.DateTime, TimeSeriesType.Last, TimeSeriesRollType.Last])) |> ignore)


    /// <summary>
    /// Function: Create a strategy
    /// </summary>    
    /// <param name="name">Name
    /// </param>
    /// <param name="initialDay">Creating date
    /// </param>
    /// <param name="initialValue">Starting NAV and portfolio AUM.
    /// </param>
    /// <param name="portfolio">Portfolio used by the strategy
    /// </param>
    /// <param name="underlyings">List of assets in the portfolio
    /// </param>
    /// <param name="fractionContract">True if fractional contracts are allowed. False if units of contracts are rounded to the closest interger.
    /// </param>
    static member public CreateStrategy(instrument : Instrument, initialDate : BusinessDay, initialValue : double, portfolio : Portfolio, underlyings : System.Collections.Generic.List<Instrument>, fractionContract :  Boolean) : PortfolioStrategy =
        match instrument with
        | x when x.InstrumentType = InstrumentType.Strategy ->
            let Strategy = new PortfolioStrategy(instrument)

            if not (underlyings = null) then underlyings |> Seq.toList |> List.iter (fun strategy -> Strategy.AddInstrument(strategy, initialDate.DateTime))
                
            Strategy.AddMemoryPoint(initialDate.DateTime, (if fractionContract then 1.0 else 0.0), (int)MemoryType.FractionContract)
                                
            Strategy.Startup(initialDate, initialValue, portfolio)
            Strategy
        | _ -> raise (new Exception("Instrument not a Strategy"))


    /// <summary>
    /// Function: Create a strategy
    /// </summary>    
    /// <param name="name">Name
    /// </param>
    /// <param name="description">Description
    /// </param>
    /// <param name="startDate">Creating date
    /// </param>
    /// <param name="startValue">Starting NAV and portfolio AUM.
    /// </param>
    /// <param name="parent">Portfolio used by the parent strategy
    /// </param>
    /// <param name="simulated">True if not stored persistently.
    /// </param>
    /// <param name="fractionContract">True if fractional contracts are allowed. False if units of contracts are rounded to the closest interger.
    /// </param>
    static member public Create(name : string, description : string, startDate : DateTime, startValue : double, parent : Portfolio , simulated : Boolean , fractional : Boolean) : PortfolioStrategy =
            let calendar = Calendar.FindCalendar("WE")

            let date = calendar.GetClosestBusinessDay(startDate, TimeSeries.DateSearchType.Next)

            let strategy_funding = FundingType.TotalReturn

            if (parent = null) then            
                let usd_currency = Currency.FindCurrency("USD")
                
                let usd_cash_instrument = Instrument.CreateInstrument(name + "/" + usd_currency.Name + "/Cash", InstrumentType.Strategy, name + " - " + usd_currency.Name + " Libor Cash Strategy", usd_currency, FundingType.TotalReturn, simulated)
                let usd_cash_strategy = ONDepositStrategy.CreateStrategy(usd_cash_instrument, date, 1.0, 0.0, null)
                usd_cash_strategy.Calendar <- calendar;
                usd_cash_strategy.SetConstantCarryCost(0.0 / (double)10000.0, 0.0 / (double)10000.0, DayCountConvention.Act360, 360.0)
                usd_cash_strategy.TimeSeriesRoll <- TimeSeriesRollType.Last
                usd_cash_strategy.Initialize()
                
                let main_currency = usd_currency
                let main_cash_strategy = usd_cash_strategy

                // Master Strategy Portfolios
                let master_portfolio_instrument = Instrument.CreateInstrument(name + "/Portfolio", InstrumentType.Portfolio, description + " Strategy Portfolio", main_currency, FundingType.TotalReturn, simulated)
                let master_portfolio = Portfolio.CreatePortfolio(master_portfolio_instrument, main_cash_strategy, main_cash_strategy, parent)                
                master_portfolio.TimeSeriesRoll <- TimeSeriesRollType.Last;
                master_portfolio.AddReserve(usd_currency, usd_cash_strategy, usd_cash_strategy)

                let ccy_names = [ "EUR"; "SEK"; "HKD"; "AUD"; "SGD"; "GBP"; "CHF"; "ZAR"; "JPY"; "CAD"; "BRL"; "MXN"; "KRW"; "TWD"; "RUB"; "PLZ"; "NOK"; "NZD" ]

                ccy_names
                |> List.iter (fun ccy_name ->
                    let x_currency = Currency.FindCurrency(ccy_name)
                    let x_cash_instrument = Instrument.CreateInstrument(name + "/" + x_currency.Name + "/Cash", InstrumentType.Strategy, name + " - " + x_currency.Name + " Libor Cash Strategy", x_currency, FundingType.TotalReturn, simulated)
                    let x_cash_strategy = ONDepositStrategy.CreateStrategy(x_cash_instrument, date, 1.0, 0.0, null)
                    x_cash_strategy.Calendar <- calendar;
                    x_cash_strategy.SetConstantCarryCost(0.0 / (double)10000.0, 0.0 / (double)10000.0, DayCountConvention.Act360, 360.0)
                    x_cash_strategy.TimeSeriesRoll <- TimeSeriesRollType.Last
                    x_cash_strategy.Initialize()
                    master_portfolio.AddReserve(x_currency, x_cash_strategy, x_cash_strategy))

                List.ofSeq master_portfolio.Reserves             
                |> List.filter (fun (instrument : Instrument) -> instrument.InstrumentType = InstrumentType.Strategy) 
                |> List.iter (fun strategy -> (strategy :?> Strategy).NAVCalculation(date) |> ignore)

                // Master Strategy Instruments, Strategies
                let master_strategy_instrument = Instrument.CreateInstrument(name, InstrumentType.Strategy, description, main_currency, strategy_funding, simulated)
                master_strategy_instrument.TimeSeriesRoll <- master_portfolio.TimeSeriesRoll
                let master_strategy = PortfolioStrategy.CreateStrategy(master_strategy_instrument, date, startValue, master_portfolio, new Collections.Generic.List<Instrument>(), fractional)
                master_strategy.Calendar <- calendar
                master_portfolio.Strategy <- master_strategy

                if not simulated then            
                    master_strategy.Tree.SaveNewPositions();
                    master_strategy.Tree.Save();

                master_strategy
            
            else            
                // Master Strategy Portfolios
                let master_portfolio_instrument = Instrument.CreateInstrument(name + "/Portfolio", InstrumentType.Portfolio, description + " Strategy Portfolio", parent.Currency, FundingType.TotalReturn, simulated);
                let master_portfolio = Portfolio.CreatePortfolio(master_portfolio_instrument, parent.LongReserve, parent.ShortReserve, parent);
                master_portfolio.TimeSeriesRoll <- TimeSeriesRollType.Last;

                parent.Reserves
                |> Seq.toList
                |> List.iter (fun reserve ->
                    master_portfolio.AddReserve(reserve.Currency, parent.Reserve(reserve.Currency, PositionType.Long), parent.Reserve(reserve.Currency, PositionType.Short)))

                // Master Strategy Instruments, Strategies
                let master_strategy_instrument = Instrument.CreateInstrument(name, InstrumentType.Strategy, description + " Strategy", parent.Currency, strategy_funding, simulated)
                master_strategy_instrument.TimeSeriesRoll <- master_portfolio.TimeSeriesRoll
                let master_strategy = PortfolioStrategy.CreateStrategy(master_strategy_instrument, date, startValue, master_portfolio, null, fractional)
                master_strategy.Calendar <- calendar
                master_portfolio.Strategy <- master_strategy
            
                if not simulated then            
                    master_strategy.Tree.SaveNewPositions();
                    master_strategy.Tree.Save();
            
                master_strategy