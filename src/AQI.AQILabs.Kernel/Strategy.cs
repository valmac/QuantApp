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

using System.Data;
using System.ComponentModel;
using System.Reflection;

using AQI.AQILabs.Kernel.Numerics.Util;

namespace AQI.AQILabs.Kernel
{
    /// <summary>
    /// Enumeration of possible Direction Types
    /// This value that is set for all Strategy objects.
    /// </summary>    
    /// <remarks>
    /// Long to follow a positive exposure to the underlying strategy or Short for the opposite
    /// </remarks>
    public enum DirectionType
    {
        Long = 1, Short = -1
    };

    /// <summary>
    /// Structure used by Kernel when saving MemorySeries values in memory
    /// </summary>    
    /// <remarks>
    /// Internal use by Kernel Only.
    /// </remarks>
    public struct MemorySeriesPoint
    {
        public int ID;
        public int memorytype;
        public int memoryclass;
        public DateTime date;
        public double value;

        /// <summary>
        /// Constructor: ID--Strategy ID
        /// </summary>
        /// <remarks>
        /// MemorySeriesPoints are stored in a three dimensional matrix for each Strategy.
        /// Coordinates: (date, memorytype, memoryclass) for each value in a Strategy MemorySeries.
        /// </remarks>
        /// <param name="ID">ID for Strategy</param>        
        /// <param name="memorytype">Type of memory point</param>
        /// <param name="memoryclass">Class of memory point</param>
        /// <param name="value">TimeSeries point value</param>        
        public MemorySeriesPoint(int ID, int memorytype, int memoryclass, DateTime date, double value)
        {
            this.ID = ID;
            this.memorytype = memorytype;
            this.memoryclass = memoryclass;
            this.date = date;
            this.value = value;
        }
    }

    /// <summary>
    /// Structure used by Kernel when passing Logic Execution information to the ExecuteLogic function of a Strategy.
    /// </summary>    
    /// <remarks>
    /// Internal use by Kernel Only.
    /// </remarks>
    public struct ExecutionContext
    {
        //public double PortfolioReturn;
        //public double Index_t;
        public BusinessDay OrderDate;
        public double ReferenceAUM;

        /// <summary>
        /// Constructor of Order Context reprenting a specific snap-shot.
        /// </summary>
        /// <param name="orderDate">BusinessDay for this specific snap-shot</param>                
        /// <param name="reference_aum">AUM for this specific snap-shot</param>        
        public ExecutionContext(BusinessDay orderDate, double reference_aum)
        {
            OrderDate = orderDate;
            ReferenceAUM = reference_aum;
        }
    }

    /// <summary>
    /// Strategy skeleton containing
    /// the most general functions and variables.
    /// This class also manages the connectivity
    /// to the database through a relevant Factories.
    /// </summary>
    public class Strategy : Instrument
    {
        new public static AQI.AQILabs.Kernel.Factories.IStrategyFactory Factory = null;

        /// <summary>
        /// Property containt int value for the unique ID of the instrument
        /// </summary>
        /// <remarks>
        /// Main identifier for each Instrument in the System
        /// </remarks>
        new public int ID
        {
            get
            {
                return base.ID;
            }
        }

        private bool _simulating = false;
        /// <summary>
        /// Property which is true if the strategy is currently being simulated historically.
        /// </summary>        
        public bool Simulating
        {
            get
            {
                return this._simulating;
            }
            set
            {
                this._simulating = value;
            }
        }

        /// <summary>
        /// Function: Clear the Strategy memory for a specific date. (Does not clear AUM Memory)
        /// </summary>       
        /// <param name="date">DateTime value date 
        /// </param>
        public void ClearMemory(DateTime date)
        {
            Factory.ClearMemory(this, date);
        }

        /// <summary>
        /// Function: Clear the Strategy AUM memory for a specific date.
        /// </summary>       
        /// <param name="date">DateTime value date 
        /// </param>
        public void ClearAUMMemory(DateTime date)
        {
            Factory.ClearAUMMemory(this, date);
        }

        /// <summary>
        /// Property: Tree valued reference to the execution Tree of the Strategy.
        /// </summary>
        /// <remarks>
        /// Can only be set by the Kernel.
        /// </remarks>
        [Newtonsoft.Json.JsonIgnore]
        public Tree Tree { get; private set; }

        private int _portfolioID = -10;
        private string _class = null;
        private DateTime _initialDate = DateTime.MinValue;
        private DateTime _finalDate = DateTime.MinValue;
        private string _dbConnection = null;
        private string _schedule = null;

        /// <summary>
        /// Constructor of the Strategy Class        
        /// </summary>
        /// <remarks>
        /// Only used Strategy implementations.
        /// </remarks>
        public Strategy(Instrument instrument)
            : base(instrument)
        {
            if (!SimulationObject)
                this.Cloud = instrument.Cloud;

            Factory.UpdateStrategyDB(this);
            Tree = Tree.GetTree(this);
            this._class = this.GetType().ToString();
        }

        /// <summary>
        /// Constructor of the Strategy Class        
        /// </summary>
        /// <remarks>
        /// Only used Strategy implementations.
        /// </remarks>
        public Strategy(Instrument instrument, string className)
            : base(instrument)
        {
            if (!SimulationObject)
                this.Cloud = instrument.Cloud;

            Factory.UpdateStrategyDB(this);
            Tree = Tree.GetTree(this);
            this._class = className;
        }

        /// <summary>
        /// Constructor of the Strategy Class        
        /// </summary>
        /// <remarks>
        /// Only used Strategy implementations.
        /// </remarks>
        public Strategy(int id)
            : base(id)
        {

            if (!SimulationObject)
                this.Cloud = Instrument.FindCleanInstrument(id).Cloud;

            Factory.UpdateStrategyDB(this);
            Tree = Tree.GetTree(this);
            this._class = this.GetType().ToString();
        }

        /// <summary>
        /// Property: Integer valued ID of the portfolio instance linked to this Strategy.
        /// </summary>
        public int PortfolioID
        {
            get
            {
                return _portfolioID;
            }
            set
            {
                this._portfolioID = value;
            }
        }

