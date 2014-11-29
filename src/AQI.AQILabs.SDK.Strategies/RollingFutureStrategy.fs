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
/// Class representing a strategy that rolls a future for a specific underlying according to a given roll schedule.
/// </summary>
type RollingFutureStrategy = 
    inherit Strategy    

    val mutable _underlyingInstrument : Instrument
        
    /// <summary>
    /// Constructor
    /// </summary> 
    new(instrument : Instrument) = { inherit Strategy(instrument); _underlyingInstrument = null; }         

    /// <summary>
    /// Constructor
    /// </summary> 
    new(id : int) = { inherit Strategy(id); _underlyingInstrument = null; }
      
    /// <summary>
    /// Function: returns a list of names of used memory types.
    /// </summary>   
    override this.MemoryTypeNames() : string[] = System.Enum.GetNames(typeof<MemoryType>)

    /// <summary>
    /// Function: returns a list of ids of used memory types.
    /// </summary>  
    override this.MemoryTypeInt(name : string) = System.Enum.Parse(typeof<MemoryType> , name) :?> int

    /// <summary>
    /// Property: returns the underlying instrument of the rolled futures.
    /// </summary>  
    member this.UnderlyingInstrument 
        with get() = this._underlyingInstrument
        and private set value = this._underlyingInstrument <- value


    /// <summary>
    /// Function: Startup function called once during the creation of the strategy.
    /// If the strategy is persistently stored, this should only be called at creation.
    /// </summary>    
    override this.Startup(initialDate : BusinessDay, initialValue : double, portfolio : Portfolio) =
        base.Startup(initialDate, initialValue, portfolio)

        this.UnderlyingInstrument <- Instrument.FindInstrument((int)this.[DateTime.Now, (int)MemoryType.UnderlyingID, TimeSeriesRollType.Last])

        List.ofSeq this.Portfolio.Reserves             
        |> List.iter (fun strategy ->
            let strat = strategy :?> Strategy 
            strat.Initialize()
            this.Tree.AddSubStrategy strat)
        
        let value = portfolio.[initialDate.DateTime]
        this.Portfolio.UpdateReservePosition(initialDate.DateTime , initialValue - value, this.Currency) |> ignore
        this.UpdateAUM(initialDate.DateTime , initialValue , true)

        this.CommitNAVCalculation(initialDate, abs(portfolio.[initialDate.DateTime]), TimeSeriesType.Last)
        this.UpdateAUM(initialDate.DateTime, portfolio.[initialDate.DateTime], true)

        this.Initialize()
        this.AddRemoveSubStrategies(initialDate)
        this.RemoveInstruments(initialDate.DateTime)


    /// <summary>
    /// Function: Initialize the strategy during runtime.
    /// </summary>      
    override this.Initialize() =        
        match this.Initialized with
        | true -> ()
        | _ ->                                         
            List.ofSeq this.Portfolio.Reserves // Add reserve strategies to the tree
            |> List.iter (fun reserve -> this.Tree.AddSubStrategy((reserve :?> Strategy)))
        
            base.Initialize()

    /// <summary>
    /// Function: Returns the id of the contract traded at a given date.
    /// </summary> 
    /// <param name="date">reference date.
    /// </param>
    member this.Contract(date : DateTime) =
        (int) this.[date, (int)MemoryType.Contract, TimeSeriesRollType.Last]
                

    /// <summary>
    /// Function: Execute the rolling future logic.
    /// </summary> 
    /// <param name="ctx">Context containing relevant environment information for the logic execution
    /// </param>
    override this.ExecuteLogic(ctx : ExecutionContext) =
        
        let orderDate = ctx.OrderDate        
        let reference_aum = ctx.ReferenceAUM

        match ctx.ReferenceAUM with
        | 0.0 -> () // Stop calculations
        | _ ->      // Run calculations
            let sign = (int)this.[orderDate.DateTime, (int)MemoryType.Sign, TimeSeriesRollType.Last]
            let contract = (int)this.[orderDate.DateTime, (int)MemoryType.Contract, TimeSeriesRollType.Last]
            let rollDayDouble = this.[orderDate.DateTime, (int)MemoryType.RollDay, TimeSeriesRollType.Last]
            this._underlyingInstrument <- Instrument.FindInstrument((int)this.[orderDate.Close, (int)MemoryType.UnderlyingID, TimeSeriesRollType.Last])

            let positions = this.Portfolio.Positions(orderDate.DateTime)
            let rollDate = if Double.IsNaN(rollDayDouble) then 5 else (int)rollDayDouble            
            let instrument = this._underlyingInstrument;
            
            // list of positions in the portfolio
            let positions_sorted = if not (positions = null) then
                                        positions
                                        |> Seq.toList
                                        |> List.filter (fun pos ->
                                            pos.Instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Future)
                                        |> List.filter (fun pos ->
                                            let fut = Future.FindFuture(Security.FindSecurity(pos.Instrument))
                                            fut.Underlying = instrument)
                                        |> List.sortBy (fun pos ->
                                            let fut = Future.FindFuture(Security.FindSecurity(pos.Instrument))
                                            fut.LastTradeDate)
                                        |> List.toArray
                                    else
                                        null

            // position in future with closest expiry
            let position0 = if not (positions = null) && positions_sorted.Length = 1 then positions_sorted.[0] else null
            // future with closest expiry
            let future_current0 = if not (positions = null) && positions_sorted.Length = 1 then position0.Instrument :?> Future else null            
            // roll date of the future with closest expiry
            let currentRollDate0 = if future_current0 = null then null else this.Calendar.GetClosestBusinessDay((if future_current0.LastTradeDate < future_current0.FirstNoticeDate then future_current0.LastTradeDate else future_current0.FirstNoticeDate), TimeSeries.DateSearchType.Next).AddBusinessDays(-(rollDate - 1))

            // if a position exists
            if not (currentRollDate0 = null) then
                
                // if the future held in the portfolio has an roll date that is prior or equal to the current date
                if (currentRollDate0.DateTime <= orderDate.DateTime.Date) then
                    
                    // cut existing position
                    if not (position0 = null) then                    
                        position0.UpdateTargetMarketOrder(orderDate.DateTime, 0.0, UpdateType.OverrideUnits) |> ignore
                        this.RemoveInstruments(orderDate.DateTime)
                    
                    // get next future                    
                    let nextFuture = ref (Future.CurrentFuture(instrument, orderDate.DateTime.Date))
                    let nextRollDate = (if !nextFuture = null then null else this.Calendar.GetClosestBusinessDay((if (!nextFuture).LastTradeDate < (!nextFuture).FirstNoticeDate then (!nextFuture).LastTradeDate else (!nextFuture).FirstNoticeDate), TimeSeries.DateSearchType.Next).AddBusinessDays(-(rollDate - 1)))

                    if not (nextRollDate = null) && (nextRollDate.DateTime <= orderDate.DateTime.Date) then
                        nextFuture := (!nextFuture).NextFuture

                    if not (!nextFuture = null) then                    
                        // roll to the specified contract
                        [1 .. contract - 1] |> List.iter (fun i -> nextFuture := (!nextFuture).NextFuture)
                        
                        this.RemoveInstruments(orderDate.DateTime)
                        let contract_value = (!nextFuture).[orderDate.Close, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last] * (!nextFuture).PointSize
                        let unit = CurrencyPair.Convert(reference_aum, orderDate.DateTime, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, instrument.Currency, this.Portfolio.Currency) / (contract_value)
                        this.AddInstrument(!nextFuture, orderDate.DateTime)                        
                        this.Portfolio.CreateTargetMarketOrder(!nextFuture, orderDate.DateTime, Math.Abs(unit) * (double) sign) |> ignore
                    
            // If no positions exist               
            else
            
                // find next contract
                let nextFuture = ref (Future.CurrentFuture(instrument, orderDate.DateTime.Date))                
                let nextRollDate = (if !nextFuture = null then null else this.Calendar.GetClosestBusinessDay((if (!nextFuture).LastTradeDate < (!nextFuture).FirstNoticeDate then (!nextFuture).LastTradeDate else (!nextFuture).FirstNoticeDate), TimeSeries.DateSearchType.Next).AddBusinessDays(-(rollDate - 1)))

                if not (nextRollDate = null) then
                    if (nextRollDate.DateTime <= orderDate.DateTime.Date) then
                        nextFuture := (!nextFuture).NextFuture

                // roll to the specified contract
                [1 .. contract - 1] |> List.iter (fun i -> nextFuture := (!nextFuture).NextFuture)

                if not (!nextFuture = null) then                
                    let reference_aum_local = CurrencyPair.Convert(reference_aum, orderDate.DateTime, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, instrument.Currency, this.Portfolio.Currency)
                    
                    this.RemoveInstruments(orderDate.DateTime)
                    let contractValue = (!nextFuture).[orderDate.Close, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last] * (!nextFuture).PointSize
                    let unit = (if Double.IsNaN(reference_aum_local) then reference_aum else reference_aum_local) / contractValue                    
                    if (not (Double.IsInfinity(unit) || Double.IsNaN(unit))) then
                        this.AddInstrument(!nextFuture, orderDate.DateTime)
                        this.Portfolio.CreateTargetMarketOrder(!nextFuture, orderDate.DateTime, Math.Abs(unit) * (double) sign) |> ignore


        /// <summary>
        /// Function: Set the direction type of the strategy
        /// </summary>       
        /// <param name="date">DateTime valued date 
        /// </param>
        /// <param name="sign">Long or Short
        /// </param>  
        override this.Direction(date : DateTime, sign : DirectionType) =        
            let sign_old = (int)this.[date, (int)MemoryType.Sign, TimeSeriesRollType.Last]

            if ((int)sign = sign_old) then
                ()
            else
                this.AddMemoryPoint(date, (double)sign, (int)MemoryType.Sign)           
                this.UnderlyingInstrument <- Instrument.FindInstrument((int)this.[date, (int)MemoryType.UnderlyingID, TimeSeriesRollType.Last])
                let positions = this.Portfolio.Positions(date)
                let positions_sorted = if not (positions = null) then
                                        positions
                                        |> Seq.toList
                                        |> List.filter (fun pos ->
                                            pos.Instrument.InstrumentType = AQI.AQILabs.Kernel.InstrumentType.Future)
                                        |> List.filter (fun pos ->
                                            let fut = Future.FindFuture(Security.FindSecurity(pos.Instrument))
                                            fut.Underlying = this.UnderlyingInstrument)
                                        |> List.sortBy (fun pos ->
                                            let fut = Future.FindFuture(Security.FindSecurity(pos.Instrument))
                                            fut.LastTradeDate)
                                        |> List.toArray

                                        else
                                            null

                let position0 = ref null
                if (not (positions = null) && positions_sorted.Length = 1) then
                    position0 := positions_sorted.[0]
                    (!position0).UpdateTargetMarketOrder(date, (double)sign * Math.Abs((!position0).Unit), UpdateType.OverrideUnits) |> ignore
        

        /// <summary>
        /// Function: Get the direction type of the strategy
        /// </summary>       
        /// <param name="date">DateTime valued date 
        /// </param>
        override this.Direction(date : DateTime) =        
            let sign_old = (int)this.[date, (int)MemoryType.Sign, TimeSeriesRollType.Last]
            if sign_old = 1 then DirectionType.Long else DirectionType.Short
       
                            

    /// <summary>
    /// Function: Create a strategy
    /// </summary>    
    /// <param name="name">Name
    /// </param>
    /// <param name="initialDay">Creating date
    /// </param>
    /// <param name="initialValue">Starting NAV and portfolio AUM.
    /// </param>
    /// <param name="underlyingInstrument">Underlying instrument
    /// </param>
    /// <param name="contract">number of contract to roll into in the active chain
    /// </param>
    /// <param name="rollDay">number of business day of the month to implement the roll
    /// </param>
    /// <param name="portfolio">Portfolio to be used in this strategy
    /// </param>
    static member public CreateStrategy(instrument : Instrument, initialDate : BusinessDay, initialValue : double, underlyingInstrument : Instrument , contract : int, rollDay : int, portfolio : Portfolio) : RollingFutureStrategy =
        match instrument with
        | x when x.InstrumentType = InstrumentType.Strategy ->

            let Strategy = new RollingFutureStrategy(instrument)
                
            portfolio.Strategy <- Strategy
            Strategy.Calendar <- underlyingInstrument.Calendar

            Strategy.AddMemoryPoint(DateTime.MinValue, 1.0, (int)MemoryType.Sign)
            Strategy.AddMemoryPoint(DateTime.MinValue, (double)underlyingInstrument.ID, (int)MemoryType.UnderlyingID)
            Strategy.AddMemoryPoint(DateTime.MinValue, (double)contract, (int)MemoryType.Contract)
            Strategy.AddMemoryPoint(DateTime.MinValue, (double)rollDay, (int)MemoryType.RollDay)

            Strategy.Startup(initialDate, initialValue, portfolio)

            Strategy.InitialDate <- new DateTime(1990, 01, 06)
            Strategy
        | _ -> raise (new Exception("Instrument not a Strategy"))


    /// <summary>
    /// Function: Create a strategy
    /// </summary>    
    /// <param name="name">Name
    /// </param>
    /// <param name="description">Description
    /// </param>
    /// <param name="initialDay">Creating date
    /// </param>
    /// <param name="initialValue">Starting NAV and portfolio AUM.
    /// </param>
    /// <param name="underlyingInstrument">Underlying instrument
    /// </param>
    /// <param name="contract">number of contract to roll into in the active chain
    /// </param>
    /// <param name="rollDay">number of business day of the month to implement the roll
    /// </param>
    /// <param name="parent">Parent portfolio of parent strategy
    /// </param>
    /// <param name="simulated">True if not stored in persistent storage
    /// </param>
    static member public Create(name : string, description : string, startDate : DateTime, startValue : double, underlyingInstrument : Instrument , contract : int, rollDay : int, parent : Portfolio , simulated : Boolean) : RollingFutureStrategy =
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

                // Master Strategy Instruments, Strategies
                let master_strategy_instrument = Instrument.CreateInstrument(name, InstrumentType.Strategy, description + " Strategy", main_currency, strategy_funding, simulated)
                master_strategy_instrument.TimeSeriesRoll <- master_portfolio.TimeSeriesRoll
                let master_strategy = RollingFutureStrategy.CreateStrategy(master_strategy_instrument, date, startValue, underlyingInstrument, contract, rollDay, master_portfolio)
                master_strategy.Calendar <- calendar
                master_portfolio.Strategy <- master_strategy

                if not simulated then            
                    master_strategy.Tree.SaveNewPositions()
                    master_strategy.Tree.Save()

                master_strategy
            
            else            
                // Master Strategy Portfolios
                let master_portfolio_instrument = Instrument.CreateInstrument(name + "/Portfolio", InstrumentType.Portfolio, description + " Strategy Portfolio", parent.Currency, FundingType.TotalReturn, simulated)
                let master_portfolio = Portfolio.CreatePortfolio(master_portfolio_instrument, parent.LongReserve, parent.ShortReserve, parent)
                master_portfolio.TimeSeriesRoll <- TimeSeriesRollType.Last

                parent.Reserves
                |> Seq.toList
                |> List.iter (fun reserve ->
                    master_portfolio.AddReserve(reserve.Currency, parent.Reserve(reserve.Currency, PositionType.Long), parent.Reserve(reserve.Currency, PositionType.Short)))

                // Master Strategy Instruments, Strategies
                let master_strategy_instrument = Instrument.CreateInstrument(name, InstrumentType.Strategy, description + " Strategy", parent.Currency, strategy_funding, simulated)
                master_strategy_instrument.TimeSeriesRoll <- master_portfolio.TimeSeriesRoll
                let master_strategy = RollingFutureStrategy.CreateStrategy(master_strategy_instrument, date, startValue, underlyingInstrument, contract, rollDay, master_portfolio)
                //let master_strategy = PortfolioStrategy.CreateStrategy(master_strategy_instrument, date, startValue, master_portfolio, null, fractional)
                master_strategy.Calendar <- calendar
                master_portfolio.Strategy <- master_strategy
            
                if not simulated then            
                    master_strategy.Tree.SaveNewPositions()
                    master_strategy.Tree.Save()
            
                master_strategy
