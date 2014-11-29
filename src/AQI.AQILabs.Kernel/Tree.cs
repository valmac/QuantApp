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
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

using System.Threading;
using System.Threading.Tasks;

using System.Data;
using System.ComponentModel;
using System.Reflection;

using AQI.AQILabs.Kernel.Numerics.Util;

namespace AQI.AQILabs.Kernel
{
    /// <summary>
    /// Class containing the logic that manages the 
    /// execution of the Tree structure of multi-level strategies.
    /// </summary>
    public class Tree : MarshalByRefObject
    {
        private static Dictionary<int, Tree> _treeDB = new Dictionary<int, Tree>();
        private static Dictionary<DateTime, Dictionary<int, double>> _strategyExecutionDB = new Dictionary<DateTime, Dictionary<int, double>>();

        public readonly static object objLock = new object();

        /// <summary>
        /// Function: Return the tree object for a given strategy
        /// </summary>    
        /// <param name="strategy">reference strategy.
        /// </param>
        public static Tree GetTree(Strategy strategy)
        {
            lock (objLock)
            {
                if (!_treeDB.ContainsKey(strategy.ID))
                    _treeDB.Add(strategy.ID, new Tree(strategy));

                return _treeDB[strategy.ID];
            }
        }

        private Strategy _parentStrategy = null;

        /// <summary>
        /// Constructor: Creates a tree for a given strategy
        /// </summary>    
        /// <param name="strategy">reference strategy.
        /// </param>
        public Tree(Strategy strategy)
        {
            this._parentStrategy = strategy;
        }

        /// <summary>
        /// Property: returns the strategy linked to this node of the tree
        /// </summary>  
        public Strategy Strategy
        {
            get
            {
                return _parentStrategy;
            }
        }

        /// <summary>
        /// Function: Initialize the tree and sub-nodes during runtime.
        /// </summary>       
        public void Initialize()
        {
            _parentStrategy.Initialize();

            foreach (Strategy strat in _strategyDB.Values)
                strat.Tree.Initialize();
        }


        private Dictionary<int, Strategy> _strategyDB = new Dictionary<int, Strategy>();

        /// <summary>
        /// Function: Remove the Strategy and sub-nodes from the persistent storage.
        /// </summary>          
        public void Remove()
        {
            if (_parentStrategy != null)
            {
                foreach (Strategy strat in _strategyDB.Values)
                    GetTree(strat).Remove();

                foreach (Dictionary<int, double> db in _strategyExecutionDB.Values)
                    if (db != null && db.ContainsKey(_parentStrategy.ID))
                        db.Remove(_parentStrategy.ID);

                if (_treeDB.ContainsKey(_parentStrategy.ID))
                    _treeDB.Remove(_parentStrategy.ID);

                if (_strategyDB.ContainsKey(_parentStrategy.ID))
                    _strategyDB.Remove(_parentStrategy.ID);
                _parentStrategy.Remove();
                _parentStrategy = null;
            }
        }

        /// <summary>
        /// Function: Remove strategy and sub-nodes data string from and including a given date.
        /// </summary>         
        /// <param name="date">reference date.
        /// </param>
        public void RemoveFrom(DateTime date)
        {
            if (_parentStrategy != null)
            {
                foreach (Strategy strat in _strategyDB.Values)
                    GetTree(strat).RemoveFrom(date);

                foreach (Dictionary<int, double> db in _strategyExecutionDB.Values)
                    if (db.ContainsKey(_parentStrategy.ID))
                        db.Remove(_parentStrategy.ID);

                if (_treeDB.ContainsKey(_parentStrategy.ID))
                    _treeDB.Remove(_parentStrategy.ID);

                _parentStrategy.RemoveFrom(date);
            }
        }