        private Portfolio _portfolio = null;

        /// <summary>
        /// Property: Portfolio valued reference to the portfolio instance linked to this Strategy.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Portfolio Portfolio
        {
            get
            {
                if (_portfolioID == -1 || _portfolioID == -10)
                    return null;
                else
                {
                    if (_portfolio == null)
                        _portfolio = Factory.FindPortfolio(_portfolioID);
                    return _portfolio;
                }
            }
            set
            {
                if (_portfolio != value)
                {
                    if (value == null)
                    {
                        _portfolioID = -1;
                        _portfolio = null;
                    }
                    else
                    {
                        _portfolioID = value.ID;
                        _portfolio = value;
                    }
                    if (!SimulationObject)
                    {
                        if (Factory != null)
                            Factory.SetProperty(this, "PortfolioID", _portfolioID);
                    }
                }
            }
        }

        /// <summary>
        /// Property: DateTime valued reference to the initial date of the strategy (t=0).
        /// </summary>
        /// <remarks>
        /// Used by Kernel when serializing object.
        /// </remarks>
        public DateTime InitialDateMemory
        {
            get
            {
                return _initialDate;
            }
            set
            {
                _initialDate = value;
            }
        }

        /// <summary>
        /// Property: DateTime valued reference to the final date the strategy.
        /// </summary>
        /// <remarks>
        /// Used by Kernel when serializing object.
        /// </remarks>
        public DateTime FinalDateMemory
        {
            get
            {
                return _finalDate;
            }
            set
            {
                _finalDate = value;
            }
        }

        /// <summary>
        /// Property: string valued reference to the connection string for the persistent storage functionality.
        /// </summary>
        /// <remarks>
        /// Used by Kernel when serializing object.
        /// </remarks>
        public string DBConnectionMemory
        {
            get
            {
                return _dbConnection;
            }
            set
            {
                _dbConnection = value;
            }
        }

        /// <summary>
        /// Property: String valued reference to the scheduling information.
        /// </summary>
        public string ScheduleCommandMemory
        {
            get
            {
                return _schedule;
            }
            set
            {
                this._schedule = value;
            }
        }

        /// <summary>
        /// Delegate: skeleton for custom calculation functions called from the scheduler
        /// </summary>
        public delegate void Calculation(DateTime date);
        public Calculation JobCalculation = null;
        private StrategyJobExecutor _jobExecutor = null;

        /// <summary>
        /// Function: start scheduler according the ScheduleCommand instructions
        /// </summary>
        public void StartScheduler()
        {
            if (ScheduleCommand != null)
            {
                _jobExecutor = new StrategyJobExecutor(this);

                string[] commands = ScheduleCommand.Split(new char[] { ';' });

                foreach (string command in commands)
                    if (!string.IsNullOrWhiteSpace(command))
                        _jobExecutor.StartJob(command.Replace(";", ""));
            }
        }

        /// <summary>
        /// Function: re-start scheduler according the ScheduleCommand instructions
        /// </summary>
        public void ReStartScheduler()
        {
            if (ScheduleCommand != null && _jobExecutor != null)
            {
                _jobExecutor.StopJob();

                string[] commands = ScheduleCommand.Split(new char[] { ';' });

                foreach (string command in commands)
                    if (!string.IsNullOrWhiteSpace(command))
                        _jobExecutor.StartJob(command.Replace(";", ""));
            }
        }

        /// <summary>
        /// Function: stop scheduler
        /// </summary>
        public void StopScheduler()
        {
            if (_jobExecutor != null)
                _jobExecutor.StopJob();
        }

        /// <summary>
        /// Property: string valued reference to class name of the Strategy.
        /// </summary>
        /// <remarks>
        /// Used by Kernel when serializing object.
        /// </remarks>
        public string ClassMemory
        {
            get
            {
                return _class;
            }
            set
            {
                this._class = value;
            }
        }


        /// <summary>
        /// Property: string valued reference to class name of the Strategy.
        /// </summary>
        /// <remarks>
        /// Used by Kernel.
        /// </remarks>
        public string Class
        {
            get
            {
                return _class;
            }
            set
            {
                this._class = value;
                if (!SimulationObject)
                    if (Factory != null)
                        Factory.SetProperty(this, "Class", value); ;
            }
        }

        /// <summary>
        /// Property: DateTime valued reference to the initial date of the strategy (t=0).
        /// </summary>
        public DateTime InitialDate
        {
            get
            {
                return _initialDate;
            }
            set
            {
                _initialDate = value;
                if (!SimulationObject)
                {
                    if (Factory != null)
                        Factory.SetProperty(this, "InitialDate", value);
                }
            }
        }

        /// <summary>
        /// Property: DateTime valued reference to the final date the strategy.
        /// </summary>
        public DateTime FinalDate
        {
            get
            {
                if (SimulationObject && _finalDate == DateTime.MinValue)
                    _finalDate = DateTime.MaxValue;

                if (_finalDate == DateTime.MinValue && !SimulationObject)
                {
                    if (_finalDate == DateTime.MinValue)
                        _finalDate = DateTime.MaxValue;
                }

                return _finalDate;
            }
            set
            {
                _finalDate = value;
                if (!SimulationObject)
                {
                    if (Factory != null)
                        Factory.SetProperty(this, "FinalDate", value);
                }
            }
        }

        /// <summary>
        /// Property: String valued reference to the scheduling information.
        /// </summary>
        public string ScheduleCommand
        {
            get
            {
                return _schedule;
            }
            set
            {
                this._schedule = value;
                if (!SimulationObject)
                    if (Factory != null)
                        Factory.SetProperty(this, "Scheduler", value); ;
            }
        }

        /// <summary>
        /// Property: string valued reference to the connection string for the persistent storage functionality.
        /// </summary>        
        public string DBConnection
        {
            get
            {
                return string.IsNullOrEmpty(_dbConnection) ? "DefaultStrategy" : _dbConnection;
            }
            set
            {
                _dbConnection = value;
                if (!SimulationObject)
                {
                    if (Factory != null)
                        Factory.SetProperty(this, "DBConnection", value);
                }
            }
        }

        /// <summary>
        /// Function: Remove the Strategy from the persistent storage.
        /// </summary>            
        new public void Remove()
        {
            if (Portfolio != null)
            {
                Portfolio.Remove();
                Portfolio = null;
            }

            Factory.Remove(this);
            base.Remove();
        }

        /// <summary>
        /// Function: Remove strategy data string from and including a given date.
        /// </summary>         
        /// <param name="date">reference date.
        /// </param>
        new public void RemoveFrom(DateTime date)
        {
            base.RemoveFrom(date);
            if (Portfolio != null)
                Portfolio.RemoveFrom(date);

            Factory.RemoveFrom(this, date);
        }

        /// <summary>
        /// Function: Save and commit all values changed for this Instrument in persistent storage.
        /// </summary>            
        public override void Save()
        {
            base.Save();
            if (!SimulationObject)
                Factory.Save(this);
        }

        /// <summary>
        /// Function: Retrieve memory series object.
        /// </summary>       
        /// <param name="memorytype">Memory series type of object to be retrieved.
        /// </param>
        /// <param name="memoryclass">Memory series class of object to be retrieved.
        /// </param>
        public TimeSeries GetMemorySeries(int memorytype, int memoryclass)
        {
            return Factory.GetMemorySeries(this, memorytype, memoryclass);
        }

        /// <summary>
        /// Function: Retrieve memory series object.
        /// </summary>       
        /// <param name="memorytype">Memory series type of object to be retrieved.
        /// </param>
        public TimeSeries GetMemorySeries(int memorytype)
        {
            return GetMemorySeries(memorytype, _max_id_do_not_use);
        }

        /// <summary>
        /// Function: Retrieve dictionary with all memory series objects index by a pair of integers
        /// representing the [memorytype, memoryclass].
        /// </summary>       
        public Dictionary<int[], TimeSeries> GetMemorySeries()
        {
            return Factory.GetMemorySeries(this);
        }

        /// <summary>
        /// Function: Retrieve value from memory series object.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the value to be retrieved.
        /// </param>
        /// <param name="memorytype">Memory series type of object to be retrieved.
        /// </param>
        /// <param name="memoryclass">Memory series class of object to be retrieved.
        /// </param>
        /// <param name="timeSeriesRoll">Roll type of reference time series object.
        /// </param>
        public double this[DateTime date, int memorytype, int memoryclass, TimeSeriesRollType timeSeriesRoll]
        {
            get
            {
                return Factory.GetMemorySeriesPoint(this, date, memorytype, memoryclass, timeSeriesRoll);
            }
        }

        /// <summary>
        /// Function: Retrieve value from memory series object.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the value to be retrieved.
        /// </param>
        /// <param name="memorytype">Memory series type of object to be retrieved.
        /// </param>
        /// <param name="memoryclass">Memory series class of object to be retrieved.
        /// </param>
        public double this[DateTime date, int memroytype, int memroyclass]
        {
            get
            {
                return this[date, memroytype, memroyclass, TimeSeriesRoll];
            }
        }

        /// <summary>
        /// Function: Retrieve value from memory series object.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the value to be retrieved.
        /// </param>
        /// <param name="memorytype">Memory series type of object to be retrieved.
        /// </param>
        /// <param name="timeSeriesRoll">Roll type of reference time series object.
        /// </param>
        public double this[DateTime date, int memorytype, TimeSeriesRollType timeSeriesRoll]
        {
            get
            {
                return this[date, memorytype, _max_id_do_not_use, timeSeriesRoll];
            }
        }

        /// <summary>
        /// Function: Retrieve value from memory series object.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the value to be retrieved.
        /// </param>
        /// <param name="memorytype">Memory series type of object to be retrieved.
        /// </param>
        public double this[DateTime date, int memroytype]
        {
            get
            {
                return this[date, memroytype, _max_id_do_not_use, TimeSeriesRoll];
            }
        }


        /// <summary>
        /// Function: Add value to memory series object.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the value to be retrieved.
        /// </param>
        /// <param name="value">Value to be added to the time series object.
        /// </param>
        /// <param name="memorytype">Memory series type of object to be retrieved.
        /// </param>
        /// <param name="memoryclass">Memory series class of object to be retrieved.
        /// </param>
        public void AddMemoryPoint(DateTime date, double value, int memorytype, int memoryclass)
        {
            Factory.AddMemoryPoint(this, date, value, memorytype, memoryclass, false);
        }

        public void AddMemoryPoint(DateTime date, double value, int memorytype, int memoryclass, Boolean onlyMemory)
        {
            Factory.AddMemoryPoint(this, date, value, memorytype, memoryclass, onlyMemory);
        }

        /// <summary>
        /// Function: Add value to memory series object.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the value to be retrieved.
        /// </param>
        /// <param name="value">Value to be added to the time series object.
        /// </param>
        /// <param name="memorytype">Memory series type of object to be retrieved.
        /// </param>
        public void AddMemoryPoint(DateTime date, double value, int memorytype)
        {
            AddMemoryPoint(date, value, memorytype, _max_id_do_not_use, false);
        }
        public void AddMemoryPoint(DateTime date, double value, int memorytype, Boolean onlyMemory)
        {
            AddMemoryPoint(date, value, memorytype, _max_id_do_not_use, onlyMemory);
        }

        public static int _aum_id_do_not_use = -1010991803;
        public static int _aum_chg_id_do_not_use = -1010991813;
        public static int _aum_ord_chg_id_do_not_use = -1010991843;
        public static int _universe_id_do_not_use = -1010991823;
        public static int _max_id_do_not_use = -1010991833;