        /// <summary>
        /// Function: Add a sub-strategy to the tree
        /// </summary>         
        /// <param name="date">reference date.
        /// </param>
        public void AddSubStrategy(Strategy strategy)
        {
            if (strategy.Tree.ContainsStrategy(_parentStrategy))
                throw new Exception("Strategy: " + _parentStrategy + " is already a Sub-Strategy of: " + strategy);

            strategy.Initialize();
            if (!_strategyDB.ContainsKey(strategy.ID))
                _strategyDB.Add(strategy.ID, strategy);
        }

        /// <summary>
        /// Function: Remove a sub-strategy to the tree
        /// </summary>         
        /// <param name="date">reference date.
        /// </param>
        public void RemoveSubStrategy(Strategy strategy)
        {
            if (!strategy.Tree.ContainsStrategy(_parentStrategy))
                throw new Exception("Strategy: " + _parentStrategy + " is not a Sub-Strategy of: " + strategy);

            strategy.Initialize();
            if (_strategyDB.ContainsKey(strategy.ID))
                _strategyDB.Remove(strategy.ID);
        }

        /// <summary>
        /// Property: List of sub strategies
        /// </summary>         
        public List<Strategy> SubStrategies
        {
            get
            {
                return _strategyDB.Values.ToList();
            }
        }