        /// <summary>
        /// Function: Retrieve instrument universe.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the universe to be retrieved.
        /// </param>        
        public new Dictionary<int, Instrument> Instruments(DateTime date, bool aggregated)
        {
            Dictionary<int, Instrument> instruments = new Dictionary<int, Instrument>();

            if (aggregated)
                InternalInstruments(date, ref instruments);
            else
                for (int ii = 0; ; ii++)
                {
                    double underlyingID = this[date, _universe_id_do_not_use, -ii, TimeSeriesRollType.Last];
                    if (double.IsNaN(underlyingID) || underlyingID == double.MinValue || underlyingID == double.MaxValue)
                        break;
                    Instrument instrument = Instrument.FindInstrument((int)underlyingID);
                    if (instrument != null)
                        instruments.Add(instrument.ID, instrument);
                }

            return instruments;
        }

        private void InternalInstruments(DateTime date, ref Dictionary<int, Instrument> instruments)
        {
            for (int ii = 0; ; ii++)
            {
                double underlyingID = this[date, _universe_id_do_not_use, -ii, TimeSeriesRollType.Last];
                if (double.IsNaN(underlyingID) || underlyingID == double.MinValue || underlyingID == double.MaxValue)
                    break;
                Instrument instrument = Instrument.FindInstrument((int)underlyingID);
                if (instrument != null)
                {
                    if (instrument.InstrumentType == Kernel.InstrumentType.Strategy)
                        (instrument as Strategy).InternalInstruments(date, ref instruments);
                    else if (!instruments.ContainsKey(instrument.ID))
                        instruments.Add(instrument.ID, instrument);
                }
            }
        }

        /// <summary>
        /// Function: Add instrument to instrumnt universe.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the universe to be added.
        /// </param>
        public void AddInstrument(Instrument instrument, DateTime date)
        {
            Dictionary<int, Instrument> instruments = Instruments(date, false);

            if (!instruments.ContainsKey(instrument.ID))
                this.AddMemoryPoint(date, instrument.ID, _universe_id_do_not_use, -instruments.Count);

            if (instrument.InstrumentType == Kernel.InstrumentType.Strategy)
                this.Tree.AddSubStrategy((Strategy)instrument);
        }

        /// <summary>
        /// Function: Remove instrument from instrumnt universe.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the universe to be removed.
        /// </param>
        public void RemoveInstrument(Instrument instrument, DateTime date)
        {
            Dictionary<int, Instrument> instruments = Instruments(date, false);

            if (instruments.ContainsKey(instrument.ID))
            {
                instruments.Remove(instrument.ID);
                //_quickInstruments[date].Remove(instrument.ID);
                int j = 0;
                foreach (Instrument i in instruments.Values)
                {
                    this.AddMemoryPoint(date, i.ID, _universe_id_do_not_use, -j);
                    j++;
                }

                this.AddMemoryPoint(date, double.MaxValue, _universe_id_do_not_use, -j);
            }
        }

        /// <summary>
        /// Function: Remove instrument from instrumnt universe.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the universe to be removed.
        /// </param>
        public void RemoveInstruments(DateTime date)
        {
            Dictionary<int, Instrument> instruments = Instruments(date, false);

            for (int i = 0; i < instruments.Count; i++)
                this.AddMemoryPoint(date, double.MaxValue, _universe_id_do_not_use, -i);
        }

        /// <summary>
        /// Function: Retreive next trading date in relation to a given date.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the next trading date to be retrieved.
        /// </param>
        public DateTime NextTradingDate(DateTime date)
        {
            return NextTradingBusinessDate(date).DateTime;
        }

        /// <summary>
        /// Function: Retreive next trading date in relation to a given date.
        /// </summary>       
        /// <param name="date">BusinessDay value representing the date of the next trading date to be retrieved.
        /// </param>
        public BusinessDay NextTradingBusinessDate(DateTime date)
        {
            return Calendar.NextTradingBusinessDate(date);
        }

        /// <summary>
        /// Function: Retreive assets under management (AUM) in relation to a given date.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the AUM to be retrieved.
        /// </param>
        /// <param name="ttype">Type of time series object.
        /// </param>
        public double GetAUM(DateTime date, TimeSeriesType ttype)
        {
            double value = this[date, _aum_id_do_not_use, (int)ttype, TimeSeriesRollType.Last];

            return value;
        }

        /// <summary>
        /// Function: Retreive assets under management (AUM) in relation to a given date.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the AUM to be retrieved.
        /// </param>
        /// <param name="ttype">Type of time series object.
        /// </param>
        public double GetAUMChange(DateTime date, TimeSeriesType ttype)
        {
            double value = this[date, _aum_chg_id_do_not_use, (int)ttype, TimeSeriesRollType.Last];

            if (double.IsNaN(value))
                value = 0;

            return value;
        }

        /// <summary>
        /// Function: Retreive assets under management (AUM) in relation to a given date.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the AUM to be retrieved.
        /// </param>
        /// <param name="ttype">Type of time series object.
        /// </param>
        public double GetOrderAUMChange(DateTime date, TimeSeriesType ttype)
        {
            double value = this[date, _aum_ord_chg_id_do_not_use, (int)ttype, TimeSeriesRollType.Last];

            if (double.IsNaN(value))
                value = 0;

            return value;
        }

        /// <summary>
        /// Function: Retreive assets under management (AUM) in relation to a given date.
        /// </summary>       
        /// <param name="start">DateTime value representing the date of the AUM to be retrieved.
        /// </param>
        /// /// <param name="end">DateTime value representing the date of the AUM to be retrieved.
        /// </param>
        /// <param name="ttype">Type of time series object.
        /// </param>
        public double GetAggregegatedAUMChanges(DateTime start, DateTime end, TimeSeriesType ttype)
        {
            TimeSeries ts = this.GetMemorySeries(_aum_chg_id_do_not_use, (int)ttype);
            if (ts != null && ts.Count > 0)
                ts = ts.GetRange(start, end, TimeSeries.RangeFillType.None);

            if (ts != null && ts.Count > 0)
            {
                double res = 0;
                for (int i = 0; i < ts.Count; i++)
                {
                    double val = ts.Data[i];
                    if (!double.IsInfinity(val) && !double.IsNaN(val))
                        res += val;
                }

                return res;
            }

            return 0;
        }

        /// <summary>
        /// Function: Retreive assets under management (AUM) in relation
        /// to the next business date of given date.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the AUM to be retrieved.
        /// </param>
        public double GetNextAUM(DateTime date, TimeSeriesType ttype)
        {
            double chg = GetOrderAUMChange(date, ttype);
            return GetAUM(date, ttype) + (double.IsNaN(chg) || double.IsInfinity(chg) ? 0 : chg);
        }


        /// <summary>
        /// Function: Clear the Strategy AUM memory on the next trading date in relation to a specific date.
        /// </summary>       
        /// <param name="date">DateTime valued date 
        /// </param>
        public void ClearNextAUMMemory(DateTime date)
        {
            ClearAUMMemory(date);
        }

        /// <summary>
        /// Function: Update the AUM of the strategy.
        /// </summary>       
        /// <param name="orderDate">DateTime valued date 
        /// </param>
        /// <param name="aumValue">double valued AUM to be updated
        /// </param>
        /// <param name="UpdatePortfolio">If true --> generate orders proportionate to the AUM change.
        /// Otherwise only update AUM value
        /// </param>
        public virtual void UpdateAUMOrder(DateTime orderDate, double aumValue)//, bool UpdatePortfolio)
        {
            double oldAUM = this.GetAUM(orderDate, TimeSeriesType.Last);
            if (double.IsNaN(oldAUM))
                oldAUM = 0;

            double chgAUM = aumValue - oldAUM;

            if (this.Portfolio != null)
            {
                this.Portfolio.UpdateNotionalOrder(orderDate, aumValue, TimeSeriesType.Last);

                Dictionary<int, Dictionary<string, Order>> orders = this.Portfolio.OpenOrders(orderDate, false);
                List<Position> pos = this.Portfolio.RiskPositions(orderDate, false);

                List<Order> res = new List<Order>();

                if (orders != null)
                    foreach (int orderKeys in orders.Keys.ToList())
                        foreach (string key in orders[orderKeys].Keys)
                        {
                            Order order = orders[orderKeys][key];
                            if (order.Instrument.InstrumentType == Kernel.InstrumentType.Strategy)
                                res.Add(order);
                            else if (order.Unit != 0)
                                res.Add(order);
                        }

                if ((res == null || (res != null && res.Count == 0)) && !(pos == null || (pos != null && pos.Count == 0)))
                    return;

            }

            this.AddMemoryPoint(orderDate, chgAUM, _aum_ord_chg_id_do_not_use, (int)TimeSeriesType.Last);
        }

        /// <summary>
        /// Function: Update the AUM of the strategy.
        /// </summary>       
        /// <param name="orderDate">DateTime valued date 
        /// </param>
        /// <param name="aumValue">double valued AUM to be updated
        /// </param>
        /// <param name="UpdatePortfolio">If true --> generate positions proportionate to the AUM change.
        /// Otherwise only update AUM value.
        /// </param>
        public virtual void UpdateAUM(DateTime date, double aumValue, bool UpdatePortfolio)
        {
            aumValue = double.IsNaN(aumValue) ? 0.0 : aumValue;

            if (this.Portfolio != null && UpdatePortfolio)
                this.Portfolio.UpdateNotional(date, aumValue);

            // This needs to be after the UpdateNotional since the position update needs to be based on the previous "next update"
            this.AddMemoryPoint(date, aumValue, _aum_id_do_not_use, (int)TimeSeriesType.Last);
            this.AddMemoryPoint(date, 0, Strategy._aum_chg_id_do_not_use, (int)TimeSeriesType.Last);
        }


        /// <summary>
        /// Function: Set the direction type of the strategy
        /// </summary>       
        /// <param name="date">DateTime valued date 
        /// </param>
        /// <param name="sign">Long or Short
        /// </param>
        /// </param>
        public virtual void Direction(DateTime date, DirectionType sign) { }

        /// <summary>
        /// Function: Get the direction type of the strategy
        /// </summary>       
        /// <param name="date">DateTime valued date 
        /// </param>
        /// </param>
        public virtual DirectionType Direction(DateTime date) { return DirectionType.Long; }


        private Boolean _initialized = false;

        /// <summary>
        /// Property: True if Strategy has been initialized during runtime.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Boolean Initialized
        {
            get
            {
                return _initialized;
            }
            private set
            {
                _initialized = value;
            }
        }

        /// <summary>
        /// Function: Initialize the strategy during runtime.
        /// </summary>       
        public virtual void Initialize()
        {
            if (Initialized)
                return;

            if (Portfolio != null && Portfolio.Reserves != null)
                foreach (Instrument ins in Portfolio.Reserves)
                    this.Tree.AddSubStrategy((Strategy)ins);


            Dictionary<int, Instrument> instruments = this.Instruments(DateTime.Now, false);

            foreach (Instrument instrument in instruments.Values)
            {
                if (instrument.InstrumentType == AQILabs.Kernel.InstrumentType.Strategy)
                {

                    Strategy strategy = Strategy.FindStrategy(instrument);
                    if (strategy != null)
                        this.Tree.AddSubStrategy(strategy);
                }
            }
            Initialized = true;
        }

        /// <summary>
        /// Delegate: Skeleton for a delegate function to create a custom ExecuteLogic procedure
        /// </summary>       
        /// <param name="strategy">reference strategy
        /// </param>
        /// <param name="context">Context containing relevant environment information for the logic execution.
        /// </param>
        public delegate void ExecuteLogicType(Strategy strategy, ExecutionContext context);
        public ExecuteLogicType ExecuteLogicFunction = null;

        /// <summary>
        /// Function: Virtual function implemented by the Strategy developer.
        /// </summary>
        /// <param name="context">context of Order Generation Calculation.
        /// </param>
        public virtual void ExecuteLogic(ExecutionContext context)
        {
            if (ExecuteLogicFunction != null)
                ExecuteLogicFunction(this, context);
        }

        /// <summary>
        /// Function: Abstract function implemented by the Strategy developer
        /// returning a string array of the Memory type names.
        /// </summary>
        public virtual string[] MemoryTypeNames()
        {
            return null;
        }