        /// <summary>
        /// Function: Create a clone of this strategy and all sub-strategies.
        /// </summary>
        /// <param name="initialDate">initial date for the strategies in the cloned tree</param>
        /// <param name="finalDate">final date for the strategies in the cloned tree</param>
        /// <param name="simulated">true if strategies in tree are to be simulated and not persistent</param>
        public Tree Clone(DateTime initialDate, DateTime finalDate, bool simulated)
        {
            Dictionary<int, Strategy> clones = new Dictionary<int, Strategy>();
            Dictionary<int, double> initial_values = new Dictionary<int, double>();

            Tree clone = Clone_Internal(initialDate, finalDate, clones, initial_values, simulated);

            try
            {
                clone.Startup(_parentStrategy.Calendar.GetClosestBusinessDay(initialDate, TimeSeries.DateSearchType.Previous), initial_values);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return clone;
        }

        /// <summary>
        /// Function: recursive helper function for cloning process.
        /// </summary>        
        /// <param name="initialDate">Clone's initialDate</param>
        /// <param name="finalDate">Clone's finalDate</param>
        /// <param name="clones">internal table of previously cloned base ids and respective cloned strategies</param>
        /// <param name="initial_values">internal table of initial values for the new cloned strategies</param>
        /// <param name="simulated">true if the strategy is simulated and not persistent</param>
        private Tree Clone_Internal(DateTime initialDate, DateTime finalDate, Dictionary<int, Strategy> clones, Dictionary<int, double> initial_values, bool simulated)
        {
            Dictionary<int, Strategy> clones_internal = new Dictionary<int, Strategy>();

            foreach (Strategy strat in _strategyDB.Values)
            {
                Tree tree_clone = strat.Tree.Clone_Internal(initialDate, finalDate, clones, initial_values, simulated);
                if (tree_clone != null)
                {
                    if (!clones.ContainsKey(strat.ID))
                        clones.Add(strat.ID, tree_clone.Strategy);
                    clones_internal.Add(strat.ID, tree_clone.Strategy);
                }
            }

            foreach (Strategy strategy in _strategyDB.Values)
                if (strategy.Portfolio == null)
                {
                    if (!clones.ContainsKey(strategy.ID))
                    {
                        Strategy subclone = strategy.Clone(null, initialDate, finalDate, clones, simulated);

                        if (!clones.ContainsKey(strategy.ID))
                            clones.Add(strategy.ID, subclone);

                        clones_internal.Add(strategy.ID, subclone);

                        initial_values.Add(subclone.ID, strategy.GetTimeSeries(TimeSeriesType.Last).Values[0]);
                    }
                    else
                        clones_internal.Add(strategy.ID, clones[strategy.ID]);
                }

            if (_parentStrategy.Portfolio != null)
            {
                Portfolio portfolioClone = _parentStrategy.Portfolio.Clone(simulated);
                foreach (int[] ids in _parentStrategy.Portfolio.ReserveIds)
                {
                    Currency ccy = Currency.FindCurrency(ids[0]);
                    Instrument longReserve = Instrument.FindInstrument(ids[1]);
                    Instrument shortReserve = Instrument.FindInstrument(ids[2]);

                    portfolioClone.AddReserve(ccy, longReserve.InstrumentType == InstrumentType.Strategy ? clones[longReserve.ID] : longReserve, shortReserve.InstrumentType == InstrumentType.Strategy ? clones[shortReserve.ID] : shortReserve);
                }

                Strategy strategyClone = _parentStrategy.Clone(portfolioClone, initialDate, finalDate, clones, simulated);
                if (!clones.ContainsKey(_parentStrategy.ID))
                    clones.Add(_parentStrategy.ID, strategyClone);

                initial_values.Add(strategyClone.ID, _parentStrategy.GetAUM(DateTime.Now, TimeSeriesType.Last));

                Tree clone = strategyClone.Tree;
                foreach (Strategy st in clones_internal.Values)
                {
                    if (st.Portfolio != null)
                        st.Portfolio.ParentPortfolio = clone.Strategy.Portfolio;

                    clone.AddSubStrategy(st);
                }

                return clone;
            }
            return null;
        }

        /// <summary>
        /// Function: recursive helper function for cloning process including positions in the base portfolios.
        /// </summary>        
        /// <param name="initialDate">Clone's initialDate</param>
        /// <param name="finalDate">Clone's finalDate</param>
        /// <param name="clones">internal table of previously cloned base ids and respective cloned strategies</param>
        /// <param name="initial_values">internal table of initial values for the new cloned strategies</param>
        /// <param name="simulated">true if the strategy is simulated and not persistent</param>
        private void Clone_Internal_Positions(DateTime initialDate, DateTime finalDate, Dictionary<int, Strategy> clones, Dictionary<int, double> initial_values, bool simulated)
        {
            Dictionary<int, Strategy> clones_internal = new Dictionary<int, Strategy>();

            foreach (Strategy strat in _strategyDB.Values)
            {
                if (strat.InitialDate <= initialDate && strat.FinalDate >= finalDate)
                    clones_internal.Add(strat.ID, clones[strat.ID]);
            }

            if (_parentStrategy.Portfolio != null)
            {
                foreach (Strategy st in clones_internal.Values)
                {
                    if (!clones[_parentStrategy.ID].Portfolio.IsReserve(st))
                        clones[_parentStrategy.ID].Portfolio.CreatePosition(st, initialDate, (initial_values[st.ID] == 0.0 ? 0 : 1.0), initial_values[st.ID]);
                }
            }
        }

        /// <summary>
        /// Function: Checks if the Tree contains a given strategy
        /// </summary>       
        /// <param name="strategy">reference strategy
        /// </param>
        public bool ContainsStrategy(Strategy strategy)
        {
            foreach (Strategy strat in _strategyDB.Values)
                if (GetTree(strat).ContainsStrategy(strat))
                    return true;

            return _strategyDB.ContainsKey(strategy.ID);
        }

        /// <summary>
        /// Function: Calculate the NAV strategies without portfolios prior to the ones with portfolios.
        /// Used for index calculation for example.
        /// </summary>
        /// <param name="day">reference day</param>
        public double PreNAVCalculation(DateTime day)
        {
            foreach (Strategy strat in _strategyDB.Values)
                if (strat.InitialDate <= day && strat.FinalDate >= day)
                    strat.Tree.PreNAVCalculation(day);


            BusinessDay date_local = _parentStrategy.Calendar.GetBusinessDay(day);
            if (date_local != null)
            {
                if (!_strategyExecutionDB.ContainsKey(date_local.DateTime))
                    _strategyExecutionDB.Add(date_local.DateTime, new Dictionary<int, double>());

                if (_parentStrategy.Portfolio == null && !_strategyExecutionDB[date_local.DateTime].ContainsKey(_parentStrategy.ID))
                    _strategyExecutionDB[date_local.DateTime].Add(_parentStrategy.ID, _parentStrategy.NAVCalculation(date_local));
            }

            return 0;
        }

        /// <summary>
        /// Function: Startup function called once during the creation of the strategy.       
        /// </summary>
        /// <remarks>called during the cloning process</remarks>
        private void Startup(BusinessDay initialDate, Dictionary<int, double> initial_values)
        {
            foreach (Strategy strat in _strategyDB.Values)
                strat.Tree.Startup(initialDate, initial_values);

            _parentStrategy.Startup(initialDate, Math.Abs(initial_values[_parentStrategy.ID]), _parentStrategy.Portfolio);
            if (initial_values[_parentStrategy.ID] < 0)
                _parentStrategy.UpdateAUMOrder(initialDate.DateTime, initial_values[_parentStrategy.ID]);
        }

        /// <summary>
        /// Function: Calculates the NAV of the Strategies in the Tree.
        /// </summary>
        /// <param name="day">reference day</param>
        public double NAVCalculation(DateTime day)
        {
            lock (objLock)
            {
                foreach (Strategy strat in _strategyDB.Values)
                    if (strat.InitialDate <= day && strat.FinalDate >= day)
                        strat.Tree.NAVCalculation(day);

                if (!_strategyExecutionDB.ContainsKey(day))
                    _strategyExecutionDB.Add(day, new Dictionary<int, double>());

                BusinessDay date_local = _parentStrategy.Calendar.GetBusinessDay(day);
                if (date_local != null)
                {
                    if (!_strategyExecutionDB[date_local.DateTime].ContainsKey(_parentStrategy.ID))
                        _strategyExecutionDB[date_local.DateTime].Add(_parentStrategy.ID, _parentStrategy.NAVCalculation(date_local));

                    return _strategyExecutionDB[date_local.DateTime][_parentStrategy.ID];
                }
                return 0.0;
            }
        }

        /// <summary>
        /// Function: Add or remove strategy as a node in the Tree.
        /// </summary>
        /// <param name="day">reference day</param>
        public void AddRemoveSubStrategies(DateTime day)
        {
            foreach (Strategy strat in _strategyDB.Values)
                if (strat.InitialDate <= day && strat.FinalDate >= day && strat.Portfolio != null)
                    GetTree(strat).AddRemoveSubStrategies(day);

            BusinessDay date_local = _parentStrategy.Calendar.GetBusinessDay(day);
            if (date_local != null)
                _parentStrategy.AddRemoveSubStrategies(date_local);
        }

        /// <summary>
        /// Function: Executed the logic for each strategy and its sub-nodes
        /// </summary>
        /// <param name="orderDate">reference day</param>
        public void ExecuteLogic(DateTime orderDate)
        {
            if (_parentStrategy.Portfolio != null)
            {
                DateTime executionDate = orderDate.AddDays(1);

                List<Strategy> recalcs = new List<Strategy>();


                //foreach (Strategy strat in _strategyDB.Values)
                //    if (strat.InitialDate <= orderDate && strat.FinalDate >= executionDate && strat.Portfolio != null)
                //    {
                //        double oldAUM = strat.GetNextAUM(orderDate, TimeSeriesType.Last);
                //        if (double.IsNaN(oldAUM) || oldAUM == 0.0)
                //            recalcs.Add(strat);
                //        else
                //            strat.Tree.ExecuteLogic(orderDate);
                //    }

                Parallel.ForEach(_strategyDB.Values, strat =>
                {
                    if (strat.InitialDate <= orderDate && strat.FinalDate >= executionDate && strat.Portfolio != null)
                    {
                        double oldAUM = strat.GetNextAUM(orderDate, TimeSeriesType.Last);
                        if (double.IsNaN(oldAUM) || oldAUM == 0.0)
                            recalcs.Add(strat);
                        else
                            strat.Tree.ExecuteLogic(orderDate);
                    }
                });

                BusinessDay orderDate_local = _parentStrategy.Calendar.GetBusinessDay(orderDate);


                if (_parentStrategy.Initialized && orderDate_local != null)
                {
                    _parentStrategy.ExecuteLogic(_parentStrategy.ExecutionContext(orderDate_local));

                    double newAUM = _parentStrategy.GetNextAUM(orderDate, TimeSeriesType.Last);

                    if (!double.IsNaN(newAUM))
                    {
                        Boolean recalc = false;
                        //foreach (Strategy strat in recalcs)
                        //    if (strat.InitialDate <= orderDate_local.DateTime && strat.FinalDate >= orderDate_local.AddBusinessDays(1).DateTime)
                        //    {
                        //        double oldAUM = strat.GetNextAUM(orderDate, TimeSeriesType.Last);

                        //        if (!double.IsNaN(oldAUM))
                        //        {
                        //            strat.Tree.ExecuteLogic(orderDate_local.DateTime);

                        //            recalc = true;
                        //        }
                        //    }


                        Parallel.ForEach(recalcs, strat =>
                        {
                            if (strat.InitialDate <= orderDate_local.DateTime && strat.FinalDate >= orderDate_local.AddBusinessDays(1).DateTime)
                            {
                                double oldAUM = strat.GetNextAUM(orderDate, TimeSeriesType.Last);

                                if (!double.IsNaN(oldAUM))
                                {
                                    strat.Tree.ExecuteLogic(orderDate_local.DateTime);

                                    recalc = true;

                                }
                            }
                        });


                        if (recalc)
                        {
                            _parentStrategy.ClearMemory(orderDate_local.DateTime);
                            _parentStrategy.ClearMemory(orderDate_local.DateTime);

                            if (_parentStrategy.Portfolio != null)
                                _parentStrategy.Portfolio.ClearOrders(orderDate_local.DateTime);

                            _parentStrategy.ExecuteLogic(_parentStrategy.ExecutionContext(orderDate_local));

                        }
                    }
                }
            }
        }

        /// <summary>
        /// Function: Calls PostExecuteLogic for each strategy and its sub-nodes
        /// </summary>
        /// <param name="orderDate">reference day</param>
        public void PostExecuteLogic(DateTime day)
        {
            foreach (Strategy strat in _strategyDB.Values)
                if (strat.InitialDate <= day && strat.FinalDate >= day)
                    GetTree(strat).PostExecuteLogic(day);

            BusinessDay date_local = _parentStrategy.Calendar.GetBusinessDay(day);
            if (date_local != null)
                _parentStrategy.PostExecuteLogic(date_local);
        }

        /// <summary>
        /// Function: Initialisation process for the Tree.
        /// </summary>
        /// <param name="orderDate">reference day</param>
        public void InitializeProcess(DateTime date)
        {
            InitializeProcess(date, true);
        }

        /// <summary>
        /// Function: Initialisation process for the Tree.
        /// </summary>
        /// <param name="orderDate">reference day</param>
        /// <param name="preNavCalculaiton">true if pre-nav calculation is to be performed</param>
        private void InitializeProcess(DateTime date, Boolean preNavCalculaiton)
        {
            BusinessDay date_local = _parentStrategy.Calendar.GetBusinessDay(date);

            if (preNavCalculaiton)
                PreNAVCalculation(date_local.DateTime);

            if (_parentStrategy.Portfolio != null)
                _parentStrategy.Portfolio.SubmitOrders(date_local.DateTime);
        }

        /// <summary>
        /// Function: Simulation process.
        /// </summary>
        /// <param name="date">reference date</param>
        /// <remarks>
        /// 1) ExecuteLogic
        /// 2) PostExecuteLogic
        /// 3) SubmitOrders
        /// then 3 milliseconds later
        /// 4) PreNavCalculation
        /// 5) ReceiveExecutionLevels
        /// 6) ManageCorporateActions
        /// 7) BookOrders
        /// 8) MarginFutures
        /// 9) HedgeFX
        /// 10) NAVCalculation
        /// 11) AddRemoveSubStrategies
        /// </remarks>
        public void Process(DateTime date)
        {
            if (_parentStrategy.Portfolio != null)
            {
                _parentStrategy.Tree.ExecuteLogic(date);
                _parentStrategy.Tree.PostExecuteLogic(date);

                _parentStrategy.Portfolio.SubmitOrders(date);
            }

            int d = 3;
            _parentStrategy.Tree.PreNAVCalculation(date.AddMilliseconds(d));

            if (_parentStrategy.Portfolio != null)
            {
                _parentStrategy.Portfolio.ReceiveExecutionLevels(date.AddMilliseconds(d));

                _parentStrategy.Tree.ManageCorporateActions(date.AddMilliseconds(d));

                _parentStrategy.Tree.BookOrders(date.AddMilliseconds(d));
                _parentStrategy.Tree.MarginFutures(date.AddMilliseconds(d));

                _parentStrategy.Tree.HedgeFX(date.AddMilliseconds(d));

                _parentStrategy.Tree.NAVCalculation(date.AddMilliseconds(d));
                _parentStrategy.Tree.AddRemoveSubStrategies(date.AddMilliseconds(d));
            }
        }

        /// <summary>
        /// Function: Load portfolio memory for entire tree and all history
        /// </summary>        
        public void LoadPortfolioMemory()
        {
            foreach (Strategy strat in _strategyDB.Values)
                strat.Tree.LoadPortfolioMemory();

            if (_parentStrategy.Portfolio != null)
                _parentStrategy.Portfolio.LoadPositionOrdersMemory(DateTime.MinValue, false);
        }

        /// <summary>
        /// Function: Load portfolio memory for entire tree on a given date
        /// </summary>        
        /// <param name="date">reference date</param>
        public void LoadPortfolioMemory(DateTime date)
        {
            if (_parentStrategy.Portfolio != null)
                _parentStrategy.Portfolio.LoadPositionOrdersMemory(date, false);

            foreach (Strategy strat in _strategyDB.Values)
                strat.Tree.LoadPortfolioMemory(date);
        }

        /// <summary>
        /// Function: Loading portfolio memory for entire tree on a given date from persistent memory only.
        /// always over write non-persistent memory.
        /// </summary>        
        /// <param name="date">reference date</param>
        public void LoadPortfolioMemory(DateTime date, bool force)
        {
            if (_parentStrategy.Portfolio != null)
                _parentStrategy.Portfolio.LoadPositionOrdersMemory(date, force);

            foreach (Strategy strat in _strategyDB.Values)
                strat.Tree.LoadPortfolioMemory(date, force);
        }

        /// <summary>
        /// Function: Manage corporate actions of the tree on a given date.
        /// </summary>        
        /// <param name="date">reference date</param>
        public void ManageCorporateActions(DateTime date)
        {
            foreach (Strategy strat in _strategyDB.Values)
                strat.Tree.ManageCorporateActions(date);

            if (_parentStrategy.Portfolio != null)
                _parentStrategy.Portfolio.ManageCorporateActions(date);
        }

        /// <summary>
        /// Function: Margin futures of the tree on a given date.
        /// </summary>        
        /// <param name="date">reference date</param>
        public void MarginFutures(DateTime date)
        {
            foreach (Strategy strat in _strategyDB.Values)
                strat.Tree.MarginFutures(date);

            if (_parentStrategy.Portfolio != null)
                _parentStrategy.Portfolio.MarginFutures(date);
        }

        /// <summary>
        /// Function: Hedge FX of the tree on a given date.
        /// </summary>        
        /// <param name="date">reference data</param>
        public void HedgeFX(DateTime date)
        {
            foreach (Strategy strat in _strategyDB.Values)
                strat.Tree.HedgeFX(date);

            if (_parentStrategy.Portfolio != null)
                _parentStrategy.Portfolio.HedgeFX(date);
        }

        /// <summary>
        /// Function: Book orders of the tree on a given date.
        /// </summary>        
        /// <param name="executionDay">reference date</param>
        public int BookOrders(DateTime executionDay)
        {
            int count = 0;

            foreach (Strategy strat in _strategyDB.Values)
                if (strat.InitialDate <= executionDay && strat.FinalDate >= executionDay)
                    count += strat.Tree.BookOrders(executionDay);

            if (_parentStrategy.Initialized && _parentStrategy.Portfolio != null)
            {
                List<Position> ps = _parentStrategy.Portfolio.BookOrders(executionDay);//, TimeSeriesType.Last);
                if (ps != null)
                    count += ps.Count;
            }

            return count;
        }

        /// <summary>
        /// Function: Re book orders of the tree on a given date.
        /// </summary>        
        /// <param name="executionDay">reference date</param>
        public int ReBookOrders(DateTime executionDay)
        {
            int count = 0;

            foreach (Strategy strat in _strategyDB.Values)
                if (strat.InitialDate <= executionDay && strat.FinalDate >= executionDay)
                    count += strat.Tree.ReBookOrders(executionDay);

            if (_parentStrategy.Initialized && _parentStrategy.Portfolio != null)
            {
                List<Position> ps = _parentStrategy.Portfolio.ReBookOrders(executionDay);
                if (ps != null)
                    count += ps.Count;
            }

            return count;
        }

        /// <summary>
        /// Function: Save and commit all new positions changed for this tree in persistent storage.
        /// </summary> 
        public void SaveNewPositions()
        {
            foreach (Strategy strat in _strategyDB.Values)
                strat.Tree.SaveNewPositions();

            if (_parentStrategy.Portfolio != null)
                _parentStrategy.Portfolio.SaveNewPositions();
        }

        /// <summary>
        /// Function: Save and commit all values changed for this tree in persistent storage.
        /// </summary>    
        public void Save()
        {
            foreach (Strategy strat in _strategyDB.Values)
                strat.Tree.Save();

            Console.WriteLine("Saving: " + _parentStrategy);
            _parentStrategy.Save();
        }

        /// <summary>
        /// Function: Clear the Strategy memory of the entire tree below this for a specific date. (Does not clear AUM Memory)
        /// </summary>       
        /// <param name="date">DateTime value date 
        /// </param>
        public void ClearMemory(DateTime date)
        {
            foreach (Strategy strat in _strategyDB.Values)
                strat.Tree.ClearMemory(date);

            if (_parentStrategy.Initialized)
                _parentStrategy.ClearMemory(date);
        }

        /// <summary>
        /// Function: Clear the portfolio's new orders of the entire tree below this for a specific date.
        /// </summary>       
        /// <param name="date">DateTime value date 
        /// </param>
        /// <param name="clearMemory">True if clear AUM memory also.</param>
        public void ClearOrders(DateTime orderDate, bool clearMemory)
        {
            foreach (Strategy strat in _strategyDB.Values)
                GetTree(strat).ClearOrders(orderDate, clearMemory);

            BusinessDay date_local = _parentStrategy.Calendar.GetBusinessDay(orderDate);
            if (date_local != null)
                if (_parentStrategy.Initialized)
                {
                    if (clearMemory)
                        _parentStrategy.ClearNextAUMMemory(orderDate);

                    if (_parentStrategy.Portfolio != null)
                        _parentStrategy.Portfolio.ClearOrders(orderDate);
                }
        }
    }
}