        /// <summary>
        /// Function: Abstract function implemented by the Strategy developer
        /// returning an integer linked to the Memory name.
        /// </summary>
        public virtual int MemoryTypeInt(string name)
        {
            return int.MinValue;
        }

        /// <summary>
        /// Function: virtual function that calculates the NAV of the Strategy.
        /// </summary>
        /// <remarks>
        /// This function is called by default unless the Strategy developer
        /// overrides it for custom functionality.
        /// </remarks>
        public virtual double NAVCalculation(BusinessDay date)
        {
            //this.AddRemoveSubStrategies(date);
            ///////////////////////////////////////////////////
            // Index Calculation
            /////////////////////////////////////////////////// 

            double portvalue_mid = Portfolio[date.DateTime, TimeSeriesType.Last, DataProvider.DefaultProvider, Portfolio.TimeSeriesRoll];
            if (double.IsNaN(portvalue_mid))
                portvalue_mid = 0;

            double index_t_1 = this[date.AddMilliseconds(-1).DateTime, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last];
            double aum_value = this.GetAUM(date.DateTime, TimeSeriesType.Last);

            if (double.IsNaN(index_t_1))
                index_t_1 = this[date.DateTime, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last];

            if (this.FundingType == FundingType.ExcessReturn)
                portvalue_mid += aum_value;

            double portfolio_return = portvalue_mid - aum_value;

            double index_t = portfolio_return + index_t_1;
            ///////////////////////////////////////////////////

            double target_aum = aum_value;

            // Clean any residual from performance when strategy aum is set to 0
            if (target_aum <= 0)
            {
                Portfolio.UpdateReservePosition(date.DateTime, -portvalue_mid, Portfolio.Currency);
                if (Portfolio.ParentPortfolio != null)
                    Portfolio.ParentPortfolio.UpdateReservePosition(date.DateTime, portvalue_mid, Portfolio.Currency);
                portvalue_mid = 0;
            }


            // Store Portfolio Value prior to rebalancing to today
            CommitNAVCalculation(date, index_t, TimeSeriesType.Last);
            UpdateAUM(date.DateTime, portvalue_mid, false);

            return portvalue_mid;
        }

        /// <summary>
        /// Function: virtual function called by the Execution Tree after the Order Generation function
        /// has been called for each strategy in the tree.
        /// </summary>
        /// <remarks>
        /// This function is called by default unless the Strategy developer
        /// overrides it for custom functionality.
        /// </remarks>
        public virtual void PostExecuteLogic(BusinessDay orderDate)
        {
        }

        /// <summary>
        /// Function: protected function called when commiting a NAV value.
        /// </summary>
        /// <remarks>
        /// Only called by Kernel or in the custom NAV calculation function.
        /// </remarks>
        protected void CommitNAVCalculation(BusinessDay date, double value, TimeSeriesType type)
        {
            if (type == TimeSeriesType.High || type == TimeSeriesType.Low)
                throw new Exception("Strategy High or Low is not implemented");

            AddTimeSeriesPoint(date.DateTime, value, type, DataProvider.DefaultProvider);
        }

        /// <summary>
        /// Function: Startup function called once during the creation of the strategy.
        /// If the strategy is persistently stored, this should only be called at creation.
        /// </summary>        
        public virtual void Startup(BusinessDay initialDate, double initialValue, Portfolio portfolio)
        {
            if (!SimulationObject)
                Factory.Startup(this);

            if (portfolio != null)
            {
                Portfolio = portfolio;
                Portfolio.Strategy = this;
            }
            CommitNAVCalculation(initialDate, initialValue, TimeSeriesType.Last);

            ///////////////////////////////////            

            if (portfolio != null)
            {
                foreach (Instrument ins in Portfolio.Reserves)
                {
                    ((Strategy)ins).Initialize();
                    this.Tree.AddSubStrategy((Strategy)ins);
                }

                double val = portfolio[initialDate.DateTime];
                Portfolio.UpdateReservePosition(initialDate.DateTime, initialValue - val, Currency);
                this.UpdateAUM(initialDate.DateTime, initialValue, true);

                CommitNAVCalculation(initialDate, Math.Abs(portfolio[initialDate.DateTime]), TimeSeriesType.Last);

                this.UpdateAUM(initialDate.DateTime, portfolio[initialDate.DateTime], true);
            }

            Initialize();

            this.AddRemoveSubStrategies(initialDate);
        }

        /// <summary>
        /// Function: Find strategy by name in both memory and persistent storage
        /// </summary>       
        /// <param name="name">string valued strategy name.
        /// </param>
        public static Strategy FindStrategy(string name)
        {
            return Factory.FindStrategy(name);
        }

        /// <summary>
        /// Delegate: Skeleton for a delegate function to customely create an instance of a strategy of a specific class linked to a base instrument.
        /// </summary>       
        /// <param name="instrument">Instrument valued base instrument.
        /// </param>
        /// <param name="classname">string valued name of the Strategy class implemention.
        /// </param>
        /// <param name="portfolioID">integer valued ID of the strategy's portfolio.
        /// </param>
        /// <param name="initialDate">DateTime value of the initial date.
        /// </param>
        /// <param name="finalDate">DateTime value of the final date.
        /// </param>
        /// <param name="dbConnection">string value of the connection address to the persistent storage.
        /// </param>
        public delegate Strategy LoadStrategyEvent(Instrument instrument, string classname, int portfolioID, DateTime initialDate, DateTime finalDate, string dbConnection);
        public static event LoadStrategyEvent StrategyLoader;

        /// <summary>
        /// Function: Create an instance of a strategy of a specific class linked to a base instrument.
        /// </summary>       
        /// <param name="instrument">Instrument valued base instrument.
        /// </param>
        /// <param name="classname">string valued name of the Strategy class implemention.
        /// </param>
        /// <param name="portfolioID">integer valued ID of the strategy's portfolio.
        /// </param>
        /// <param name="initialDate">DateTime value of the initial date.
        /// </param>
        /// <param name="finalDate">DateTime value of the final date.
        /// </param>
        /// <param name="dbConnection">string value of the connection address to the persistent storage.
        /// </param>
        public static Strategy LoadStrategy(Instrument instrument, string classname, int portfolioID, DateTime initialDate, DateTime finalDate, string dbConnection, string scheduler)
        {
            if (StrategyLoader == null)
            {
                string assemblyname = classname.Substring(0, classname.LastIndexOf("."));
                Assembly assembly = Assembly.Load(assemblyname);
                Type type = assembly.GetType(classname);
                Strategy strategy = (Strategy)Activator.CreateInstance(type, new Object[] { instrument });

                strategy.PortfolioID = portfolioID;
                strategy.InitialDateMemory = initialDate;
                strategy.FinalDateMemory = finalDate;
                strategy.DBConnectionMemory = dbConnection;
                strategy.ScheduleCommandMemory = scheduler;

                return strategy;
            }
            else
                return (Strategy)StrategyLoader(instrument, classname, portfolioID, initialDate, finalDate, dbConnection);
        }

        public delegate Dictionary<int[], TimeSeries> CloneFilter(Dictionary<int[], TimeSeries> memory);
        public CloneFilter CloneFilterFunction = null;

        /// <summary>
        /// Function: Create a clone of this strategy.
        /// </summary>        
        /// <param name="portfolioClone">Clone of the base strategy's portfolio</param>
        /// <param name="initialDate">Clone's initialDate</param>
        /// <param name="finalDate">Clone's finalDate</param>
        /// <param name="cloned">internal table of previously cloned base ids and respective cloned strategies</param>
        /// <param name="simulated">true if the strategy is simulated and not persistent</param>
        internal Strategy Clone(Portfolio portfolioClone, DateTime initialDate, DateTime finalDate, Dictionary<int, Strategy> cloned, bool simulated)
        {
            Console.WriteLine("CLONE: " + this);

            string assemblyname = Class.Substring(0, Class.LastIndexOf("."));
            Strategy clone = LoadStrategy(this.Clone(simulated) as Instrument, Class, portfolioClone != null ? portfolioClone.ID : -1, initialDate, finalDate, DBConnection, ScheduleCommand);

            if (portfolioClone != null)
                portfolioClone.Strategy = clone;

            Dictionary<int[], TimeSeries> memory = GetMemorySeries();
            if (CloneFilterFunction != null)
                memory = CloneFilterFunction(memory);

            Dictionary<int[], TimeSeries> memory_new = new Dictionary<int[], TimeSeries>();

            foreach (int[] key in memory.Keys)
            {
                if (key[0] != _aum_ord_chg_id_do_not_use && key[1] != _aum_chg_id_do_not_use)
                {
                    TimeSeries ts_New = new TimeSeries(memory[key]);
                    memory_new.Add(key, ts_New);
                }
            }

            foreach (int[] key in memory_new.Keys)
            {
                for (int i = 0; i < memory_new[key].Count; i++)
                {
                    double v = memory_new[key][i];
                    if (cloned.Keys.Contains((int)v))
                        memory_new[key][i] = cloned[(int)v].ID;
                }

                int key0 = cloned.Keys.Contains(key[0]) ? cloned[key[0]].ID : key[0];
                int key1 = cloned.Keys.Contains(key[1]) ? cloned[key[1]].ID : key[1];

                Factory.AddMemorySeries(clone, key0, key1, memory_new[key]);
            }

            if (this.Portfolio == null)
                clone.Startup(Calendar.GetClosestBusinessDay(initialDate, TimeSeries.DateSearchType.Previous), this.GetTimeSeries(TimeSeriesType.Last).Values[0], portfolioClone);

            return clone;
        }


        /// <summary>
        /// Function: Find strategy by base instrument in both memory and persistent storage
        /// </summary>       
        /// <param name="instrument">Instrument valued base instrument.
        /// </param>
        public static Strategy FindStrategy(Instrument instrument)
        {
            return Factory.FindStrategy(instrument);
        }

        /// <summary>
        /// Function: Generate the order context for a specific order date.
        /// </summary>
        /// <param name="orderDate">BusinessDay valued date.
        /// </param>
        public ExecutionContext ExecutionContext(BusinessDay orderDate)
        {
            // Add Active Strategies
            foreach (Strategy s in Tree.SubStrategies)
            {
                if (!Portfolio.IsReserve(s))
                {
                    Position position = Portfolio.FindPosition(s, orderDate.DateTime);

                    if (position != null && !ActiveStrategies.Contains(s))
                        ActiveStrategies.Add(s);
                }
            }


            //double index_t = this[orderDate.DateTime, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last];            
            //double aum_value = Portfolio[orderDate.DateTime, TimeSeriesType.Last, DataProvider.DefaultProvider, Portfolio.TimeSeriesRoll];
            double aum_value = this.GetAUM(orderDate.DateTime, TimeSeriesType.Last);
            //double aum_chg = this.GetAUMChange(orderDate.DateTime.AddMilliseconds(1), TimeSeriesType.Last);
            double aum_chg = this.GetOrderAUMChange(orderDate.DateTime, TimeSeriesType.Last);
            //double aum_chg = this.GetAUMChange(NextTradingDate(orderDate.DateTime), TimeSeriesType.Last);
            aum_value += double.IsNaN(aum_chg) || double.IsInfinity(aum_chg) ? 0 : aum_chg;
            return new ExecutionContext(orderDate, aum_value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public List<Strategy> ActiveStrategies = new List<Strategy>();

        /// <summary>
        /// Function: Add a new a strategy that is not in the execution tree if the new strategy's start date allows it.
        /// Or remove a strategy in the execution tree that has expired according to it's final date.
        /// </summary>
        /// <remarks>
        /// This function will create positions in this strategy's portfolio when relevant and aggregate
        /// the new strategy's positions to this strategy's portfolio.
        /// When removing the strategy, this function will remove aggregated positions linked to the removed strategy
        /// from this strategy's portfolio.
        /// </remarks>
        /// <param name="date">BusinessDay valued date.
        /// </param>
        public virtual void AddRemoveSubStrategies(BusinessDay date)
        {
            Dictionary<int, Instrument> instruments = this.Instruments(date.DateTime, false);
            if (instruments != null)
                foreach (Instrument instrument in instruments.Values)
                {
                    if (instrument.InstrumentType == Kernel.InstrumentType.Strategy)
                    {
                        Strategy strategy = instrument as Strategy;
                        if (strategy != null && !this.Tree.ContainsStrategy(strategy))
                            this.Tree.AddSubStrategy(strategy);
                    }
                }

            if (Portfolio != null)
                foreach (Strategy s in Tree.SubStrategies)
                {
                    if (!Portfolio.IsReserve(s))
                    {
                        Position position = Portfolio.FindPosition(s, date.DateTime);
                        if (position == null)
                        {
                            double aum = s.GetNextAUM(date.DateTime, TimeSeriesType.Last);
                            if (s.Portfolio == null)
                                aum = s[date.DateTime, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last];
                            if (aum > 0.0 && date.DateTime >= s.InitialDate && date.DateTime < s.FinalDate)
                            {
                                ActiveStrategies.Add(s);
                                s.Initialize();

                                double value = (s.Portfolio != null ? s.GetAUM(date.DateTime, TimeSeriesType.Last) : s[date.DateTime, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last]);

                                Portfolio.CreatePosition(s, date.DateTime, 1.0, value);

                                SystemLog.Write("ADD: " + s + " " + this + " AUM: " + aum + " " + Portfolio[date.DateTime] + " " + date.DateTime);

                                if (s.Portfolio != null)
                                {
                                    s.Portfolio.MarginFutures(date.DateTime);
                                    s.Portfolio.HedgeFX(date.DateTime);
                                }
                            }
                        }
                        else
                        {
                            if (!ActiveStrategies.Contains(s))
                                ActiveStrategies.Add(s);

                            if (date.DateTime >= s.FinalDate)
                            {
                                ActiveStrategies.Remove(s);

                                double value = (s.Portfolio != null ? s.Portfolio[date.DateTime, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last] : s[date.DateTime, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last]);

                                if (s.Portfolio != null)
                                {
                                    s.Portfolio.MarginFutures(date.DateTime);

                                    List<Position> positions = s.Portfolio.Positions(date.DateTime);
                                    if (positions != null)
                                        foreach (Position p in positions)
                                            if (!s.Portfolio.IsReserve(p.Instrument))
                                                p.UpdatePosition(date.DateTime, 0, p.Instrument[date.DateTime, TimeSeriesType.Last, TimeSeriesRollType.Last], RebalancingType.Reserve, UpdateType.OverrideNotional);
                                    s.Portfolio.HedgeFX(date.DateTime);
                                }

                                Portfolio.UpdateReservePosition(date.DateTime, position.Value(date.DateTime), Currency);
                                position.UpdatePosition(date.DateTime, 0, double.NaN, RebalancingType.Reserve, UpdateType.OverrideNotional);
                                SystemLog.Write("Remove Strategy: " + s + " " + date.DateTime.ToShortDateString() + " " + value);
                            }
                        }
                    }
                }
        }

        /// <summary>
        /// Function: virtual function implemented by strategy developers to manage a local strategy investment universe.
        /// </summary>
        /// <param name="strategy">Strategy to be added or removed.
        /// </param>
        /// <param name="date">BusinessDay valued date.
        /// </param>
        public virtual void AddRemoveSubStrategies(Strategy strategy, BusinessDay date)
        {
            Dictionary<int, Instrument> instruments = this.Instruments(DateTime.Now, false);

            if (!this.Tree.ContainsStrategy(strategy))
            {
                this.Tree.AddSubStrategy(strategy);
                this.AddInstrument(strategy, DateTime.MinValue);
            }
            else
                this.RemoveInstrument(strategy, DateTime.MinValue);
        }

        /// <summary>
        /// Function: Retrieve a list of instruments from both memory and persistent storage.
        /// </summary>
        /// <param name="type">Type of instrument to be retrieved.
        /// </param>         
        public static List<Strategy> ActiveMasters(DateTime date)
        {
            return Factory.ActiveMasters(User.CurrentUser, date);
        }

        /// <summary>
        /// Function: Create a strategy
        /// </summary>    
        /// <param name="name">Name
        /// </param>
        /// <param name="initialDay">Creating date
        /// </param>
        /// <param name="initialValue">Starting NAV and portfolio AUM.
        /// </param>
        /// <param name="ccy">Base currency
        /// </param>
        /// <param name="simulated">True if not stored persistently.
        /// </param>
        public static Strategy CreateStrategy(string name, Currency ccy, BusinessDay initialDay, double initialValue, bool simulated)
        {
            Instrument instrument = Instrument.CreateInstrument(name, InstrumentType.Strategy, name, ccy, FundingType.TotalReturn, simulated);
            Strategy strategy = new Strategy(instrument);

            ConstantStrategy main_cash_strategy = ConstantStrategy.CreateStrategy(instrument.Name + "/" + ccy + "/Cash", ccy, initialDay, 1.0, instrument.SimulationObject);

            Instrument portfolio_instrument = Instrument.CreateInstrument(instrument.Name + "/Portfolio", InstrumentType.Portfolio, instrument.Name + "/Portfolio", ccy, FundingType.TotalReturn, instrument.SimulationObject);
            Portfolio portfolio = Portfolio.CreatePortfolio(portfolio_instrument, main_cash_strategy, main_cash_strategy, null);
            portfolio.TimeSeriesRoll = TimeSeriesRollType.Last;
            portfolio.AddReserve(ccy, main_cash_strategy, main_cash_strategy);

            portfolio.Strategy = strategy;

            strategy.Startup(initialDay, initialValue, portfolio);
            strategy.InitialDate = initialDay.DateTime;// new DateTime(1990, 01, 06);


            if (!instrument.SimulationObject)
            {
                strategy.Portfolio.MasterPortfolio.Strategy.Tree.SaveNewPositions();
                strategy.Portfolio.MasterPortfolio.Strategy.Tree.Save();
            }

            return strategy;

        }
    }
}