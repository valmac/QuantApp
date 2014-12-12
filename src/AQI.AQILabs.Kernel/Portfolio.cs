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
using System.Data;
using System.Linq;
using AQI.AQILabs.Kernel.Numerics.Util;

namespace AQI.AQILabs.Kernel
{
    /// <summary>
    /// Enumeration of possible Position PnL types.
    /// Pct = Pnl in percentage points.
    /// Pts = Pnl in absolute points.
    /// </summary>
    public enum PositionPnLType
    {
        Pct = 0, Pts = 1
    };

    /// <summary>
    /// Enumeration of possible portfolio rebalancing types.
    /// Internal = Not implemented.
    /// Reserve = Cash needed to execute and carry position is taken from the reserve asset.
    /// Equal = Not implemented.
    /// </summary>
    public enum RebalancingType
    {
        Internal = 0, Reserve = 1, Equal = 2
    };

    /// <summary>
    /// Enumeration of possible position/order update types.
    /// UpdateUnits = Additively update the new unit to the old units.
    /// UpdateNotional = Additively update the new aum notional to the old aum notioanal.
    /// OverrideUnits = Override the old unit with the new units.
    /// OverrideNotional = Override the old notional aum with the new notional aum.
    /// </summary>
    public enum UpdateType
    {
        UpdateUnits = 0, UpdateNotional = 1, OverrideUnits = 2, OverrideNotional = 3
    };

    /// <summary>
    /// Enumeration of possible position/order types.
    /// </summary>
    public enum PositionType
    {
        Short = -1, Long = 1
    };

    /// <summary>
    /// Enumeration of possible order statues.
    /// NotSet = On creation, orders are not set a status and this flag is default.
    /// Submitted = When an order has been submitted for execution.
    /// Executed = When an order has been executed but not yet booked.
    /// Booked = When an order has been executed, booked and a position has been created or altered.
    /// NotExecuted = When an order has been sent for execution but was not executed.
    /// </summary>
    public enum OrderStatus
    {
        New = 0, Submitted = 1, Executed = 2, Booked = 3, NotExecuted = 4
    };

    /// <summary>
    /// Enumeration of possible order types.
    /// Market = Standard market order.
    /// Limit = Standard limit order.
    /// </summary>
    public enum OrderType
    {
        Market = 0, Limit = 1
    };

    /// <summary>
    /// Class virtual position containing the intersection of the information contained
    /// in both orders and positions. Instances of this class are return when retrieving both positions
    /// and orders of a portfolio.
    /// </summary>
    public class VirtualPosition : MarshalByRefObject
    {
        protected int _instrumentID = -1;
        protected DateTime _timestamp;
        protected double _unit;

        [Newtonsoft.Json.JsonIgnore]
        protected Instrument _instrument = null;

        /// <summary>
        /// Constructor of the VirtualPosition Class
        /// </summary>
        /// <remarks>
        /// Only used Internaly by Kernel
        /// </remarks>
        /// <param name="instrumentID">integer valued ID of reference Instrument</param>
        /// <param name="timestamp">DateTime valued timestamp of the position or order.</param>
        /// <param name="unit">double valued unit of the position or order.</param>
        public VirtualPosition(int instrumentID, DateTime timestamp, double unit)
        {
            this._instrumentID = instrumentID;
            this._timestamp = timestamp;
            this._unit = unit;
        }

        /// <summary>
        /// Function: String representation of the Virtual Position.
        /// </summary>
        public override string ToString()
        {
            return Instrument + " " + Unit + " " + Timestamp.ToShortDateString();
        }

        /// <summary>
        /// Property: integer valued ID of the position's/order's underlying Instrument.
        /// </summary>
        public int InstrumentID
        {
            get
            {
                return _instrumentID;
            }
            set
            {
                this._instrumentID = value;
            }
        }

        /// <summary>
        /// Property: Instrument valued reference to the underlying Instrument.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Instrument Instrument
        {
            get
            {
                if (_instrument == null)
                    _instrument = Instrument.FindInstrument(_instrumentID);

                return _instrument;
            }
        }

        /// <summary>
        /// Property: DateTime valued timestamp reference of the position/order.
        /// </summary>
        public DateTime Timestamp
        {
            get
            {
                return _timestamp;
            }
            set
            {
                this._timestamp = value;
            }
        }

        /// <summary>
        /// Property: double valued unit of the position/order.
        /// </summary>
        public double Unit
        {
            get
            {
                return _unit;
            }
            set
            {
                _unit = value;
            }
        }
    }

    /// <summary>
    /// Class representing the Order's generaterated within the system.
    /// Orders are created within a specific portfolio. Sent to an execution platform
    /// and finally booked as a position.
    /// </summary>
    public class Order : MarshalByRefObject, IEquatable<Order>
    {
        internal double _unit = double.NaN;
        private int _instrumentID = -1;
        private int _portfolioID = -1;

        private string _id = null;

        private DateTime _orderDate = DateTime.MaxValue;
        private DateTime _executionDate = DateTime.MaxValue;
        private OrderStatus _status = OrderStatus.New;
        private OrderType _type = OrderType.Market;
        private double _executionLevel;
        private double _limit;

        private string _client = null;
        private string _destination = null;
        private string _account = null;

        private Boolean _aggregated;

        private Portfolio _portfolio = null;
        private Instrument _instrument = null;

        /// <summary>
        /// Constructor of the Order Class
        /// </summary>
        /// <remarks>
        /// Only used Internaly by Kernel
        /// </remarks>
        /// <param name="instrumentID">integer valued ID of reference Instrument</param>
        /// <param name="timestamp">DateTime valued timestamp of the position or order.</param>
        /// <param name="unit">double valued unit of the order.</param>
        /// <param name="orderDate">DateTime valued date referencing when the order was generated.</param>
        /// <param name="executionDate">DateTime valued date referencing when the order was executed.</param>        
        /// <param name="status">OrderStatus valued ID of the position's/order's underlying Instrument.</param>
        /// <param name="executionLevel">Execution level of the order.</param>
        /// <param name="aggregated">True if order is an aggregate of all the orders linked to the same Instrument within the Portfolio and it's sub portfolios. False otherwise.</param>
        [Newtonsoft.Json.JsonConstructor]
        public Order(string id, int portfolioID, int instrumentID, double unit, DateTime orderDate, DateTime executionDate, OrderType type, double limit, OrderStatus status, double executionLevel, Boolean aggregated)
        {
            _id = id;
            _portfolioID = portfolioID;
            _instrumentID = instrumentID;
            _unit = unit;
            _orderDate = orderDate;
            _executionDate = executionDate;
            _status = status;
            _executionLevel = executionLevel;
            _aggregated = aggregated;

            _type = type;
            _limit = limit;
        }

        /// <summary>
        /// Constructor of the Order Class
        /// </summary>
        /// <remarks>
        /// Only used Internaly by Kernel
        /// </remarks>
        /// <param name="instrumentID">integer valued ID of reference Instrument</param>
        /// <param name="timestamp">DateTime valued timestamp of the position or order.</param>
        /// <param name="unit">double valued unit of the order.</param>
        /// <param name="orderDate">DateTime valued date referencing when the order was generated.</param>
        /// <param name="executionDate">DateTime valued date referencing when the order was executed.</param>        
        /// <param name="status">OrderStatus valued ID of the position's/order's underlying Instrument.</param>
        /// <param name="executionLevel">Execution level of the order.</param>
        public Order(int portfolioID, int instrumentID, double unit, DateTime orderDate, DateTime executionDate, OrderType type, double limit, OrderStatus status, double executionLevel)
        {
            if (type == OrderType.Market)
            {
                Portfolio portfolio = Instrument.FindInstrument(portfolioID) as Portfolio;
                Dictionary<int, Dictionary<string, Order>> orders = portfolio.MasterPortfolio.Orders(orderDate, true);
                if (orders != null && orders.ContainsKey(instrumentID))
                    foreach (Order order in orders[instrumentID].Values.ToList())
                        if (order.Type == OrderType.Market && order.Status == OrderStatus.New)
                            _id = order.ID;
            }

            _id = _id == null ? System.Guid.NewGuid().ToString() : _id;
            _portfolioID = portfolioID;
            _instrumentID = instrumentID;
            _unit = unit;
            _orderDate = orderDate;
            _executionDate = executionDate;
            _status = status;
            _executionLevel = executionLevel;
            _aggregated = false;

            _type = type;
            _limit = limit;
        }

        /// <summary>
        /// Constructor of the Order Class
        /// </summary>
        /// <remarks>
        /// Only used Internaly by Kernel
        /// </remarks>
        /// <param name="instrumentID">integer valued ID of reference Instrument</param>
        /// <param name="timestamp">DateTime valued timestamp of the position or order.</param>
        /// <param name="unit">double valued unit of the order.</param>
        /// <param name="orderDate">DateTime valued date referencing when the order was generated.</param>
        /// <param name="executionDate">DateTime valued date referencing when the order was executed.</param>        
        /// <param name="status">OrderStatus valued ID of the position's/order's underlying Instrument.</param>
        /// <param name="executionLevel">Execution level of the order.</param>
        /// <param name="aggregated">True if order is an aggregate of all the orders linked to the same Instrument within the Portfolio and it's sub portfolios. False otherwise.</param>
        public Order(Order order, Portfolio portfolio)
        {
            _id = order.ID;
            _portfolioID = portfolio.ID;
            _instrumentID = order.InstrumentID;
            _unit = order.Unit;
            _orderDate = order.OrderDate;
            _executionDate = order.ExecutionDate;
            _status = order.Status;
            _executionLevel = order.ExecutionLevel;
            _aggregated = true;

            _client = order.Client;
            _destination = order.Destination;
            _account = order.Account;

            _type = order.Type;
            _limit = order.Limit;
        }

        /// <summary>
        /// Property: integer valued ID of the order's underlying Portfolio.
        /// </summary>
        public int PortfolioID
        {
            get
            {
                return this._portfolioID;
            }
            set
            {
                this._portfolioID = value;
            }
        }

        /// <summary>
        /// Property: string valued ID of the order.
        /// </summary>
        public string ID
        {
            get
            {
                return this._id;
            }
        }

        /// <summary>
        /// Property: integer valued ID of the order's underlying Instrument.
        /// </summary>
        public int InstrumentID
        {
            get
            {
                return this._instrumentID;
            }
            set
            {
                this._instrumentID = value;
            }
        }

        public bool Equals(Order other)
        {
            if (((object)other) == null)
                return false;
            return Portfolio.ID == other.Portfolio.ID && Instrument.ID == other.Instrument.ID && OrderDate == other.OrderDate && Aggregated == other.Aggregated && Unit == other.Unit;
        }
        public override bool Equals(object other)
        {
            if (typeof(Order) != other.GetType())
                return false;

            return Equals((Order)other);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Function: String representation of the order.
        /// </summary>
        public override string ToString()
        {
            return ID + " " + Instrument + " " + Unit + " " + OrderDate.ToShortDateString() + (Aggregated ? " Aggregated" : "") + " " + Status + " " + Portfolio.Name;
        }

        /// <summary>
        /// Property: OrderStatus valued ID of the position's/order's underlying Instrument.
        /// </summary>
        /// <remarks>
        /// NotSet = On creation, orders are not set a status and this flag is default.
        /// Submitted = When an order has been submitted for execution.
        /// Executed = When an order has been executed but not yet booked.
        /// Booked = When an order has been executed, booked and a position has been created or altered.
        /// NotExecuted = When an order has been sent for execution but was not executed.
        /// </remarks>
        public OrderStatus Status
        {
            get
            {
                return _status;
            }
            set
            {
                _status = value;
                Portfolio.UpdateOrder(this, false);
            }
        }

        /// <summary>
        /// Property: OrderStatus valued ID of the position's/order's underlying Instrument.
        /// </summary>
        /// <remarks>
        /// NotSet = On creation, orders are not set a status and this flag is default.
        /// Submitted = When an order has been submitted for execution.
        /// Executed = When an order has been executed but not yet booked.
        /// Booked = When an order has been executed, booked and a position has been created or altered.
        /// NotExecuted = When an order has been sent for execution but was not executed.
        /// </remarks>
        public OrderType Type
        {
            get
            {
                return _type;
            }
            set
            {
                _type = value;
                Portfolio.UpdateOrder(this, false);
            }
        }

        /// <summary>
        /// Property: Order's Limit if it is a limit order.
        /// </summary>
        public double Limit
        {
            get
            {
                return _limit;
            }
            set
            {
                _limit = value;
                Portfolio.UpdateOrder(this, false);
            }
        }


        /// <summary>
        /// Property: Client the order should be routed through
        /// </summary>
        public string Client
        {
            get
            {
                return _client;
            }
            set
            {
                _client = value == null ? "" : value;
                Portfolio.UpdateOrder(this, false);
            }
        }

        /// <summary>
        /// Property: Destinatio through the Client the order should be routed through
        /// </summary>
        public string Destination
        {
            get
            {
                return _destination;
            }
            set
            {
                _destination = value == null ? "" : value;
                Portfolio.UpdateOrder(this, false);
            }
        }


        /// <summary>
        /// Property: Account the order should be delivered to
        /// </summary>
        public string Account
        {
            get
            {
                return _account;
            }
            set
            {
                _account = value == null ? "" : value;
                Portfolio.UpdateOrder(this, false);
            }
        }

        /// <summary>
        /// Function: Update the units addivitely and weight the execution level accordingly.
        /// If unit (input parameter) = 1 and old unit --> total unit ==> 2
        /// If old execution level = 1 and new execution level = 2 --> order execution level = 1.5
        /// </summary>
        /// <param name="unit">double valued unit to be added to the old unit. 
        /// </param>
        /// <param name="date">DateTime valued date of updated order.
        /// </param>
        /// <param name="executionLevel">double valued execution level of updated order.
        /// </param>
        public void UpdateWeightedUnitExecutionLevel(double unit, DateTime date, double executionLevel)
        {
            double oldUnit = Unit;
            double oldExecution = ExecutionLevel;

            double wexec = (oldUnit * oldExecution + unit * executionLevel) / (oldUnit + unit);
            ExecutionLevel = wexec;
            ExecutionDate = date;
            Unit = unit + oldUnit;
        }

        /// <summary>
        /// Property: Execution level of the order.
        /// If the order has not been executed the return is NaN.
        /// </summary>
        public double ExecutionLevel
        {
            get
            {
                if (_status == OrderStatus.Executed || _status == OrderStatus.Booked)
                    return _executionLevel;

                return double.NaN;
            }
            set
            {
                _executionLevel = value;
                Portfolio.UpdateOrder(this, false);
            }
        }

        /// <summary>
        /// Property: Portfolio that owns the this order.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Portfolio Portfolio
        {
            get
            {
                if (_portfolio == null)
                    _portfolio = Instrument.FindInstrument(_portfolioID) as Portfolio;

                return _portfolio;
            }
        }

        /// <summary>
        /// Property: Instrument linked to this order.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Instrument Instrument
        {
            get
            {
                if (_instrument == null)
                    _instrument = Instrument.FindInstrument(_instrumentID);

                return _instrument;
            }
        }

        /// <summary>
        /// Property: DateTime valued date referencing when the order was generated.
        /// </summary>
        public DateTime OrderDate
        {
            get
            {
                return _orderDate;
            }
            set
            {
                _orderDate = value;
            }
        }

        /// <summary>
        /// Property: DateTime valued date referencing when the order was executed.
        /// </summary>
        public DateTime ExecutionDate
        {
            get
            {
                return _executionDate;
            }
            set
            {
                _executionDate = value;
                Portfolio.UpdateOrder(this, false);
            }
        }

        /// <summary>
        /// Property: double valued number of Instrument units linked to this order.
        /// </summary>
        public double Unit
        {
            get
            {
                return _unit;
            }
            set
            {
                double newUnit = Portfolio.OrderUnitCalculationFilter(this.Instrument, value);

                double diff = newUnit - (double.IsNaN(_unit) ? 0 : _unit);


                if (!Aggregated && Portfolio.ParentPortfolio != null)
                    Portfolio.ParentPortfolio.UpdateOrderTree(this, diff);

                _unit = newUnit;

                Portfolio.UpdateOrder(this, false);

                if (!Aggregated)
                {
                    Order agg = Portfolio.FindOrder(this.ID, true);
                    if (agg != null)
                        agg.Unit += diff;
                    else
                        Portfolio.CreateOrder(this);
                }
            }
        }

        /// <summary>
        /// Property: True if the order is an aggregate of all the orders linked to the same Instrument within the Portfolio and it's sub portfolios.
        /// </summary>
        public Boolean Aggregated
        {
            get
            {
                return _aggregated;
            }
            set
            {
                _aggregated = value;
            }
        }

        /// <summary>
        /// Function: Update existing order.
        /// </summary>
        /// <param name="orderDate">DateTime valued date referencing when the order is updated.</param>
        /// <param name="unit">double valued unit of the order.</param>
        /// <param name="ttype">TimeSeries type of order.</param>
        /// <param name="utype">Type of update to implement.</param>
        public Order UpdateTargetMarketOrder(DateTime orderDate, double unit, UpdateType utype)
        {
            double instrument_value_mid = 0.0;

            if ((Instrument.InstrumentType == InstrumentType.Strategy && Strategy.FindStrategy(Instrument).Portfolio != null) || Instrument.InstrumentType == InstrumentType.Portfolio)
            {
                Portfolio portfolio = Instrument.InstrumentType == InstrumentType.Strategy ? Strategy.FindStrategy(Instrument).Portfolio : Portfolio.FindPortfolio(Instrument);

                instrument_value_mid = Instrument.InstrumentType == InstrumentType.Strategy ? Strategy.FindStrategy(Instrument).GetNextAUM(orderDate, TimeSeriesType.Last) : portfolio[orderDate, TimeSeriesType.Last, DataProvider.DefaultProvider, Portfolio.TimeSeriesRoll]; // checked
                instrument_value_mid = CurrencyPair.Convert(instrument_value_mid, orderDate, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, Instrument.Currency, Portfolio.Currency);


                double notional = 0;
                if (utype == UpdateType.UpdateUnits)
                    notional = instrument_value_mid * (1.0 + unit);
                else if (utype == UpdateType.UpdateNotional)
                    notional = instrument_value_mid + unit;
                else if (utype == UpdateType.OverrideNotional)
                    notional = unit;
                else if (utype == UpdateType.OverrideUnits)
                    notional = instrument_value_mid * unit;


                Unit = notional;
                (Instrument.InstrumentType == InstrumentType.Strategy ? (Strategy)Instrument : ((Portfolio)Instrument).Strategy).UpdateAUMOrder(orderDate, notional);//, true);
                return this;
            }
            else
            {
                instrument_value_mid = Instrument[orderDate, TimeSeriesType.Last, DataProvider.DefaultProvider, Portfolio.TimeSeriesRoll];

                if (utype == UpdateType.UpdateUnits)
                    unit = Unit + unit;

                else if (utype == UpdateType.UpdateNotional)
                    unit = Unit + unit / instrument_value_mid;

                else if (utype == UpdateType.OverrideNotional)
                    unit = unit / instrument_value_mid;

                //Position oldPosition = Portfolio.FindPosition(Instrument, OrderDate);
                //Unit = unit -(oldPosition != null ? oldPosition.Unit : 0);

                return Portfolio.CreateTargetMarketOrder(Instrument, orderDate, unit);
            }
        }
    }

    /// <summary>
    /// Class representing the Positions's generaterated within the system.
    /// Positions are created within a specific portfolio.
    /// </summary>
    public class Position : MarshalByRefObject, IEquatable<Position>
    {
        private double _unit = double.NaN;
        private DateTime _timestamp = DateTime.MaxValue;
        private Boolean _aggregated = false;
        private double _strike = double.NaN;
        private DateTime _initialStrikeTimeStamp = DateTime.MaxValue;
        private double _initialstrike = double.NaN;
        private DateTime _strikeTimeStamp = DateTime.MaxValue;

        public bool Equals(Position other)
        {
            if (((object)other) == null)
                return false;
            return _instrumentID == other.InstrumentID && _portfolioID == other.PortfolioID && _timestamp == other.Timestamp && _strike == other.Strike && _aggregated == other.Aggregated && _unit == other.Unit;
        }
        public override bool Equals(object other)
        {
            try { return Equals((Position)other); }
            catch { return false; }
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }


        private int _instrumentID = -1;
        private int _portfolioID = -1;

        [Newtonsoft.Json.JsonIgnore]
        private Portfolio _portfolio = null;
        [Newtonsoft.Json.JsonIgnore]
        private Instrument _instrument = null;

        /// <summary>
        /// Constructor of the Position Class
        /// </summary>
        /// <remarks>
        /// Only used Internaly by Kernel
        /// </remarks>
        /// <param name="portfolioID">integer valued ID of the Portfolio that owns this position.</param>
        /// <param name="instrumentID">integer valued ID of reference Instrument.</param>
        /// <param name="unit">double valued unit of the position.</param>
        /// <param name="timestamp">DateTime valued timestamp of the position.</param>
        /// <param name="strike">double valued strike price of the position on the most previous trade previous to the given timestamp.</param>
        /// <param name="initialStrikeTimeStamp">DateTime valued date referencing date the position was created.</param>
        /// <param name="initialStrike">double valued strike price of when the position was initially created.</param>
        /// <param name="strikeTimeStamp">DateTime valued date referencing date the position on the most previous trade previous to the given timestamp.</param>
        /// <param name="aggregated">True if order is an aggregate of all the orders linked to the same Instrument within the Portfolio and it's sub portfolios. False otherwise.</param>
        public Position(int portfolioID, int instrumentID, double unit, DateTime timestamp, double strike, DateTime initialStrikeTimeStamp, double initialStrike, DateTime strikeTimeStamp, Boolean aggregated)
        {
            _portfolioID = portfolioID;
            _instrumentID = instrumentID;

            _unit = unit;
            _timestamp = timestamp;
            _strike = strike;
            _initialStrikeTimeStamp = initialStrikeTimeStamp;
            _initialstrike = initialStrike;
            _strikeTimeStamp = strikeTimeStamp;
            _aggregated = aggregated;
        }

        /// <summary>
        /// Property: integer valued ID of the position's underlying Portfolio.
        /// </summary>
        public int PortfolioID
        {
            get
            {
                return this._portfolioID;
            }
            set
            {
                this._portfolioID = value;
            }
        }

        /// <summary>
        /// Property: integer valued ID of the position's underlying Instrument.
        /// </summary>
        public int InstrumentID
        {
            get
            {
                return this._instrumentID;
            }
            set
            {
                this._instrumentID = value;
            }
        }

        /// <summary>
        /// Function: String representation of the order.
        /// </summary>
        public override string ToString()
        {
            return Instrument + " " + Unit + " " + Timestamp.ToShortDateString() + " " + (_aggregated ? "Aggregated" : "");
        }

        /// <summary>
        /// Property: Portfolio that owns the this position.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Portfolio Portfolio
        {
            get
            {
                if (_portfolio == null)
                    _portfolio = Instrument.FindInstrument(_portfolioID) as Portfolio;

                return _portfolio;
            }
        }

        /// <summary>
        /// Property: Instrument linked to this position.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Instrument Instrument
        {
            get
            {
                if (_instrument == null)
                    _instrument = Instrument.FindInstrument(_instrumentID);

                return _instrument;
            }
        }

        /// <summary>
        /// Property: DateTime valued date timestamp referencing this position.
        /// </summary>
        public DateTime Timestamp
        {
            get
            {
                return _timestamp;
            }
            set
            {
                _timestamp = value;
            }
        }

        /// <summary>
        /// Property: double valued number of Instrument units linked to this position.
        /// </summary>
        public double Unit
        {
            get
            {
                return _unit;
            }
            set
            {
                _unit = value;
                if (Math.Abs(_unit) < Portfolio._tolerance)
                    _unit = 0.0;
                //_unit = Math.Round(value, 7);
            }
        }

        /// <summary>
        /// Property: True if the order is an aggregate of all the orders linked to the same Instrument within the Portfolio and it's sub portfolios.
        /// </summary>
        public Boolean Aggregated
        {
            get
            {
                return _aggregated;
            }
        }

        /// <summary>
        /// Property: double valued number of Instrument units aggregated from the Portfolio and it's sub portfolios linked to this position.
        /// </summary>
        public double MasterUnit(DateTime date)
        {
            return Portfolio.MasterPortfolio.AggregatedUnit(Instrument, date);
        }

        /// <summary>
        /// Property: double valued strike price of the position on the most previous trade previous to the given timestamp.
        /// </summary>
        public double Strike
        {
            get
            {
                return _strike;
            }
            set
            {
                _strike = value;
            }
        }

        /// <summary>
        /// Property: double valued strike price of when the position was initially created.
        /// </summary>
        public double InitialStrike
        {
            get
            {
                return _initialstrike;
            }
            set
            {
                _initialstrike = value;
            }
        }

        /// <summary>
        /// Property: DateTime valued date referencing date the position on the most previous trade previous to the given timestamp.
        /// </summary>
        public DateTime StrikeTimestamp
        {
            get
            {
                return _strikeTimeStamp;
            }
            set
            {
                _strikeTimeStamp = value;
            }
        }

        /// <summary>
        /// Property: DateTime valued date referencing date the position was created.
        /// </summary>
        public DateTime InitialStrikeTimestamp
        {
            get
            {
                return _initialStrikeTimeStamp;
            }
            private set
            {
                _initialStrikeTimeStamp = value;
            }
        }

        /// <summary>
        /// Function: Value of the position.
        /// If the instrument is excess return, this value is the position Pnl.
        /// If the instrument is total return, this value is the notional value of this position (Strike + Pnl).
        /// </summary>
        /// <param name="date">DateTime value referecing the valuation timestamp.</param>
        public double Value(DateTime date)
        {
            return Value(date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last);//, false);
        }

        /// <summary>
        /// Function: Value of the position.
        /// If the instrument is excess return, this value is the position Pnl.
        /// If the instrument is total return, this value is the notional value of this position (Strike + Pnl).
        /// </summary>
        /// <param name="date">DateTime value referecing the valuation timestamp.</param>
        /// <param name="ccy">Currency denomination of this value.</param>
        public double Value(DateTime date, Currency ccy)
        {
            return Value(date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, ccy);//, false);
        }

        /// <summary>
        /// Function: Value of the position.
        /// If the instrument is excess return, this value is the position Pnl.
        /// If the instrument is total return, this value is the notional value of this position (Strike + Pnl).
        /// </summary>
        /// <param name="date">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).</param>
        /// <param name="rollType">Roll type of reference time series object.</param>
        /// <param name="ccy">Currency denomination of this value.</param>
        public double Value(DateTime date, TimeSeriesType tstype, DataProvider provider, TimeSeriesRollType rollType, Currency ccy)//, bool useExecutionValues)
        {
            double value = PnL(date, tstype, provider, rollType, PositionPnLType.Pts, ccy, true);//, useExecutionValues);

            if (Instrument.FundingType == FundingType.ExcessReturn)
                return value;

            return value + CurrencyPair.Convert(Strike, date, tstype, provider, rollType, ccy, Instrument.Currency);
        }

        /// <summary>
        /// Function: Value of the position.
        /// If the instrument is excess return, this value is the position Pnl.
        /// If the instrument is total return, this value is the notional value of this position (Strike + Pnl).
        /// </summary>
        /// <param name="date">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).</param>
        /// <param name="rollType">Roll type of reference time series object.</param>        
        public double Value(DateTime date, TimeSeriesType tstype, DataProvider provider, TimeSeriesRollType rollType)//, Boolean useExecutionLevel)
        {
            double value = PnL(date, tstype, provider, rollType, PositionPnLType.Pts, Portfolio.Currency, true);//, useExecutionLevel);

            if (Instrument.FundingType == FundingType.ExcessReturn)
                return value;

            return value + CurrencyPair.Convert(Strike, date, tstype, provider, rollType, Portfolio.Currency, Instrument.Currency);
        }

        /// <summary>
        /// Function: Notional value of the position. For both excess and total return instruments, this value is the notional value of this position (Strike + Pnl).
        /// </summary>
        /// <param name="date">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>        
        public double NotionalValue(DateTime date)
        {
            return NotionalValue(date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last);
        }

        /// <summary>
        /// Function: Notional value of the position. For both excess and total return instruments, this value is the notional value of this position (Strike + Pnl).
        /// </summary>
        /// <param name="date">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).</param>
        /// <param name="rollType">Roll type of reference time series object.</param>
        public double NotionalValue(DateTime date, TimeSeriesType tstype, DataProvider provider, TimeSeriesRollType rollType)
        {
            return PnL(date, tstype, provider, rollType, PositionPnLType.Pts) + CurrencyPair.Convert(Strike, date, tstype, provider, rollType, Portfolio.Currency, Instrument.Currency);
        }

        /// <summary>
        /// Function: Notional value of the position. For both excess and total return instruments, this value is the notional value of this position (Strike + Pnl).
        /// </summary>
        /// <param name="date">DateTime value referecing the valuation timestamp.</param>
        /// <param name="ccy">Currency denomination of this value.</param>
        public double NotionalValue(DateTime date, Currency ccy)
        {
            return NotionalValue(date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, ccy);
        }

        /// <summary>
        /// Function: Notional value of the position. For both excess and total return instruments, this value is the notional value of this position (Strike + Pnl).
        /// </summary>
        /// <param name="date">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).</param>
        /// <param name="rollType">Roll type of reference time series object.</param>
        /// <param name="ccy">Currency denomination of this value.</param>
        public double NotionalValue(DateTime date, TimeSeriesType tstype, DataProvider provider, TimeSeriesRollType rollType, Currency ccy)
        {
            return PnL(date, tstype, provider, rollType, PositionPnLType.Pts, ccy) + CurrencyPair.Convert(Strike, date, tstype, provider, rollType, ccy, Instrument.Currency);
        }

        /// <summary>
        /// Function: Notional value of the position. For both excess and total return instruments, this value is the notional value of this position (Strike + Pnl).
        /// </summary>
        /// <param name="date">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).</param>
        /// <param name="rollType">Roll type of reference time series object.</param>
        /// <param name="ccy">Currency denomination of this value.</param>
        public double NotionalValue(DateTime date, TimeSeriesType tstype, DataProvider provider, TimeSeriesRollType rollType, Currency ccy, Boolean includeCarryCost)//, Boolean useExecutionLevel)
        {
            //return PnL(date, tstype, provider, rollType, PositionPnLType.Pts, ccy, includeCarryCost, useExecutionLevel) + CurrencyPair.Convert(Strike, date, tstype, provider, rollType, ccy, Instrument.Currency);
            return PnL(date, tstype, provider, rollType, PositionPnLType.Pts, ccy, includeCarryCost) + CurrencyPair.Convert(Strike, date, tstype, provider, rollType, ccy, Instrument.Currency);
        }

        /// <summary>
        /// Function: Profit and Loss of the position. For both excess and total return instruments, this value represents the position's change in value since it's last change.
        /// </summary>
        /// <param name="timestamp">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).</param>
        /// <param name="rollType">Roll type of reference time series object.</param>
        /// <param name="type">type of PnL value returned.</param>
        public double PnL(DateTime timestamp, TimeSeriesType tstype, DataProvider provider, TimeSeriesRollType rollType, PositionPnLType type)
        {
            return PnL(timestamp, tstype, provider, rollType, type, Portfolio.Currency);
        }

        /// <summary>
        /// Function: Profit and Loss of the position. For both excess and total return instruments, this value represents the position's change in value since it's last change.
        /// </summary>
        /// <param name="timestamp">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).</param>
        /// <param name="rollType">Roll type of reference time series object.</param>
        /// <param name="type">type of PnL value returned.</param>
        /// <param name="ccy">Currency denomination of this value.</param>        
        public double PnL(DateTime timestamp, TimeSeriesType tstype, DataProvider provider, TimeSeriesRollType rollType, PositionPnLType type, Currency ccy)
        {
            return PnL(timestamp, tstype, provider, rollType, type, ccy, true);
        }

        /// <summary>
        /// Function: Profit and Loss of the position. For both excess and total return instruments, this value represents the position's change in value since it's last change.
        /// </summary>
        /// <param name="timestamp">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).</param>
        /// <param name="rollType">Roll type of reference time series object.</param>
        /// <param name="type">type of PnL value returned.</param>
        /// <param name="ccy">Currency denomination of this value.</param>
        /// <param name="includeCarryCost">If True, include carry costs in the PnL calculation. Else, do not include carry cost.</param>
        //public double PnL(DateTime timestamp, TimeSeriesType tstype, DataProvider provider, TimeSeriesRollType rollType, PositionPnLType type, Currency ccy, Boolean includeCarryCost)
        //{
        //    return PnL(timestamp, tstype, provider, rollType, type, ccy, includeCarryCost, false);                       
        //}

        /// <summary>
        /// Function: Profit and Loss of the position. For both excess and total return instruments, this value represents the position's change in value since it's last change.
        /// </summary>
        /// <param name="timestamp">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).</param>
        /// <param name="rollType">Roll type of reference time series object.</param>
        /// <param name="type">type of PnL value returned.</param>
        /// <param name="ccy">Currency denomination of this value.</param>
        /// <param name="includeCarryCost">If True, include carry costs in the PnL calculation. Else, do not include carry cost.</param>
        /// <param name="executionLevel">Level of execution used in P&L valuation</param>
        public double PnL(DateTime timestamp, TimeSeriesType tstype, DataProvider provider, TimeSeriesRollType rollType, PositionPnLType type, Currency ccy, Boolean includeCarryCost)//, Boolean useExecutionLevel)
        {
            //if (timestamp == InitialStrikeTimestamp)
            //    return 0;

            double pts = 0.0;

            //double executionLevel = Portfolio.MasterPortfolio.Market.ExecutionLevel(Instrument, timestamp, Portfolio);
            //if (useExecutionLevel)
            //{
            //    if (double.IsNaN(executionLevel))
            //        executionLevel = Instrument[timestamp, tstype, provider, rollType] * (_instrument.InstrumentType == InstrumentType.Future ? (_instrument as Future).ContractSize : 1.0);

            //}
            //else
            double executionLevel = Instrument[timestamp, tstype, provider, rollType] * (_instrument.InstrumentType == InstrumentType.Future ? (_instrument as Future).PointSize : 1.0);

            //if (includeCarryCost)
            //{
            //    master_unit = MasterUnit(timestamp);
            //    master_unit = double.IsNaN(master_unit) ? 0.0 : master_unit;
            //    prct = Unit;
            //    prct *= (master_unit == 0 || Math.Sign(master_unit) != Math.Sign(Unit) ? 0.0 : Math.Max(-1.0, Math.Min(1.0, master_unit / Unit)));
            //}

            double aggCCost = this.AggregatedCarryCost(timestamp, tstype, provider, rollType);

            if (Instrument.InstrumentType == InstrumentType.Strategy)
            {
                Strategy strategy = Strategy.FindStrategy(Instrument);
                if (strategy.Portfolio != null)
                    //pts = strategy.Portfolio[timestamp, tstype, provider, rollType, null, useExecutionLevel] * Unit - Strike + aggCCost;
                    pts = strategy.Portfolio[timestamp, tstype, provider, rollType] * Unit - Strike + aggCCost;
                else
                    pts = executionLevel * Unit - Strike + aggCCost;
            }
            else
                pts = executionLevel * Unit - Strike + aggCCost;


            pts = CurrencyPair.Convert(pts, timestamp, tstype, provider, rollType, ccy, Instrument.Currency);

            if (type == PositionPnLType.Pts)
                return pts;
            else
                return pts / CurrencyPair.Convert(Portfolio[timestamp, tstype, provider, rollType], timestamp, tstype, provider, rollType, ccy, Portfolio.Currency);
        }

        /// <summary>
        /// Function: Profit and Loss of the position. For both excess and total return instruments, this value represents the position's change in value since it's last change.
        /// </summary>
        /// <param name="timestamp">DateTime value referecing the valuation timestamp.</param>
        /// <param name="type">type of PnL value returned.</param>
        public double PnL(DateTime timestamp, PositionPnLType type)
        {
            return PnL(timestamp, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, type);
        }

        /// <summary>
        /// Function: Aggregated carry cost
        /// </summary>
        /// <param name="timestamp">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).</param>
        /// <param name="rollType">Roll type of reference time series object.</param>
        public double AggregatedCarryCost(DateTime timestamp, TimeSeriesType tstype, DataProvider provider, TimeSeriesRollType rollType)
        {
            double prct = 0.0;
            double master_unit = 0.0;


            master_unit = MasterUnit(timestamp);
            master_unit = double.IsNaN(master_unit) ? 0.0 : master_unit;
            prct = Unit;
            prct *= (master_unit == 0 || Math.Sign(master_unit) != Math.Sign(Unit) ? 0.0 : Math.Max(-1.0, Math.Min(1.0, master_unit / Unit)));

            return (prct != 0.0 ? prct * Instrument.CarryCost(StrikeTimestamp, timestamp, tstype, (master_unit > 0 ? PositionType.Long : PositionType.Short)) : 0.0);
        }

        /// <summary>
        /// Function: Daily Profit and Loss of the position. For both excess and total return instruments, this value represents the position's change in value since it's last change.
        /// </summary>
        /// <param name="timestamp">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).</param>
        /// <param name="rollType">Roll type of reference time series object.</param>
        /// <param name="type">type of PnL value returned.</param>
        /// <param name="ccy">Currency denomination of this value.</param>
        /// <param name="includeCarryCost">If True, include carry costs in the PnL calculation. Else, do not include carry cost.</param>
        public double DailyPnL(DateTime timestamp, TimeSeriesType tstype, DataProvider provider, TimeSeriesRollType rollType, PositionPnLType type, Currency ccy, Boolean includeCarryCost)
        {
            DateTime t0 = timestamp.Date < InitialStrikeTimestamp ? InitialStrikeTimestamp : timestamp.Date;

            double pt = PnL(timestamp, tstype, provider, rollType, type, ccy, includeCarryCost);//, false);
            double pt_1 = t0 == InitialStrikeTimestamp ? 0 : PnL(t0, tstype, provider, rollType, type, ccy, includeCarryCost);//, false);

            return pt - pt_1;
        }

        /// <summary>
        /// Function: Daily Profit and Loss of the position. For both excess and total return instruments, this value represents the position's change in value since it's last change.
        /// </summary>
        /// <param name="timestamp">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).</param>
        /// <param name="rollType">Roll type of reference time series object.</param>
        /// <param name="type">type of PnL value returned.</param>
        /// <param name="ccy">Currency denomination of this value.</param>        
        public double DailyPnL(DateTime timestamp, TimeSeriesType tstype, DataProvider provider, TimeSeriesRollType rollType, PositionPnLType type, Currency ccy)
        {
            return DailyPnL(timestamp, tstype, provider, rollType, type, ccy, true);
        }

        /// <summary>
        /// Function: Daily Profit and Loss of the position. For both excess and total return instruments, this value represents the position's change in value since it's last change.
        /// </summary>
        /// <param name="timestamp">DateTime value referecing the valuation timestamp.</param>
        /// <param name="tstype">Time series type of time series point used in the value calculation.</param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).</param>
        /// <param name="rollType">Roll type of reference time series object.</param>
        /// <param name="type">type of PnL value returned.</param>
        public double DailyPnL(DateTime timestamp, TimeSeriesType tstype, DataProvider provider, TimeSeriesRollType rollType, PositionPnLType type)
        {
            return DailyPnL(timestamp, tstype, provider, rollType, type, Portfolio.Currency);
        }

        /// <summary>
        /// Function: Daily Profit and Loss of the position. For both excess and total return instruments, this value represents the position's change in value since it's last change.
        /// </summary>
        /// <param name="timestamp">DateTime value referecing the valuation timestamp.</param>
        /// <param name="type">type of PnL value returned.</param>
        public double DailyPnL(DateTime timestamp, PositionPnLType type)
        {
            return DailyPnL(timestamp, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, type);
        }

        /// <summary>
        /// Function: Update existing position.
        /// </summary>
        /// <param name="timestamp">DateTime valued date referencing when the position is updated.</param>
        /// <param name="unit">double valued unit of the position.</param>
        /// <param name="executionLevel">double valued execution level of the latest trade in the instrument.</param>
        /// <param name="ttype">TimeSeries type of the position update.</param>
        /// <param name="rtype">Type of rebalancing to implement.</param>
        /// <param name="utype">Type of update to implement.</param>
        public Position UpdatePosition(DateTime timestamp, double unit, double executionLevel, RebalancingType rtype, UpdateType utype)
        {
            if (Timestamp == timestamp && Unit == unit)
                return this;

            //Position latestPos = Portfolio.FindPosition(Instrument, timestamp);

            double unit_diff = 0;

            //////if (Instrument.InstrumentType == InstrumentType.Portfolio)
            //////{
            //////    if (utype == UpdateType.OverrideNotional)
            //////    {
            //////        Portfolio portfolio = Portfolio.FindPortfolio(Instrument);
            //////        portfolio.UpdateNotional(timestamp, executionLevel);
            //////        unit = 1.0;
            //////    }
            //////    else
            //////        throw new Exception("Only Override Notional is updated here");
            //////}

            if (Instrument.InstrumentType == InstrumentType.Strategy)
            {
                Strategy strategy = Strategy.FindStrategy(Instrument);
                if (strategy.Portfolio != null)
                {
                    if (utype == UpdateType.OverrideNotional)
                    {
                        strategy.UpdateAUM(timestamp, executionLevel, true);
                        unit = unit == 0.0 ? 0.0 : 1.0;
                    }
                    else
                        throw new Exception("Only Override Notional is updated here");
                }
            }

            Boolean strategy_portfolio = false;

            if (utype == UpdateType.UpdateUnits)
            {
                unit = Unit + unit;
                unit_diff = unit - Unit;
            }
            else if (utype == UpdateType.UpdateNotional)
            {
                unit_diff = unit / executionLevel - Unit;
                unit = Unit + unit / executionLevel;
            }
            else if (utype == UpdateType.OverrideNotional)
            {

                if ((Instrument.InstrumentType == InstrumentType.Strategy && Strategy.FindStrategy(Instrument).Portfolio != null) || Instrument.InstrumentType == InstrumentType.Portfolio)
                {
                    strategy_portfolio = true;
                    unit_diff = 1.0;
                }
                else
                {
                    double old_unit = Unit;
                    unit_diff = unit / executionLevel - Unit;
                    unit = unit / executionLevel;
                }
            }
            else
                unit_diff = unit - Unit;

            double valueDiff = 0;
            if (Instrument.FundingType == FundingType.ExcessReturn)
                valueDiff = Math.Abs(unit) < Math.Abs(Unit) ? -(executionLevel - Strike / Unit) * unit_diff : 0;// + this.AggregatedCarryCost(timestamp, tstype, provider, rollType);
            else
                valueDiff = -(strategy_portfolio ? 0.0 : executionLevel) * unit_diff;

            if (rtype == RebalancingType.Reserve)
            {
                Portfolio.UpdatePositions(timestamp);

                if (!double.IsNaN(valueDiff) && valueDiff != 0)// && Math.Abs(valueDiff) > Portfolio._tolerance)
                    Portfolio.UpdateReservePosition(timestamp, valueDiff, Instrument.Currency, false, false);

                //Portfolio.UpdatePositions(timestamp);
            }
            else if (rtype == RebalancingType.Equal)
                throw new Exception("Not Properly Implemented");

            return Portfolio.CreatePosition(Instrument, timestamp, unit, executionLevel, true, false, false, false, true, true, false);
        }

        private static Dictionary<string, string> _realizedDB = new Dictionary<string, string>();
        /// <summary>
        /// Function: Realize the position's carry cost and create a new position reflecting the changes.
        /// </summary>
        /// <param name="timestamp">DateTime valued date referencing when the order is updated.</param>
        /// <param name="ttype">TimeSeries type of order.</param>
        public Position RealizeCarryCost(DateTime timestamp)
        {
            string key = Portfolio.ID + "/" + Instrument.ID + "/" + Unit + "/" + timestamp;
            if (_realizedDB.ContainsKey(key))
                return this;
            _realizedDB.Add(key, key);

            Position latestPos = Portfolio.FindPosition(Instrument, timestamp);

            double valueDiff = 0;

            double master_unit = MasterUnit(timestamp);
            master_unit = double.IsNaN(master_unit) ? 0.0 : master_unit;
            double prct = Unit;
            prct *= (master_unit == 0 || Math.Sign(master_unit) != Math.Sign(Unit) ? 0.0 : Math.Max(-1.0, Math.Min(1.0, master_unit / Unit)));

            valueDiff = Instrument.CarryCost(StrikeTimestamp, timestamp, TimeSeriesType.Last, (master_unit > 0 ? PositionType.Long : PositionType.Short)) * prct;

            Portfolio.UpdatePositions(timestamp);

            double unit = Unit;

            if (valueDiff != 0 && !Portfolio.IsReserve(Instrument))
                Portfolio.UpdateReservePosition(timestamp, valueDiff, Instrument.Currency, false, false);
            else if (Portfolio.IsReserve(Instrument))
            {
                Position reserve = Portfolio.GetReservePosition(timestamp, Instrument.Currency);
                if (reserve != null)
                {
                    double notional = reserve.Value(timestamp, Instrument.Currency);
                    double value = reserve.Instrument[timestamp, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last];

                    unit = notional / value;
                }
            }

            double executionLevel = Instrument[timestamp, TimeSeriesType.Last, DataProvider.DefaultProvider, Portfolio.TimeSeriesRoll] * (_instrument.InstrumentType == InstrumentType.Future ? (_instrument as Future).PointSize : 1.0);

            double instrument_value = executionLevel;
            return Portfolio.CreatePosition(Instrument, timestamp, unit, instrument_value, true, false, false, false, false, false, false);
        }

        /// <summary>
        /// Function: Update existing order.
        /// </summary>
        /// <param name="orderDate">DateTime valued date referencing when the order is updated.</param>
        /// <param name="unit">double valued unit of the order.</param>
        /// <param name="ttype">TimeSeries type of order.</param>
        /// <param name="utype">Type of update to implement.</param>
        public Order UpdateTargetMarketOrder(DateTime orderDate, double unit, UpdateType utype)
        {
            double instrument_value_mid = 0.0;

            if ((Instrument.InstrumentType == InstrumentType.Strategy && Strategy.FindStrategy(Instrument).Portfolio != null) || Instrument.InstrumentType == InstrumentType.Portfolio)
            {
                Portfolio portfolio = Instrument.InstrumentType == InstrumentType.Strategy ? Strategy.FindStrategy(Instrument).Portfolio : Portfolio.FindPortfolio(Instrument);

                instrument_value_mid = Instrument.InstrumentType == InstrumentType.Strategy ? Strategy.FindStrategy(Instrument).GetNextAUM(orderDate, TimeSeriesType.Last) : portfolio[orderDate, TimeSeriesType.Last, DataProvider.DefaultProvider, Portfolio.TimeSeriesRoll]; // checked
                instrument_value_mid = CurrencyPair.Convert(instrument_value_mid, orderDate, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, Instrument.Currency, Portfolio.Currency);


                double notional = 0;
                if (utype == UpdateType.UpdateUnits)
                    notional = instrument_value_mid * (1.0 + unit);
                else if (utype == UpdateType.UpdateNotional)
                    notional = instrument_value_mid + unit;
                else if (utype == UpdateType.OverrideNotional)
                    notional = unit;
                else if (utype == UpdateType.OverrideUnits)
                    notional = instrument_value_mid * unit;

                return Portfolio.CreateTargetMarketOrder(Instrument, orderDate, notional);
            }
            else
            {
                instrument_value_mid = Instrument[orderDate, TimeSeriesType.Last, DataProvider.DefaultProvider, Portfolio.TimeSeriesRoll] * (_instrument.InstrumentType == InstrumentType.Future ? (_instrument as Future).PointSize : 1.0);

                if (utype == UpdateType.UpdateUnits)
                    unit = Unit + unit;

                else if (utype == UpdateType.UpdateNotional)
                    unit = Unit + unit / instrument_value_mid;

                else if (utype == UpdateType.OverrideNotional)
                    unit = unit / instrument_value_mid;

                return Portfolio.CreateTargetMarketOrder(Instrument, orderDate, unit);
            }
        }
    }

    /// <summary>
    /// Class containing the portfolio logic that
    /// manages orders, positions and tree structure.
    /// This class also manages the connectivity
    /// to the database through a relevant Factories.
    /// </summary>        
    public class Portfolio : Instrument
    {
        new public static AQI.AQILabs.Kernel.Factories.IPortfolioFactory Factory = null;

        private DateTime _lastTimestamp = DateTime.MinValue;
        private DateTime _firstTimestamp = DateTime.MinValue;

        private ConcurrentDictionary<int, Instrument> _longReserves = new ConcurrentDictionary<int, Instrument>();
        private ConcurrentDictionary<int, Instrument> _shortReserves = new ConcurrentDictionary<int, Instrument>();

        private ConcurrentDictionary<DateTime, ConcurrentDictionary<int, Position>> _positionHistoryMemory_date = new ConcurrentDictionary<DateTime, ConcurrentDictionary<int, Position>>();
        private ConcurrentDictionary<int, ConcurrentDictionary<DateTime, Position>> _positionHistoryMemory_instrument = new ConcurrentDictionary<int, ConcurrentDictionary<DateTime, Position>>();
        private ConcurrentDictionary<DateTime, ConcurrentDictionary<int, Position>> _positionNewHistoryMemory = new ConcurrentDictionary<DateTime, ConcurrentDictionary<int, Position>>();

        private ConcurrentDictionary<DateTime, ConcurrentDictionary<int, Position>> _positionHistoryMemory_date_aggregated = new ConcurrentDictionary<DateTime, ConcurrentDictionary<int, Position>>();
        private ConcurrentDictionary<int, ConcurrentDictionary<DateTime, Position>> _positionHistoryMemory_instrument_aggregated = new ConcurrentDictionary<int, ConcurrentDictionary<DateTime, Position>>();
        private ConcurrentDictionary<DateTime, ConcurrentDictionary<int, Position>> _positionNewHistoryMemory_aggregated = new ConcurrentDictionary<DateTime, ConcurrentDictionary<int, Position>>();

        private ConcurrentDictionary<DateTime, ConcurrentDictionary<int, ConcurrentDictionary<string, Order>>> _orderMemory_orderDate = new ConcurrentDictionary<DateTime, ConcurrentDictionary<int, ConcurrentDictionary<string, Order>>>();
        private ConcurrentDictionary<int, ConcurrentDictionary<DateTime, ConcurrentDictionary<string, Order>>> _orderMemory_instrument_orderDate = new ConcurrentDictionary<int, ConcurrentDictionary<DateTime, ConcurrentDictionary<string, Order>>>();
        private ConcurrentDictionary<string, Order> _orderMemory = new ConcurrentDictionary<string, Order>();
        private ConcurrentDictionary<string, Order> _orderNewMemory = new ConcurrentDictionary<string, Order>();

        private ConcurrentDictionary<DateTime, ConcurrentDictionary<int, ConcurrentDictionary<string, Order>>> _orderMemory_aggregated_orderDate = new ConcurrentDictionary<DateTime, ConcurrentDictionary<int, ConcurrentDictionary<string, Order>>>();
        private ConcurrentDictionary<int, ConcurrentDictionary<DateTime, ConcurrentDictionary<string, Order>>> _orderMemory_aggregated_instrument_orderDate = new ConcurrentDictionary<int, ConcurrentDictionary<DateTime, ConcurrentDictionary<string, Order>>>();
        private ConcurrentDictionary<string, Order> _orderMemory_aggregated = new ConcurrentDictionary<string, Order>();
        private ConcurrentDictionary<string, Order> _orderNewMemory_aggregated = new ConcurrentDictionary<string, Order>();

        private List<DateTime> _orderRemoveMemory = new List<DateTime>();

        private List<Instrument> _instrumentList = new List<Instrument>();

        private List<DateTime> _orderedDates = new List<DateTime>();

        public static double _tolerance = 0;

        private int _strategyID = -10;
        private int _parentPortfolioID = -10;

        /// <summary>
        /// Property contains int value of the unique ID of the portfolio
        /// </summary>
        /// <remarks>
        /// Main identifier for each Portfolio in the System
        /// </remarks>
        new public int ID
        {
            get
            {
                return base.ID;
            }
        }

        /// <summary>
        /// Property contains int value foofr the unique ID of the strategy linked this this portfolio.
        /// </summary>
        public int StrategyID
        {
            get
            {
                return this._strategyID;
            }
            set
            {
                if (value != -1 && value != -10)
                {
                    if (MasterPortfolio != null && MasterPortfolio == this)
                        Market.AddPortfolio(this);
                }


                this._strategyID = value;
            }
        }

        /// <summary>
        /// Property contains int value of the unique ID of this portfolio's parent portfolio.
        /// </summary>
        public int ParentPortfolioID
        {
            get
            {
                return this._parentPortfolioID;
            }
            set
            {
                this._parentPortfolioID = value;
            }
        }

        /// <summary>
        /// Constructor of the Portfolio Class        
        /// </summary>
        /// <remarks>
        /// Only used by the Kernel.
        /// </remarks>
        public Portfolio(Instrument instrument, bool loadPositionsInMemory)
            : base(instrument)
        {
            if (!SimulationObject)
                this.Cloud = instrument.Cloud;

            if (loadPositionsInMemory)
                LoadPositionOrdersMemory(DateTime.MinValue, false);
        }

        /// <summary>
        /// Constructor of the Portfolio Class        
        /// </summary>
        /// <remarks>
        /// Only used by the Kernel.
        /// </remarks>
        [Newtonsoft.Json.JsonConstructor]
        public Portfolio(int id, bool loadPositionsInMemory)
            : base(Instrument.FindCleanInstrument(id))
        {
            if (!SimulationObject)
                this.Cloud = Instrument.FindCleanInstrument(id).Cloud;

            if (loadPositionsInMemory)
                LoadPositionOrdersMemory(DateTime.MinValue, false);
        }

        /// <summary>
        /// Function: Create a clone of this portfolio.
        /// </summary>
        /// <param name="simulated">false if a persistent clone is to be created
        /// </param>
        /// <remarks>This function does not clone the reserves nor the positions/orders</remarks>
        new public Portfolio Clone(bool simulated)
        {
            Instrument instrumentClone = base.Clone(simulated);
            Portfolio clone = Portfolio.CreatePortfolio(instrumentClone, null, null, null);

            // NEED TO THINK ABOUT THIS MARKET
            //clone.Market = Market;

            return clone;
        }


        /// <summary>
        /// Function: Submit the new orders through the market adapter
        /// </summary>
        /// <param name="orderDate">reference time for order submittion
        /// </param>
        public object[] SubmitOrders(DateTime orderDate)
        {
            return Market.SubmitOrders(orderDate, this);
        }


        /// <summary>
        /// Function: Receive executed values from the market adapter
        /// </summary>
        /// <param name="executionDate">reference time for the executed orders
        /// </param>
        public void ReceiveExecutionLevels(DateTime executionDate)
        {
            Market.ReceiveExecutionLevels(executionDate, this);
        }

        private Dictionary<DateTime, string> _loadedDates = new Dictionary<DateTime, string>();

        public static readonly object objLock = new object();

        /// <summary>
        /// Function: Load the positions and orders from persistent memory on a given date.
        /// </summary>       
        /// <param name="date">DateTime value date
        /// </param>
        public void LoadPositionOrdersMemory(DateTime date, bool force)
        {
            lock (objLock)
            {
                date = Calendar.Close(date);

                if (!force)
                {
                    if (_loadedDates.ContainsKey(date.Date))
                        return;
                }

                SystemLog.Write("Loading Positions and Orders Memory: " + this + " for date " + date);
                Order order = null;
                DateTime t1 = DateTime.Now;
                List<Order> os = Factory.LoadOrders(this, date);
                DateTime t2 = DateTime.Now;
                Console.WriteLine("Load Order: " + (t2 - t1));

                List<DateTime> dates = new List<DateTime>();
                if (os != null)
                    foreach (Order o in os)
                    {
                        order = o;

                        if (!dates.Contains(o.OrderDate.Date))
                            dates.Add(o.OrderDate.Date);

                        if (!o.Aggregated)
                        {
                            if (!_orderMemory.ContainsKey(o.ID))
                                _orderMemory.TryAdd(o.ID, o);

                            if (!_orderMemory_orderDate.ContainsKey(o.OrderDate.Date))
                                _orderMemory_orderDate.TryAdd(o.OrderDate.Date, new ConcurrentDictionary<int, ConcurrentDictionary<string, Order>>());
                            if (!_orderMemory_orderDate[o.OrderDate.Date].ContainsKey(o.InstrumentID))
                                _orderMemory_orderDate[o.OrderDate.Date].TryAdd(o.Instrument.ID, new ConcurrentDictionary<string, Order>());
                            if (!_orderMemory_orderDate[o.OrderDate.Date][o.InstrumentID].ContainsKey(o.ID))
                                _orderMemory_orderDate[o.OrderDate.Date][o.InstrumentID].TryAdd(o.ID, o);

                            if (!_orderMemory_instrument_orderDate.ContainsKey(o.InstrumentID))
                                _orderMemory_instrument_orderDate.TryAdd(o.InstrumentID, new ConcurrentDictionary<DateTime, ConcurrentDictionary<string, Order>>());
                            if (!_orderMemory_instrument_orderDate[o.InstrumentID].ContainsKey(o.OrderDate.Date))
                                _orderMemory_instrument_orderDate[o.InstrumentID].TryAdd(o.OrderDate.Date, new ConcurrentDictionary<string, Order>());
                            if (!_orderMemory_instrument_orderDate[o.InstrumentID][o.OrderDate.Date].ContainsKey(o.ID))
                                _orderMemory_instrument_orderDate[o.InstrumentID][o.OrderDate.Date].TryAdd(o.ID, o);

                            UpdateOrderTree(o, o.Unit);
                        }
                    }

                Position position = null;
                List<Position> ps = new List<Position>();

                t1 = DateTime.Now;
                foreach (DateTime d in dates)
                {
                    List<Position> pss = Factory.LoadPositions(this, d);
                    foreach (Position p in pss)
                        if (!ps.Contains(p))
                            ps.Add(p);
                }

                List<Position> psss = Factory.LoadPositions(this, date);

                t2 = DateTime.Now;
                Console.WriteLine("Load Positions: " + (t2 - t1));

                if (psss != null)
                    foreach (Position p in psss)
                        if (!ps.Contains(p))
                            ps.Add(p);

                if (ps != null)
                    foreach (Position p in ps)
                    {
                        position = p;

                        if (!InstrumentList.Contains(p.Instrument))
                            InstrumentList.Add(p.Instrument);

                        if (FirstTimestamp == DateTime.MinValue)
                            FirstTimestamp = p.Timestamp;
                        else
                            FirstTimestamp = FirstTimestamp < p.Timestamp ? FirstTimestamp : p.Timestamp;

                        if (!p.Aggregated)
                        {
                            if (!_positionHistoryMemory_date.ContainsKey(p.Timestamp))
                            {
                                if (!_orderedDates.Contains(p.Timestamp))
                                {
                                    _orderedDates.Add(p.Timestamp);
                                    _orderedDates = _orderedDates.OrderBy(x => x).ToList();
                                }

                                _positionHistoryMemory_date.TryAdd(p.Timestamp, new ConcurrentDictionary<int, Position>());
                            }
                            if (!_positionHistoryMemory_date[p.Timestamp].ContainsKey(p.Instrument.ID))
                                _positionHistoryMemory_date[p.Timestamp].TryAdd(p.Instrument.ID, p);

                            if (!_positionHistoryMemory_instrument.ContainsKey(p.Instrument.ID))
                                _positionHistoryMemory_instrument.TryAdd(p.Instrument.ID, new ConcurrentDictionary<DateTime, Position>());
                            if (!_positionHistoryMemory_instrument[p.Instrument.ID].ContainsKey(p.Timestamp))
                            {
                                if (!_orderedDates.Contains(p.Timestamp))
                                {
                                    _orderedDates.Add(p.Timestamp);
                                    _orderedDates = _orderedDates.OrderBy(x => x).ToList();
                                }

                                _positionHistoryMemory_instrument[p.Instrument.ID].TryAdd(p.Timestamp, p);
                            }

                            if (this.ParentPortfolio != null)
                                this.ParentPortfolio.UpdatePositions(p.Timestamp);

                            UpdatePositionTree(p, p.Unit);
                        }
                    }

                if (position != null)
                    LastTimestamp = LastTimestamp > position.Timestamp ? LastTimestamp : position.Timestamp;
                else
                    LastTimestamp = DateTime.MinValue;

                if (!_loadedDates.ContainsKey(date.Date))
                    _loadedDates.Add(date.Date, "");
            }
        }


        /// <summary>
        /// Property: List of instruments that have been traded in this portfolio during this runtime session
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public List<Instrument> InstrumentList
        {
            get
            {
                return _instrumentList;
            }
        }

        /// <summary>
        /// Property: Address to persistent storage.        
        /// </summary>
        /// <remarks>
        /// Positions and orders are stored in the persistent memory defined by this string address. (Unless simulated)
        /// </remarks>
        [Newtonsoft.Json.JsonIgnore]
        public override string StrategyDB
        {
            get
            {
                return Strategy != null ? Strategy.DBConnection : "DefaultStrategy";
                //return "DefaultStrategy";
            }
        }

        private Portfolio _parentPortfolio = null;

        /// <summary>
        /// Property: Parent portfolio of this Portfolio.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Portfolio ParentPortfolio
        {
            get
            {
                if (_parentPortfolioID == -1 || _parentPortfolioID == -10)
                    return null;
                else
                {
                    if (_parentPortfolio == null)
                        _parentPortfolio = Factory.FindParentPortfolio(_parentPortfolioID);
                    return _parentPortfolio;
                }
            }
            set
            {
                if (_parentPortfolio == null)
                {
                    _parentPortfolioID = value.ID;
                    _parentPortfolio = value;

                    Market.RemovePortfolio(this);

                    if (LastTimestamp < _parentPortfolio.LastTimestamp)
                    {
                        UpdatePositions(_parentPortfolio.LastTimestamp);
                        _parentPortfolio.UpdatePositions(_parentPortfolio.LastTimestamp);
                    }
                    else if (LastTimestamp > _parentPortfolio.LastTimestamp)
                    {
                        _parentPortfolio.UpdatePositions(LastTimestamp);
                        UpdatePositions(LastTimestamp);
                    }

                    List<Position> apList = Positions(LastTimestamp, true);
                    if (apList != null)
                        foreach (Position pos in apList)
                            _parentPortfolio.UpdateAggregatedPositionTree(pos.Instrument, double.NaN, pos.Unit, LastTimestamp);

                    if (!SimulationObject)
                        Factory.SetProperty(this, "ParentPortfolioID", _parentPortfolio.ID);
                }
                else
                    throw new Exception("Parent Portfolio Already Set");
            }
        }

        private Strategy _strategy = null;

        /// <summary>
        /// Property: Strategy linked to this portfolio.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Strategy Strategy
        {
            get
            {
                if (_strategyID == -1 || _strategyID == -10)
                    return null;
                else
                {
                    if (_strategy == null && !SimulationObject)
                        _strategy = Factory.FindStrategy(_strategyID);

                    return _strategy;
                }
            }
            set
            {
                if (_strategy == null)
                {
                    if (MasterPortfolio != null && MasterPortfolio == this)
                        Market.AddPortfolio(this);

                    _strategyID = value.ID;
                    _strategy = value;
                    _strategy.Portfolio = this;

                    if (!SimulationObject)
                        Factory.SetProperty(this, "StrategyID", _strategy.ID);
                }
            }
        }

        /// <summary>
        /// Property: Master portfolio of the tree this portfolio as member in.
        /// The Master portfolio is the top of the tree and has not parent portfolios.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Portfolio MasterPortfolio
        {
            get
            {
                if (ParentPortfolio == null)
                    return this;

                else if (ParentPortfolio.ParentPortfolio != null)
                    return ParentPortfolio.MasterPortfolio;

                else
                    return ParentPortfolio;
            }
        }

        /// <summary>
        /// Property: Reserve asset used to purchase assets in this portfolio.        
        /// </summary>
        public Instrument LongReserve
        {
            get
            {
                return Reserve(Currency, PositionType.Long);
            }
        }

        /// <summary>
        /// Property: Reserve asset purchased when selling an asset in this portfolio.        
        /// </summary>
        public Instrument ShortReserve
        {
            get
            {
                return Reserve(Currency, PositionType.Short);
            }
        }

        /// <summary>
        /// Property: List of the IDs of the reserve assets in this portfolio. Includes long and short reserves for multiple currencies.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public List<int[]> ReserveIds
        {
            get
            {
                int[] ccyids = _longReserves.Keys.ToArray();

                List<int[]> res = new List<int[]>();

                foreach (int ccyid in ccyids)
                    res.Add(new int[] { ccyid, _longReserves[ccyid].ID, _shortReserves[ccyid].ID });

                return res;
            }
        }
        [Newtonsoft.Json.JsonIgnore]

        /// <summary>
        /// Property: List of the reserve assets in this portfolio. Includes long and short reserves for multiple currencies.
        /// </summary>
        public List<Instrument> Reserves
        {
            get
            {
                List<Instrument> list = new List<Instrument>();
                foreach (Instrument ins in _longReserves.Values)
                    if (!list.Contains(ins))
                        list.Add(ins);

                foreach (Instrument ins in _shortReserves.Values)
                    if (!list.Contains(ins))
                        list.Add(ins);

                if (list.Count == 0)
                    return list;

                return list;
            }
        }

        /// <summary>
        /// Function: Reserve asset linked to a given currency and position type.
        /// </summary>
        /// <param name="ccy">Currency of reserve asset.</param>
        /// <param name="ptype">Long or short position type.</param>
        public Instrument Reserve(Currency ccy, PositionType ptype)
        {
            if (ptype == PositionType.Long && _longReserves.ContainsKey(ccy.ID))
                return _longReserves[ccy.ID];

            else if (ptype == PositionType.Short && _shortReserves.ContainsKey(ccy.ID))
                return _shortReserves[ccy.ID];

            if (MasterPortfolio != null && MasterPortfolio != this)
                return MasterPortfolio.Reserve(ccy, ptype);

            return null;
        }

        /// <summary>
        /// Function: Add a long and short reserve asset for a specific currency into memory.
        /// </summary>
        /// <param name="ccy">Currency of reserve asset.</param>
        /// <param name="longInstrument">Long reserve asset.</param>
        /// <param name="shortInstrument">Short reserve asset.</param>
        public void AddReserveMemory(Currency ccy, Instrument longInstrument, Instrument shortInstrument)
        {
            if (!_longReserves.ContainsKey(ccy.ID) && !_shortReserves.ContainsKey(ccy.ID))
            {
                if (longInstrument != null)
                    _longReserves.TryAdd(ccy.ID, longInstrument);

                if (shortInstrument != null)
                    _shortReserves.TryAdd(ccy.ID, shortInstrument);
            }
            else
            {
                if (longInstrument != null)
                    _longReserves[ccy.ID] = longInstrument;

                if (shortInstrument != null)
                    _shortReserves[ccy.ID] = shortInstrument;
            }
        }

        /// <summary>
        /// Function: Add a long and short reserve asset for a specific currency into memory and persistent storage.
        /// </summary>
        /// <param name="ccy">Currency of reserve asset.</param>
        /// <param name="longInstrument">Long reserve asset.</param>
        /// <param name="shortInstrument">Short reserve asset.</param>
        public void AddReserve(Currency ccy, Instrument longInstrument, Instrument shortInstrument)
        {
            this.AddReserveMemory(ccy, longInstrument, shortInstrument);
            if (!SimulationObject)
                Factory.AddReserve(this, ccy, longInstrument, shortInstrument);
        }

        /// <summary>
        /// Function: Checks if an instrument is a reserve asset in this portfolio.
        /// </summary>
        /// <param name="instrument">Instrument to check.</param>
        public Boolean IsReserve(Instrument instrument)
        {
            Currency ccy = instrument.Currency;
            Instrument longReserve = Reserve(ccy, PositionType.Long);
            if (instrument == longReserve)
                return true;

            Instrument shortReserve = Reserve(ccy, PositionType.Short);
            if (instrument == shortReserve)
                return true;

            return false;
        }

        /// <summary>
        /// Function: Position of the reserve asset linked to a given currency and date.
        /// </summary>
        /// <param name="date">Date of query.</param>
        /// <param name="ccy">Currency of reserve asset.</param>        
        public Position GetReservePosition(DateTime date, Currency ccy)
        {
            Position reservePosition = FindPosition(Reserve(ccy, PositionType.Long), date);

            if (reservePosition == null)
                reservePosition = FindPosition(Reserve(ccy, PositionType.Short), date);

            if (reservePosition == null)
                return null;

            return reservePosition;
        }

        /// <summary>
        /// Function: Update the Notional of the Portfolio and positions are also update proportionally to the notional change.
        /// </summary>       
        /// <param name="date">DateTime valued date.
        /// </param>
        /// <param name="notional">double valued notional to be updated
        /// </param>
        /// <param name="tstype">Type of time series object.
        /// </param>
        public void UpdateNotional(DateTime date, double notional)
        {
            UpdateNotional(date, notional, false);
        }

        /// <summary>
        /// Function: Update the Notional of the Portfolio. If onlyUpdateTimestamp is false, positions are also update proportionally to the notional change.
        /// </summary>       
        /// <param name="date">DateTime valued date.
        /// </param>
        /// <param name="notional">double valued notional to be updated
        /// </param>
        /// <param name="tstype">Type of time series object.
        /// </param>
        /// <param name="onlyUpdateTimestamp">If true, only update the timestamp of the positions in this portfolio.
        /// </param>
        public void UpdateNotional(DateTime date, double notional, bool onlyUpdateTimestamp)
        {
            if (onlyUpdateTimestamp)
                UpdatePositions(date);
            else
            {
                List<Position> positions = this.Positions(date);

                if (positions != null && positions.Count != 0)
                {
                    double abs_notional = this[date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, null];

                    if (notional != abs_notional)
                    {
                        UpdateReservePosition(date, notional - abs_notional, Currency);

                        for (int i = 0; i < positions.Count; i++)
                        {
                            Position p = positions[i];
                            double sub_unit = p.Unit * (notional / abs_notional);

                            if (!IsReserve(p.Instrument))
                            {
                                double executionLevel = p.Instrument[date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last] * (p.Instrument.InstrumentType == InstrumentType.Future ? (p.Instrument as Future).PointSize : 1.0); ;

                                if (p.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio)
                                {
                                    double sub_notional = p.NotionalValue(date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, Currency, true) * (notional / abs_notional);

                                    Portfolio prt = Portfolio.FindPortfolio(p.Instrument);
                                    prt.UpdateNotional(date, sub_notional);
                                    CreatePosition(p.Instrument, date, p.Unit, sub_notional, false, true, false, true, false, false, false);
                                }
                                else if (p.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy)
                                {
                                    Strategy strategy = Strategy.FindStrategy(p.Instrument);
                                    if (strategy.Portfolio != null)
                                    {
                                        double old_sub_notional = p.NotionalValue(date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, Currency, true);
                                        old_sub_notional = 0.0;

                                        double sub_notional = (old_sub_notional == 0 ? 1 : old_sub_notional) * (notional / abs_notional);

                                        strategy.UpdateAUM(date, sub_notional, true);
                                        p.UpdatePosition(date, p.Unit, sub_notional, RebalancingType.Reserve, UpdateType.OverrideNotional);
                                    }
                                    else
                                        p.UpdatePosition(date, sub_unit, executionLevel, RebalancingType.Reserve, UpdateType.OverrideUnits);
                                }
                                else
                                    p.UpdatePosition(date, sub_unit, executionLevel, RebalancingType.Reserve, UpdateType.OverrideUnits);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Function: Update the Notional of the Portfolio and generate orders that proportionally update the units of the positions in the portfolio.
        /// </summary>       
        /// <param name="orderDate">DateTime valued date.
        /// </param>
        /// <param name="notional">double valued notional to be updated
        /// </param>
        /// <param name="tstype">Type of time series object.
        /// </param>
        public void UpdateNotionalOrder(DateTime orderDate, double notional, TimeSeriesType tstype)
        {
            Dictionary<int, VirtualPosition> positions = this.PositionNewMarketOrders(orderDate);

            if (positions != null && positions.Count != 0)
            {
                double abs_notional = Strategy == null ? this[orderDate, tstype, DataProvider.DefaultProvider, TimeSeriesRollType.Last] : Strategy.GetNextAUM(orderDate, tstype);

                if (notional != abs_notional)
                {
                    foreach (VirtualPosition p in positions.Values)
                    {
                        if (!this.IsReserve(p.Instrument))
                        {
                            double sub_unit = p.Unit * (notional / (abs_notional == 0.0 ? 1.0 : abs_notional));

                            Position position = FindPosition(p.Instrument, orderDate);
                            Dictionary<string, Order> orders = FindMarketOrder(p.Instrument, orderDate, false);

                            Order order = orders != null && orders.Count == 1 ? orders.Values.First() : null;

                            if (p.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio)
                            {
                                Portfolio portfolio = Portfolio.FindPortfolio(p.Instrument);
                                double old_sub_notional = portfolio[orderDate, tstype, DataProvider.DefaultProvider, TimeSeriesRollType.Last];

                                old_sub_notional = old_sub_notional == 0.0 ? 1.0 : old_sub_notional;

                                double sub_notional = old_sub_notional * (notional / (abs_notional == 0.0 ? 1.0 : abs_notional));

                                if (order != null)
                                    order.UpdateTargetMarketOrder(orderDate, sub_notional, UpdateType.OverrideNotional);
                                else if (position != null)
                                    position.UpdateTargetMarketOrder(orderDate, sub_notional, UpdateType.OverrideNotional);
                                else
                                    CreateTargetMarketOrder(p.Instrument, orderDate, sub_notional);
                            }
                            else if (p.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy)
                            {
                                Strategy strategy = Strategy.FindStrategy(p.Instrument);

                                BusinessDay orderDate_local = strategy.Calendar.GetClosestBusinessDay(orderDate, TimeSeries.DateSearchType.Previous);

                                if (strategy.Portfolio != null)
                                {
                                    double old_sub_notional = strategy.GetNextAUM(orderDate_local.DateTime, tstype);

                                    if (double.IsNaN(old_sub_notional))
                                        old_sub_notional = 0.0;

                                    old_sub_notional = old_sub_notional == 0.0 ? 1.0 : old_sub_notional;

                                    double sub_notional = p.Unit == 0 ? 0.0 : old_sub_notional * (notional / (abs_notional == 0.0 ? 1.0 : abs_notional));

                                    if (order != null)
                                        order.UpdateTargetMarketOrder(orderDate, sub_notional, UpdateType.OverrideNotional);
                                    else if (position != null)
                                        position.UpdateTargetMarketOrder(orderDate_local.DateTime, sub_notional, UpdateType.OverrideNotional);
                                    else
                                        CreateTargetMarketOrder(p.Instrument, orderDate_local.DateTime, sub_notional);
                                }
                                else
                                {
                                    if (order != null)
                                        order.UpdateTargetMarketOrder(orderDate, sub_unit, UpdateType.OverrideUnits);

                                    else if (position != null)
                                        position.UpdateTargetMarketOrder(orderDate, sub_unit, UpdateType.OverrideUnits);

                                    else
                                        CreateTargetMarketOrder(p.Instrument, orderDate, sub_unit);
                                }
                            }
                            else
                            {
                                if (order != null && order.Status != OrderStatus.Booked)
                                    order.UpdateTargetMarketOrder(orderDate, sub_unit, UpdateType.OverrideUnits);

                                else if (position != null)
                                    position.UpdateTargetMarketOrder(orderDate, sub_unit, UpdateType.OverrideUnits);

                                else
                                    CreateTargetMarketOrder(p.Instrument, orderDate, sub_unit);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Function: Update the portfolio's reserve position denominated in a specific currency.
        /// </summary>       
        /// <param name="orderDate">DateTime valued date.
        /// </param>
        /// <param name="notional">double valued notional to be updated
        /// </param>
        /// <param name="ccy">Denomination currency.
        /// </param>
        public Position UpdateReservePosition(DateTime date, double notional, Currency ccy)
        {
            return UpdateReservePosition(date, notional, ccy, true, false);
        }


        /// <summary>
        /// Function: Update the the timestamp of all the positions in the portfolio on a given date.
        /// </summary>       
        /// <param name="date">DateTime valued date.
        /// </param>
        public void UpdatePositions(DateTime date)
        {
            if (ParentPortfolio != null)
                ParentPortfolio.UpdatePositions(date);

            if (date > LastTimestamp)
            {
                List<Position> pList = Positions(date, false);
                List<Position> apList = Positions(date, true);

                if (pList != null)
                    for (int i = 0; i < pList.Count; i++)
                    {
                        Position pos = pList[i];
                        CreatePosition(pos.Instrument, date, pos.Unit, double.NaN, true, true, false, true, false, false, false);
                    }


                if (apList != null)
                    for (int i = 0; i < apList.Count; i++)
                    {
                        Position pos = apList[i];
                        CreatePosition(pos.Instrument, date, pos.Unit, double.NaN, true, true, false, true, false, false, true);
                    }
            }
        }

        /// <summary>
        /// Function: Update the reserve asset with a given currency denonimation of the Portfolio. If update_all_flag is true, the timestamp of all positions are also update.
        /// </summary>       
        /// <param name="date">DateTime valued date.
        /// </param>
        /// <param name="update_value">double valued notional to be updated
        /// </param>
        /// <param name="ccy">Denomination of the reserve asset to update.
        /// </param>
        /// <param name="update_all_flag">If true, update the timestamp of all positions.
        /// </param>
        public Position UpdateReservePosition(DateTime date, double update_value, Currency ccy, bool update_all_flag, bool updateParents)
        {
            if (update_all_flag)
                UpdatePositions(date);

            double notional = 0;
            Position reserve = GetReservePosition(date, ccy);

            if (reserve != null)
                notional = reserve.Value(date, ccy);

            double oldNotional = notional;
            notional += update_value;


            Position result = null;
            if (reserve != null)
            {
                double value = reserve.Instrument[date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last];
                double executionLevel = value;

                if (Reserve(ccy, PositionType.Long) == Reserve(ccy, PositionType.Short))
                    result = reserve.UpdatePosition(date, notional / value, executionLevel, RebalancingType.Internal, UpdateType.OverrideUnits);

                else if (reserve.Instrument == Reserve(ccy, PositionType.Long))
                {
                    if (notional > 0)
                        result = reserve.UpdatePosition(date, notional / value, executionLevel, RebalancingType.Internal, UpdateType.OverrideUnits);
                    else if (notional <= 0)
                    {
                        reserve.UpdatePosition(date, 0, executionLevel, RebalancingType.Internal, UpdateType.OverrideUnits);
                        result = CreatePosition(Reserve(ccy, PositionType.Short), date, notional / Reserve(ccy, PositionType.Short)[date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last], notional);
                    }
                }
                else
                {
                    if (notional < 0)
                        result = reserve.UpdatePosition(date, notional / value, executionLevel, RebalancingType.Internal, UpdateType.OverrideUnits);
                    else if (notional >= 0)
                    {
                        reserve.UpdatePosition(date, 0, executionLevel, RebalancingType.Internal, UpdateType.OverrideUnits);
                        result = CreatePosition(Reserve(ccy, PositionType.Long), date, notional / Reserve(ccy, PositionType.Long)[date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last], notional);
                    }
                }

                return reserve;
            }
            else
            {
                if (notional > 0)
                    result = CreatePosition(Reserve(ccy, PositionType.Long), date, notional / Reserve(ccy, PositionType.Long)[date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last], notional);
                else
                    result = CreatePosition(Reserve(ccy, PositionType.Short), date, notional / Reserve(ccy, PositionType.Short)[date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last], notional);
            }

            return result;
        }

        /// <summary>
        /// Function: Retrieve weighted value of the positions in the portfolio.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the value to be retrieved.
        /// </param>
        /// <param name="type">Time series type of time series point.
        /// </param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).
        /// </param>
        /// <param name="timeSeriesRoll">Roll type of reference time series object.
        /// </param>
        public override double this[DateTime date, TimeSeriesType type, DataProvider provider, TimeSeriesRollType timeSeriesRoll]
        {
            get
            {
                List<Position> pList = Positions(date, false);

                double pvalue = 0;

                if (pList != null)
                    for (int i = 0; i < pList.Count; i++)
                    {
                        Position pos = pList[i];
                        pvalue += pos.Value(date, type, provider, timeSeriesRoll);//, false);
                    }

                return pvalue;
            }
        }

        /// <summary>
        /// Function: Retrieve weighted value of the positions in the portfolio.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the value to be retrieved.
        /// </param>
        /// <param name="type">Time series type of time series point.
        /// </param>
        /// <param name="timeSeriesRoll">Roll type of reference time series object.
        /// </param>
        public override double this[DateTime date, TimeSeriesType type, TimeSeriesRollType timeSeriesRoll]
        {
            get
            {
                return this[date, type, DataProvider.DefaultProvider, timeSeriesRoll];
            }
        }


        /// <summary>
        /// Function: Retrieve weighted value of the positions of a given currency denomination in the portfolio.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the value to be retrieved.
        /// </param>
        /// <param name="type">Time series type of time series point.
        /// </param>
        /// <param name="provider">Provider of reference time series object (Standard is AQI).
        /// </param>
        /// <param name="timeSeriesRoll">Roll type of reference time series object.
        /// </param>
        /// <param name="ccyFilter">Currency denomination to filter the positions.
        /// </param>
        public double this[DateTime date, TimeSeriesType type, DataProvider provider, TimeSeriesRollType timeSeriesRoll, Currency ccyFilter]//, bool useExecutionValues]
        {
            get
            {
                List<Position> pList = Positions(date, false);

                double pvalue = 0;

                if (pList != null)
                    for (int i = 0; i < pList.Count; i++)
                    {
                        Position pos = pList[i];
                        if (ccyFilter == null ? true : pos.Instrument.Currency == ccyFilter)
                            pvalue += pos.Value(date, type, provider, timeSeriesRoll, ccyFilter == null ? Currency : ccyFilter);//, useExecutionValues);               
                    }

                return pvalue;
            }
        }

        /// <summary>
        /// Function: Retrieve weighted value of the positions in the portfolio.
        /// </summary>       
        /// <param name="date">DateTime value representing the date of the value to be retrieved.
        /// </param>
        public override double this[DateTime date]
        {
            get
            {
                return this[date, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRoll];
            }
        }

        /// <summary>
        /// Function: Remove this portfolio from memory and persistent storage
        /// </summary>       
        new public void Remove()
        {
            Market.RemovePortfolio(this);
            Factory.Remove(this);
            base.Remove();
        }

        /// <summary>
        /// Function: Remove the portfolio data including orders and positions from a give date and forward
        /// </summary>    
        /// <param name="date">DateTime value representing the date of the value to be removed.
        /// </param>
        new public void RemoveFrom(DateTime date)
        {
            Factory.RemoveFrom(this, date);
        }

        /// <summary>
        /// Function: Remove orders from the portfolio starting from a give date and forward
        /// </summary>    
        /// <param name="date">DateTime value representing the date of the value to be removed.
        /// </param>
        public void RemoveOrdersFrom(DateTime date)
        {
            Factory.RemoveOrdersFrom(this, date);
        }

        /// <summary>
        /// Function: Remove positions from the portfolio starting from a give date and forward
        /// </summary>    
        /// <param name="date">DateTime value representing the date of the value to be removed.
        /// </param>
        public void RemovePositionsFrom(DateTime date)
        {
            Factory.RemovePositionsFrom(this, date);
        }

        /// <summary>
        /// Function: Remove reserve assets from the portfolio
        /// </summary>    
        public void RemoveReserves()
        {
            Factory.RemoveReserves(this);
        }

        /// <summary>
        /// Function: Returns a dictionary with ID, Order for all orders created on a given date
        /// </summary>    
        /// <param name="instrument">instrument linked to the orders to be returned
        /// </param>
        /// <param name="orderDate">DateTime value representing the date of the orders to be returned.
        /// </param>
        public Dictionary<string, Order> FindOrder(Instrument instrument, DateTime orderDate)
        {
            return FindOrder(instrument, orderDate, false);
        }

        /// <summary>
        /// Function: Returns an order with ID
        /// </summary>    
        /// <param name="id">string value representing the id of the order to be returned
        /// </param>
        /// <param name="aggregated">true if looking through sub portfolios for the order
        /// </param>
        public Order FindOrder(string id, Boolean aggregated)
        {
            if (aggregated)
            {
                if (_orderMemory_aggregated.ContainsKey(id))
                    return _orderMemory_aggregated[id];
            }
            else
            {
                if (_orderMemory.ContainsKey(id))
                    return _orderMemory[id];
            }

            return null;
        }

        /// <summary>
        /// Function: Returns a dictionary with ID, Order for all Market orders created on a given date
        /// </summary>    
        /// <param name="instrument">instrument linked to the orders to be returned
        /// </param>
        /// <param name="orderDate">DateTime value representing the date of the orders to be returned.
        /// </param>
        public Dictionary<string, Order> FindMarketOrder(Instrument instrument, DateTime orderDate, Boolean aggregated)
        {
            DateTime lt = orderDate;

            if (aggregated)
            {
                if (_orderMemory_aggregated_orderDate.ContainsKey(lt) && _orderMemory_aggregated_orderDate[lt].ContainsKey(instrument.ID))// && _orderMemory_aggregated_orderDate[lt][ttype].ContainsKey(instrument.ID))
                {
                    ConcurrentDictionary<string, Order> o = _orderMemory_aggregated_orderDate[lt][instrument.ID];

                    Dictionary<string, Order> res = new Dictionary<string, Order>();

                    if (o != null)
                        foreach (Order order in o.Values.ToList())
                            if (order.Type == OrderType.Market)
                                res.Add(order.ID, order);
                    return res;
                }
            }
            else
            {
                if (_orderMemory_orderDate.ContainsKey(lt) && _orderMemory_orderDate[lt].ContainsKey(instrument.ID))// && _orderMemory_orderDate[lt][instrument.ID].ContainsKey(instrument.ID))
                {
                    ConcurrentDictionary<string, Order> o = _orderMemory_orderDate[lt][instrument.ID];

                    Dictionary<string, Order> res = new Dictionary<string, Order>();

                    if (o != null)
                        foreach (Order order in o.Values.ToList())
                            if (order.Type == OrderType.Market)
                                res.Add(order.ID, order);
                    return res;
                }
            }

            if (SimulationObject)
                return null;

            if (_loadedDates.ContainsKey(orderDate.Date))
                return null;

            return null;
        }

        /// <summary>
        /// Function: Returns a dictionary with ID, Order for all orders created on a given date
        /// </summary>    
        /// <param name="instrument">instrument linked to the orders to be returned
        /// </param>
        /// <param name="orderDate">DateTime value representing the date of the orders to be returned.
        /// </param>
        /// <param name="aggregated">true if orders are to retrieved from subportfolios
        /// </param>        
        public Dictionary<string, Order> FindOrder(Instrument instrument, DateTime orderDate, Boolean aggregated)
        {
            DateTime lt = orderDate;

            if (aggregated)
            {
                if (_orderMemory_aggregated_orderDate.ContainsKey(lt) && _orderMemory_aggregated_orderDate[lt].ContainsKey(instrument.ID))// && _orderMemory_aggregated_orderDate[lt][ttype].ContainsKey(instrument.ID))
                {
                    ConcurrentDictionary<string, Order> o = _orderMemory_aggregated_orderDate[lt][instrument.ID];
                    return o.ToDictionary(entry => entry.Key, entry => entry.Value);
                }
            }
            else
            {
                if (_orderMemory_orderDate.ContainsKey(lt) && _orderMemory_orderDate[lt].ContainsKey(instrument.ID))// && _orderMemory_orderDate[lt][instrument.ID].ContainsKey(instrument.ID))
                {
                    ConcurrentDictionary<string, Order> o = _orderMemory_orderDate[lt][instrument.ID];
                    return o.ToDictionary(entry => entry.Key, entry => entry.Value);
                }
            }

            if (SimulationObject)
                return null;

            if (_loadedDates.ContainsKey(orderDate.Date))
                return null;

            return null;
        }


        /// <summary>
        /// Function: Returns a position in a given instrument at a specific timestamp
        /// </summary>    
        /// <param name="instrument">instrument linked to the position to be returned
        /// </param>
        /// <param name="timestamp">DateTime value representing the date of the position to be returned.
        /// </param>
        public Position FindPosition(Instrument instrument, DateTime timestamp)
        {
            return FindPosition(instrument, timestamp, false);
        }

        /// <summary>
        /// Function: Returns a position in a given instrument at a specific timestamp
        /// </summary>    
        /// <param name="instrument">instrument linked to the position to be returned
        /// </param>
        /// <param name="timestamp">DateTime value representing the date of the position to be returned.
        /// </param>
        /// <param name="aggregated">true if position are to retrieved from subportfolios
        /// </param>
        public Position FindPosition(Instrument instrument, DateTime timestamp, Boolean aggregated)
        {
            DateTime lt;

            try { lt = GetLastTimestamp(timestamp); }
            catch { return null; }

            if (lt == DateTime.MinValue)
                return null;

            if (aggregated)
            {
                if (_positionHistoryMemory_date_aggregated.ContainsKey(lt))
                {
                    if (_positionHistoryMemory_date_aggregated[lt].ContainsKey(instrument.ID))
                    {
                        Position p = _positionHistoryMemory_date_aggregated[lt][instrument.ID];
                        if (p.Unit == 0.0)
                            return null;
                        else
                            return p;
                    }
                }
            }
            else
            {
                if (_positionHistoryMemory_date.ContainsKey(lt))
                {
                    if (_positionHistoryMemory_date[lt].ContainsKey(instrument.ID))
                    {
                        Position p = _positionHistoryMemory_date[lt][instrument.ID];
                        if (p.Unit == 0.0)
                            return null;
                        else
                            return p;
                    }
                }
            }

            if (SimulationObject)
                return null;

            if (_loadedDates.ContainsKey(timestamp.Date))
                return null;

            return null;
        }


        /// <summary>
        /// Function: Returns a position in a given instrument at a specific timestamp. If the a position doesn't exist for the specific date, the most recemt position prior to that date is returned.
        /// </summary>    
        /// <param name="instrument">instrument linked to the position to be returned
        /// </param>
        /// <param name="timestamp">DateTime value representing the date of the position to be returned.
        /// </param>
        /// <param name="aggregated">true if position are to retrieved from subportfolios
        /// </param>
        public Dictionary<string, Order> FindLatestPosition(Instrument instrument, DateTime timestamp, Boolean aggregated)
        {
            if (aggregated)
            {
                if (_orderMemory_aggregated_orderDate.Count == 0)
                    return null;

                if (_orderMemory_aggregated_instrument_orderDate.ContainsKey(instrument.ID))
                {
                    DateTime[] dates = _orderMemory_aggregated_instrument_orderDate[instrument.ID].Keys.OrderBy(x => x).ToArray();

                    DateTime lastDate = timestamp;
                    Boolean found = false;
                    for (int i = dates.Length - 1; i >= 0; i--)
                    {
                        if (dates[i] <= lastDate)
                        {
                            lastDate = dates[i];
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        return null;

                    if (found)
                    {
                        Dictionary<string, Order> order = _orderMemory_aggregated_orderDate[lastDate][instrument.ID].ToDictionary(entry => entry.Key, entry => entry.Value); ;
                        return order;
                    }
                }
            }
            else
            {
                if (_orderMemory_orderDate.Count == 0)
                    return null;

                if (_orderMemory_instrument_orderDate.ContainsKey(instrument.ID))
                {
                    {
                        DateTime[] dates = _orderMemory_instrument_orderDate[instrument.ID].Keys.OrderBy(x => x).ToArray();
                        DateTime lastDate = timestamp;
                        Boolean found = false;
                        for (int i = dates.Length - 1; i >= 0; i--)
                        {
                            if (dates[i] <= lastDate)
                            {
                                lastDate = dates[i];
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                            return null;

                        if (found)
                        {
                            Dictionary<string, Order> order = _orderMemory_orderDate[lastDate][instrument.ID].ToDictionary(entry => entry.Key, entry => entry.Value);
                            return order;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Function: Returns the latest position in a given instrument.
        /// </summary>    
        /// <param name="instrument">instrument linked to the position to be returned
        /// </param>
        /// <param name="aggregated">true if position are to retrieved from subportfolios
        /// </param>
        public Position FindLatestPosition(Instrument instrument, Boolean aggregated)
        {
            if (aggregated)
            {
                if (_positionHistoryMemory_date_aggregated.ContainsKey(LastTimestamp))
                    if (_positionHistoryMemory_date_aggregated[LastTimestamp].ContainsKey(instrument.ID))
                        return _positionHistoryMemory_date_aggregated[LastTimestamp][instrument.ID];

                if (_positionHistoryMemory_instrument_aggregated.ContainsKey(instrument.ID))
                {
                    if (_positionHistoryMemory_instrument_aggregated[instrument.ID].Keys.Count == 0)
                        return null;

                    DateTime dt = _positionHistoryMemory_instrument_aggregated[instrument.ID].Keys.Max();
                    Position p = _positionHistoryMemory_instrument_aggregated[instrument.ID][dt];
                    if (p.Unit == 0)
                        return null;
                    else
                        return p;
                }
            }
            else
            {
                if (_positionHistoryMemory_date.ContainsKey(LastTimestamp))
                    if (_positionHistoryMemory_date[LastTimestamp].ContainsKey(instrument.ID))
                        return _positionHistoryMemory_date[LastTimestamp][instrument.ID];

                if (_positionHistoryMemory_instrument.ContainsKey(instrument.ID))
                {
                    if (_positionHistoryMemory_instrument[instrument.ID].Keys.Count == 0)
                        return null;

                    DateTime dt = _positionHistoryMemory_instrument[instrument.ID].Keys.Max();
                    Position p = _positionHistoryMemory_instrument[instrument.ID][dt];
                    if (p.Unit == 0)
                        return null;
                    else
                        return p;
                }
            }

            return null;
        }


        /// <summary>
        /// Function: Returns a list of positions exisiting at a given time
        /// </summary>    
        /// <param name="timestamp">reference timestamp for position retrieval
        /// </param>
        /// <param name="aggregated">true if positions are to retrieved from subportfolios
        /// </param>
        public List<Position> Positions(DateTime timestamp)
        {
            return Positions(timestamp, false);
        }

        /// <summary>
        /// Function: Returns a list of non-cash (reserve) positions exisiting at a given time
        /// </summary>    
        /// <param name="timestamp">reference timestamp for position retrieval
        /// </param>
        /// <param name="aggregated">true if positions are to retrieved from subportfolios
        /// </param>
        public List<Position> RiskPositions(DateTime timestamp, Boolean aggregated)
        {
            if (aggregated)
            {
                if (_positionHistoryMemory_date_aggregated.Count == 0)
                    return null;

                DateTime latestdate = GetLastTimestamp(timestamp);

                List<Position> res = new List<Position>();

                if (_positionHistoryMemory_date_aggregated.ContainsKey(latestdate))
                    foreach (Position p in _positionHistoryMemory_date_aggregated[latestdate].Values.ToList())
                    {
                        if (Math.Abs(p.Unit) > 0 && !IsReserve(p.Instrument))
                            res.Add(p);
                    }

                if (res.Count == 0)
                    return null;
                else if (res.Count != 0)
                    return res;
            }
            else
            {
                if (_positionHistoryMemory_date.Count == 0)
                    return null;

                DateTime latestdate = GetLastTimestamp(timestamp);

                List<Position> res = new List<Position>();

                if (_positionHistoryMemory_date.ContainsKey(latestdate))
                    foreach (Position p in _positionHistoryMemory_date[latestdate].Values.ToList())
                    {
                        if (Math.Abs(p.Unit) > 0 && !IsReserve(p.Instrument))
                            res.Add(p);
                    }

                if (res.Count == 0)
                    return null;
                else if (res.Count != 0)
                    return res;
            }

            return null;
        }

        /// <summary>
        /// Function: Returns a list of positions exisiting at a given time
        /// </summary>    
        /// <param name="timestamp">reference timestamp for position retrieval
        /// </param>
        /// <param name="aggregated">true if positions are to retrieved from subportfolios
        /// </param>
        public List<Position> Positions(DateTime timestamp, Boolean aggregated)
        {
            if (aggregated)
            {
                if (_positionHistoryMemory_date_aggregated.Count == 0)
                    return null;

                DateTime latestdate = GetLastTimestamp(timestamp);

                List<Position> res = new List<Position>();

                if (_positionHistoryMemory_date_aggregated.ContainsKey(latestdate))
                    foreach (Position p in _positionHistoryMemory_date_aggregated[latestdate].Values.ToList())
                    {
                        if (Math.Abs(p.Unit) > 0)
                            res.Add(p);
                    }

                if (res.Count == 0)
                    return null;
                else if (res.Count != 0)
                    return res;
            }
            else
            {
                if (_positionHistoryMemory_date.Count == 0)
                    return null;

                DateTime latestdate = GetLastTimestamp(timestamp);

                List<Position> res = new List<Position>();

                if (_positionHistoryMemory_date.ContainsKey(latestdate))
                    foreach (Position p in _positionHistoryMemory_date[latestdate].Values.ToList())
                    {
                        if (Math.Abs(p.Unit) > 0)
                            res.Add(p);
                    }

                if (res.Count == 0)
                    return null;
                else if (res.Count != 0)
                    return res;
            }

            return null;
        }

        /// <summary>
        /// Function: returns the unit of an instrument aggregated across of positions in sub portfolios
        /// </summary>    
        /// <param name="instrument">reference instrument
        /// </param>
        /// <param name="timestamp">reference timestamp
        /// </param>
        public double AggregatedUnit(Instrument instrument, DateTime timestamp)
        {
            Position ap = FindPosition(instrument, timestamp, true);
            if (ap != null)
                return ap.Unit;

            return double.NaN;
        }

        /// <summary>
        /// Function: Returns a dictionary all orders for linked to all instruments exisiting at a given time.
        /// The function returns a nested dictionary where the inner dictionary is a set of orders linked to a given instrument
        /// and the outer dictionary is a set of the inner dictionaries linked for each instrument.
        /// </summary>    
        /// <param name="timestamp">reference timestamp for orders retrieval
        /// </param>
        public Dictionary<int, Dictionary<string, Order>> Orders(DateTime timestamp)
        {
            return Orders(timestamp, false);
        }

        /// <summary>
        /// Function: Removes new orders from the internal memory. This function is used during the tree re-execution algorithm.
        /// </summary>    
        /// <param name="timestamp">reference timestamp
        /// </param>
        public void ClearOrders(DateTime timestamp)
        {
            if (_orderMemory_aggregated_orderDate.ContainsKey(timestamp.Date))
            {
                ConcurrentDictionary<int, ConcurrentDictionary<string, Order>> orders = _orderMemory_aggregated_orderDate[timestamp.Date];
                if (orders != null)
                    foreach (int id in orders.Keys.ToList())
                    {
                        foreach (Order o in orders[id].Values.ToList())
                        {
                            if (o.OrderDate == timestamp)
                            {
                                Order v = null;
                                
                                if (_orderMemory_aggregated.ContainsKey(o.ID))
                                    _orderMemory_aggregated.TryRemove(o.ID, out v);

                                if (_orderNewMemory_aggregated.ContainsKey(o.ID))
                                    _orderNewMemory_aggregated.TryRemove(o.ID, out v);
                            }
                        }

                        if (_orderMemory_aggregated_instrument_orderDate.ContainsKey(id))
                        {
                            Order v = null;
                            if (_orderMemory_aggregated_instrument_orderDate[id].ContainsKey(timestamp.Date))
                                foreach (var i in _orderMemory_aggregated_instrument_orderDate[id][timestamp.Date].Values.ToList())
                                    if (i.OrderDate == timestamp)
                                        _orderMemory_aggregated_instrument_orderDate[id][timestamp.Date].TryRemove(i.ID, out v);
                        }

                        Order v2 = null;
                        foreach (Order o in _orderMemory_aggregated_orderDate[timestamp.Date][id].Values.ToList())
                            if (o.OrderDate == timestamp)
                                _orderMemory_aggregated_orderDate[timestamp.Date][id].TryRemove(o.ID, out v2);
                    }
            }

            if (_orderMemory_orderDate.ContainsKey(timestamp.Date))
            {
                ConcurrentDictionary<int, ConcurrentDictionary<string, Order>> orders = _orderMemory_orderDate[timestamp.Date];
                if (orders != null)
                    foreach (int id in orders.Keys.ToList())
                    {
                        foreach (Order o in orders[id].Values)
                        {
                            Order v = null;
                            if (_orderMemory.ContainsKey(o.ID) && o.OrderDate == timestamp)
                                _orderMemory.TryRemove(o.ID, out v);

                            if (_orderNewMemory.ContainsKey(o.ID) && o.OrderDate == timestamp)
                                _orderNewMemory.TryRemove(o.ID, out v);
                        }

                        Order v2 = null;
                        if (_orderMemory_instrument_orderDate.ContainsKey(id))
                        {
                            if (_orderMemory_instrument_orderDate[id].ContainsKey(timestamp.Date))
                                foreach (Order o in _orderMemory_instrument_orderDate[id][timestamp.Date].Values.ToList())
                                    if (o.OrderDate == timestamp)
                                        _orderMemory_instrument_orderDate[id][timestamp.Date].TryRemove(o.ID, out v2);
                        }

                        foreach (Order o in _orderMemory_orderDate[timestamp.Date][id].Values.ToList())
                            if (o.OrderDate == timestamp)
                                _orderMemory_orderDate[timestamp.Date][id].TryRemove(o.ID, out v2);
                    }
            }
        }

        /// <summary>
        /// Function: Returns a dictionary all OPEN orders for linked to a given instruemnt at a given time.
        /// The function returns a dictionary where the key is the order ID and the value is the order.
        /// </summary>    
        /// <param name="timestamp">reference timestamp for orders retrieval
        /// </param>
        /// <param name="aggregated">true if orders are to retrieved from subportfolios
        /// </param>
        public Dictionary<string, Order> FindOpenOrder(Instrument instrument, DateTime timestamp, Boolean aggregated)
        {
            if (aggregated)
            {
                if (_orderMemory_aggregated_orderDate.Count == 0)
                    return new Dictionary<string, Order>();

                if (_orderMemory_aggregated_instrument_orderDate.ContainsKey(instrument.ID))
                {
                    {
                        DateTime[] dates = _orderMemory_aggregated_instrument_orderDate[instrument.ID].Keys.ToList().OrderBy(x => x).ToArray();
                        DateTime lastDate = timestamp;
                        Boolean found = false;
                        for (int i = dates.Length - 1; i >= 0; i--)
                        {
                            if (dates[i] <= lastDate)
                            {
                                lastDate = dates[i];
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                            return new Dictionary<string, Order>();

                        if (found)
                        {
                            ConcurrentDictionary<string, Order> orders = _orderMemory_aggregated_orderDate[lastDate][instrument.ID];
                            Dictionary<string, Order> res = new Dictionary<string, Order>();
                            foreach (Order order in orders.Values.ToList())
                                if (order.Status != OrderStatus.Booked && order.Status != OrderStatus.NotExecuted && order.OrderDate.Date == timestamp.Date)
                                    res.Add(order.ID, order);

                            return res;
                        }
                    }
                }
            }
            else
            {
                if (_orderMemory_orderDate.Count == 0)
                    return new Dictionary<string, Order>();

                if (_orderMemory_instrument_orderDate.ContainsKey(instrument.ID))
                {
                    {
                        DateTime[] dates = _orderMemory_instrument_orderDate[instrument.ID].Keys.ToList().OrderBy(x => x).ToArray();
                        DateTime lastDate = timestamp;
                        Boolean found = false;
                        for (int i = dates.Length - 1; i >= 0; i--)
                        {
                            if (dates[i] <= lastDate)
                            {
                                lastDate = dates[i];
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                            return new Dictionary<string, Order>();

                        if (found)
                        {
                            ConcurrentDictionary<string, Order> orders = _orderMemory_orderDate[lastDate][instrument.ID];
                            Dictionary<string, Order> res = new Dictionary<string, Order>();

                            foreach (Order order in orders.Values.ToList())
                                if (order.Status != OrderStatus.Booked && order.Status != OrderStatus.NotExecuted && order.OrderDate.Date == timestamp.Date)
                                    res.Add(order.ID, order);

                            return res;
                        }
                    }
                }
            }

            return new Dictionary<string, Order>();
        }

        /// <summary>
        /// Function: Returns a dictionary all OPEN orders for linked to all instruments exisiting at a given time.
        /// The function returns a nested dictionary where the inner dictionary is a set of orders linked to a given instrument
        /// and the outer dictionary is a set of the inner dictionaries linked for each instrument.
        /// </summary>    
        /// <param name="timestamp">reference timestamp for orders retrieval
        /// </param>
        /// <param name="aggregated">true if orders are to retrieved from subportfolios
        /// </param>
        public Dictionary<int, Dictionary<string, Order>> OpenOrders(DateTime timestamp, Boolean aggregated)
        {
            timestamp = timestamp.Date;
            {
                Dictionary<int, Dictionary<string, Order>> orders = new Dictionary<int, Dictionary<string, Order>>();
                if (aggregated)
                {
                    if (_orderMemory_aggregated_orderDate.Count == 0)
                        return null;

                    foreach (int id in _orderMemory_aggregated_instrument_orderDate.Keys.ToList())
                    {
                        Instrument instrument = Instrument.FindInstrument(id);

                        DateTime[] dates = _orderMemory_aggregated_instrument_orderDate[id].Keys.ToList().OrderBy(x => x).ToArray();
                        DateTime lastDate = timestamp;
                        for (int i = dates.Length - 1; i >= 0; i--)
                        {
                            if (dates[i] <= lastDate)
                            {
                                lastDate = dates[i];
                                break;
                            }
                        }

                        if (_orderMemory_aggregated_orderDate.ContainsKey(lastDate) && _orderMemory_aggregated_orderDate[lastDate].ContainsKey(id))
                        {
                            ConcurrentDictionary<string, Order> os = _orderMemory_aggregated_orderDate[lastDate][id];
                            Dictionary<string, Order> res = new Dictionary<string, Order>();
                            foreach (Order order in os.Values.ToList())
                                if (order.Status != OrderStatus.Booked && order.Status != OrderStatus.NotExecuted && order.OrderDate.Date == timestamp.Date)
                                    res.Add(order.ID, order);

                            if (res.Count > 0)
                                orders.Add(instrument.ID, res);
                        }
                    }

                    if (orders.Count != 0)
                        return orders;
                }
                else
                {
                    if (_orderMemory_orderDate.Count == 0)
                        return null;

                    foreach (int id in _orderMemory_instrument_orderDate.Keys.ToList())
                    {
                        Instrument instrument = Instrument.FindInstrument(id);

                        DateTime[] dates = _orderMemory_instrument_orderDate[id].Keys.ToList().OrderBy(x => x).ToArray();

                        if (dates.Length > 0)
                        {
                            DateTime lastDate = timestamp;
                            for (int i = dates.Length - 1; i >= 0; i--)
                            {
                                if (dates[i] <= lastDate)
                                {
                                    lastDate = dates[i];
                                    break;
                                }
                            }

                            if (_orderMemory_orderDate.ContainsKey(lastDate) && _orderMemory_orderDate[lastDate].ContainsKey(id))
                            {
                                ConcurrentDictionary<string, Order> os = _orderMemory_orderDate[lastDate][id];
                                Dictionary<string, Order> res = new Dictionary<string, Order>();

                                foreach (Order order in os.Values.ToList())
                                    if (order.Status != OrderStatus.Booked && order.Status != OrderStatus.NotExecuted && order.OrderDate.Date == timestamp.Date)
                                        res.Add(order.ID, order);


                                if (res.Count > 0)
                                    orders.Add(instrument.ID, res);
                            }
                        }
                    }

                    if (orders.Count != 0)
                        return orders;
                }
            }

            if (_loadedDates.ContainsKey(timestamp.Date))
                return null;

            return null;
        }

        /// <summary>
        /// Function: Returns a dictionary all NON-EXECUTED orders for linked to a given instruemnt at a given time.
        /// The function returns a dictionary where the key is the order ID and the value is the order.
        /// </summary>    
        /// <param name="timestamp">reference timestamp for orders retrieval
        /// </param>
        /// <param name="aggregated">true if orders are to retrieved from subportfolios
        /// </param>
        public Dictionary<string, Order> FindNonExecutedOrder(Instrument instrument, DateTime timestamp, TimeSeriesType ttype, Boolean aggregated)
        {
            if (aggregated)
            {
                if (_orderMemory_aggregated_orderDate.Count == 0)
                    return null;

                if (_orderMemory_aggregated_instrument_orderDate.ContainsKey(instrument.ID))
                {
                    DateTime[] dates = _orderMemory_aggregated_instrument_orderDate[instrument.ID].Keys.OrderBy(x => x).ToArray();

                    DateTime lastDate = timestamp;
                    Boolean found = false;
                    for (int i = dates.Length - 1; i >= 0; i--)
                    {
                        if (dates[i] <= lastDate)
                        {
                            lastDate = dates[i];
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        return null;

                    if (found)
                    {
                        ConcurrentDictionary<string, Order> orders = _orderMemory_aggregated_orderDate[lastDate][instrument.ID];
                        Dictionary<string, Order> res = new Dictionary<string, Order>();
                        foreach (Order order in orders.Values)
                            if (order.Status == OrderStatus.NotExecuted || order.Status == OrderStatus.New || order.Status == OrderStatus.Submitted)
                                res.Add(order.ID, order);

                        if (res.Count > 0)
                            return res;
                    }
                }
            }
            else
            {
                if (_orderMemory_orderDate.Count == 0)
                    return null;

                if (_orderMemory_instrument_orderDate.ContainsKey(instrument.ID))
                {
                    DateTime[] dates = _orderMemory_instrument_orderDate[instrument.ID].Keys.OrderBy(x => x).ToArray();

                    DateTime lastDate = timestamp;
                    Boolean found = false;
                    for (int i = dates.Length - 1; i >= 0; i--)
                    {
                        if (dates[i] <= lastDate)
                        {
                            lastDate = dates[i];
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        return null;

                    if (found)
                    {
                        ConcurrentDictionary<string, Order> orders = _orderMemory_orderDate[lastDate][instrument.ID];
                        Dictionary<string, Order> res = new Dictionary<string, Order>();
                        foreach (Order order in orders.Values)
                            if (order.Status == OrderStatus.NotExecuted || order.Status == OrderStatus.New || order.Status == OrderStatus.Submitted)
                                res.Add(order.ID, order);

                        if (res.Count > 0)
                            return res;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Function: Returns a dictionary all EXECUTED orders for linked to all instruments exisiting at a given time.
        /// The function returns a nested dictionary where the inner dictionary is a set of orders linked to a given instrument
        /// and the outer dictionary is a set of the inner dictionaries linked for each instrument.
        /// </summary>    
        /// <param name="timestamp">reference timestamp for orders retrieval
        /// </param>
        /// <param name="aggregated">true if orders are to retrieved from subportfolios
        /// </param>
        public Dictionary<int, Dictionary<string, Order>> ExecutedOrders(DateTime timestamp, Boolean aggregated)
        {
            Dictionary<int, Dictionary<string, Order>> orders = new Dictionary<int, Dictionary<string, Order>>();

            if (aggregated)
            {
                if (_orderMemory_aggregated_orderDate.Count == 0)
                    return null;

                foreach (int id in _orderMemory_aggregated_instrument_orderDate.Keys)
                {
                    Instrument instrument = Instrument.FindInstrument(id);

                    DateTime[] dates = _orderMemory_aggregated_instrument_orderDate[id].Keys.OrderBy(x => x).ToArray();

                    if (dates.Length > 0)
                    {
                        DateTime lastDate = timestamp;
                        for (int i = dates.Length - 1; i >= 0; i--)
                        {
                            if (dates[i] <= lastDate)
                            {
                                lastDate = dates[i];
                                break;
                            }
                        }

                        if (_orderMemory_aggregated_orderDate.ContainsKey(lastDate))
                        {
                            ConcurrentDictionary<string, Order> os = _orderMemory_aggregated_orderDate[lastDate][id];
                            Dictionary<string, Order> res = new Dictionary<string, Order>();
                            foreach (Order order in os.Values)
                                if (order.Status == OrderStatus.Executed && order.ExecutionDate == timestamp)
                                    res.Add(order.ID, order);

                            if (res.Count > 0)
                                orders.Add(instrument.ID, res);
                        }
                    }
                }

                if (orders.Count != 0)
                    return orders;
            }
            else
            {
                if (_orderMemory_orderDate.Count == 0)
                    return null;

                foreach (int id in _orderMemory_instrument_orderDate.Keys)
                {
                    Instrument instrument = Instrument.FindInstrument(id);

                    DateTime[] dates = _orderMemory_instrument_orderDate[id].Keys.OrderBy(x => x).ToArray();

                    if (dates.Length > 0)
                    {
                        DateTime lastDate = timestamp;
                        for (int i = dates.Length - 1; i >= 0; i--)
                        {
                            if (dates[i] <= lastDate)
                            {
                                lastDate = dates[i];
                                break;
                            }
                        }

                        ConcurrentDictionary<string, Order> os = _orderMemory_orderDate[lastDate][id];
                        Dictionary<string, Order> res = new Dictionary<string, Order>();
                        foreach (Order order in os.Values)
                            if (order.Status == OrderStatus.Executed && order.ExecutionDate == timestamp)
                                res.Add(order.ID, order);

                        if (res.Count > 0)
                            orders.Add(instrument.ID, res);
                    }
                }

                if (orders.Count != 0)
                    return orders;
            }

            return null;
        }

        /// <summary>
        /// Function: Returns a dictionary all BOOKED orders for linked to all instruments exisiting at a given time.
        /// The function returns a nested dictionary where the inner dictionary is a set of orders linked to a given instrument
        /// and the outer dictionary is a set of the inner dictionaries linked for each instrument.
        /// </summary>    
        /// <param name="timestamp">reference timestamp for orders retrieval
        /// </param>
        /// <param name="aggregated">true if orders are to retrieved from subportfolios
        /// </param>
        public Dictionary<int, Dictionary<string, Order>> BookedOrders(DateTime timestamp, Boolean aggregated)
        {
            Dictionary<int, Dictionary<string, Order>> orders = new Dictionary<int, Dictionary<string, Order>>();
            if (aggregated)
            {
                if (_orderMemory_aggregated_orderDate.Count == 0)
                    return null;

                foreach (int id in _orderMemory_aggregated_instrument_orderDate.Keys)
                {
                    Instrument instrument = Instrument.FindInstrument(id);
                    DateTime[] dates = _orderMemory_aggregated_instrument_orderDate[id].Keys.OrderBy(x => x).ToArray();

                    if (dates.Length > 0)
                    {
                        DateTime lastDate = timestamp;
                        for (int i = dates.Length - 1; i >= 0; i--)
                        {
                            if (dates[i] <= lastDate)
                            {
                                lastDate = dates[i];
                                break;
                            }
                        }

                        if (_orderMemory_aggregated_orderDate.ContainsKey(lastDate))
                        {
                            ConcurrentDictionary<string, Order> os = _orderMemory_aggregated_orderDate[lastDate][id];
                            Dictionary<string, Order> res = new Dictionary<string, Order>();
                            foreach (Order order in os.Values)
                                if (order.Status == OrderStatus.Booked && order.ExecutionDate == timestamp)
                                    res.Add(order.ID, order);

                            if (res.Count > 0)
                                orders.Add(instrument.ID, res);
                        }
                    }
                }

                if (orders.Count != 0)
                    return orders;
            }
            else
            {
                if (_orderMemory_orderDate.Count == 0)
                    return null;

                foreach (int id in _orderMemory_instrument_orderDate.Keys)
                {
                    Instrument instrument = Instrument.FindInstrument(id);
                    DateTime[] dates = _orderMemory_instrument_orderDate[id].Keys.OrderBy(x => x).ToArray();

                    if (dates.Length > 0)
                    {
                        DateTime lastDate = timestamp;
                        for (int i = dates.Length - 1; i >= 0; i--)
                        {
                            if (dates[i] <= lastDate)
                            {
                                lastDate = dates[i];
                                break;
                            }
                        }

                        ConcurrentDictionary<string, Order> os = _orderMemory_orderDate[lastDate][id];
                        Dictionary<string, Order> res = new Dictionary<string, Order>();
                        foreach (Order order in os.Values)
                            if (order.Status == OrderStatus.Booked && order.ExecutionDate == timestamp)
                                res.Add(order.ID, order);

                        if (res.Count > 0)
                            orders.Add(instrument.ID, res);
                    }
                }

                if (orders.Count != 0)
                    return orders;
            }

            return null;
        }

        /// <summary>
        /// Function: Returns a dictionary all NON-EXECUTED orders for linked to all instruments exisiting at a given time.
        /// The function returns a nested dictionary where the inner dictionary is a set of orders linked to a given instrument
        /// and the outer dictionary is a set of the inner dictionaries linked for each instrument.
        /// </summary>    
        /// <param name="timestamp">reference timestamp for orders retrieval
        /// </param>
        /// <param name="aggregated">true if orders are to retrieved from subportfolios
        /// </param>
        public Dictionary<int, Dictionary<string, Order>> NonExecutedOrders(DateTime timestamp, Boolean aggregated)
        {
            timestamp = timestamp.Date;
            Dictionary<int, Dictionary<string, Order>> orders = new Dictionary<int, Dictionary<string, Order>>();

            if (aggregated)
            {
                if (_orderMemory_aggregated_orderDate.Count == 0)
                    return null;

                foreach (int id in _orderMemory_aggregated_instrument_orderDate.Keys)
                {
                    Instrument instrument = Instrument.FindInstrument(id);
                    DateTime[] dates = _orderMemory_aggregated_instrument_orderDate[id].Keys.OrderBy(x => x).ToArray();

                    if (dates.Length > 0)
                    {
                        DateTime lastDate = timestamp;
                        for (int i = dates.Length - 1; i >= 0; i--)
                        {
                            if (dates[i] <= lastDate)
                            {
                                lastDate = dates[i];
                                break;
                            }
                        }

                        if (_orderMemory_aggregated_orderDate.ContainsKey(lastDate))
                        {
                            ConcurrentDictionary<string, Order> os = _orderMemory_aggregated_orderDate[lastDate][id];
                            Dictionary<string, Order> res = new Dictionary<string, Order>();
                            foreach (Order order in os.Values.ToList())
                                if ((order.Status == OrderStatus.NotExecuted && order.ExecutionDate == timestamp))// || order.Status == OrderStatus.New || order.Status == OrderStatus.Submitted))// && order.ExecutionDate == timestamp)
                                    res.Add(order.ID, order);

                            if (res.Count > 0)
                                orders.Add(instrument.ID, res);
                        }
                    }
                }

                if (orders.Count != 0)
                    return orders;
            }
            else
            {
                if (_orderMemory_orderDate.Count == 0)
                    return null;

                foreach (int id in _orderMemory_instrument_orderDate.Keys)
                {
                    Instrument instrument = Instrument.FindInstrument(id);
                    DateTime[] dates = _orderMemory_instrument_orderDate[id].Keys.OrderBy(x => x).ToArray();
                    if (dates.Length > 0)
                    {
                        DateTime lastDate = timestamp;
                        for (int i = dates.Length - 1; i >= 0; i--)
                        {
                            if (dates[i] <= lastDate)
                            {
                                lastDate = dates[i];
                                break;
                            }
                        }

                        ConcurrentDictionary<string, Order> os = _orderMemory_orderDate[lastDate][id];
                        Dictionary<string, Order> res = new Dictionary<string, Order>();
                        foreach (Order order in os.Values.ToList())
                            if ((order.Status == OrderStatus.NotExecuted && order.ExecutionDate == timestamp))// || order.Status == OrderStatus.New || order.Status == OrderStatus.Submitted) && order.ExecutionDate == timestamp)
                                res.Add(order.ID, order);

                        if (res.Count > 0)
                            orders.Add(instrument.ID, res);
                    }
                }

                if (orders.Count != 0)
                    return orders;
            }

            return null;
        }

        /// <summary>
        /// Function: Returns a dictionary all orders for linked to all instruments exisiting at a given time. If orders exist at the specific timestamp, the most previously exising orders are returned.
        /// The function returns a nested dictionary where the inner dictionary is a set of orders linked to a given instrument
        /// and the outer dictionary is a set of the inner dictionaries linked for each instrument.
        /// </summary>    
        /// <param name="timestamp">reference timestamp for orders retrieval
        /// </param>
        /// <param name="aggregated">true if orders are to retrieved from subportfolios
        /// </param>
        public Dictionary<int, Dictionary<string, Order>> LastOrders(DateTime timestamp, Boolean aggregated)
        {
            timestamp = timestamp.Date;
            Dictionary<int, Dictionary<string, Order>> orders = new Dictionary<int, Dictionary<string, Order>>();
            if (aggregated)
            {
                if (_orderMemory_aggregated_orderDate.Count == 0)
                    return null;

                foreach (int id in _orderMemory_aggregated_instrument_orderDate.Keys)
                {
                    Instrument instrument = Instrument.FindInstrument(id);
                    DateTime[] dates = _orderMemory_aggregated_instrument_orderDate[id].Keys.OrderBy(x => x).ToArray();
                    if (dates.Length > 0)
                    {
                        DateTime lastDate = timestamp;
                        for (int i = dates.Length - 1; i >= 0; i--)
                        {
                            if (dates[i] <= lastDate)
                            {
                                lastDate = dates[i];
                                break;
                            }
                        }

                        Dictionary<string, Order> os = _orderMemory_aggregated_orderDate[lastDate][id].ToDictionary(entry => entry.Key, entry => entry.Value); ;
                        orders.Add(instrument.ID, os);
                    }
                }

                if (orders.Count != 0)
                    return orders;
            }
            else
            {
                if (_orderMemory_orderDate.Count == 0)
                    return null;

                foreach (int id in _orderMemory_instrument_orderDate.Keys)
                {
                    Instrument instrument = Instrument.FindInstrument(id);
                    DateTime[] dates = _orderMemory_instrument_orderDate[id].Keys.OrderBy(x => x).ToArray();
                    if (dates.Length > 0)
                    {
                        DateTime lastDate = timestamp;
                        for (int i = dates.Length - 1; i >= 0; i--)
                        {
                            if (dates[i] <= lastDate)
                            {
                                lastDate = dates[i];
                                break;
                            }
                        }

                        Dictionary<string, Order> order = _orderMemory_orderDate[lastDate][id].ToDictionary(entry => entry.Key, entry => entry.Value); ;
                        orders.Add(instrument.ID, order);
                    }
                }

                if (orders.Count != 0)
                    return orders;
            }
            if (SimulationObject)
                return null;

            return null;
        }

        /// <summary>
        /// Function: Returns a dictionary all orders for linked to all instruments exisiting at a given time.
        /// The function returns a nested dictionary where the inner dictionary is a set of orders linked to a given instrument
        /// and the outer dictionary is a set of the inner dictionaries linked for each instrument.
        /// </summary>    
        /// <param name="timestamp">reference timestamp for orders retrieval
        /// </param>
        /// <param name="aggregated">true if orders are to retrieved from subportfolios
        /// </param>
        public Dictionary<int, Dictionary<string, Order>> Orders(DateTime timestamp, Boolean aggregated)
        {
            timestamp = timestamp.Date;

            if (aggregated)
            {
                if (_orderMemory_aggregated_orderDate.Count == 0)
                    return null;

                if (_orderMemory_aggregated_orderDate.ContainsKey(timestamp))
                    return _orderMemory_aggregated_orderDate[timestamp].ToDictionary(entry => entry.Key, entry => entry.Value.ToDictionary(e => e.Key, e => e.Value));
            }
            else
            {
                if (_orderMemory_orderDate.Count == 0)
                    return null;

                if (_orderMemory_orderDate.ContainsKey(timestamp))
                    return _orderMemory_orderDate[timestamp].ToDictionary(entry => entry.Key, entry => entry.Value.ToDictionary(e => e.Key, e => e.Value));
            }

            if (SimulationObject)
                return null;

            if (_loadedDates.ContainsKey(timestamp.Date))
                return null;

            return null;
        }

        /// <summary>
        /// Function: Returns a dictionary all virtual positions for linked to all instruments exisiting at a given time.
        /// Virtual positions represent an object with an aggregated unit of positions and orders.
        /// Example: The portfolio has 1 long position in asset A and 1 long order to be executed in the same asset, then the virtual position's unit is 2 for that asset.        
        /// </summary>    
        /// <param name="timestamp">reference timestamp for orders retrieval
        /// </param>
        public Dictionary<int, VirtualPosition> PositionOrders(DateTime timestamp)
        {
            Dictionary<int, Dictionary<string, Order>> orders = Orders(timestamp);
            List<Position> positions = Positions(timestamp);

            if (positions == null && orders == null)
                return null;

            Dictionary<int, VirtualPosition> res = new Dictionary<int, VirtualPosition>();

            if (positions != null)
                foreach (Position position in positions)
                    //if (!orders.ContainsKey(position.Instrument.ID))
                    res.Add(position.Instrument.ID, new VirtualPosition(position.Instrument.ID, timestamp, position.Unit));


            if (orders != null)
                foreach (Dictionary<string, Order> os in orders.Values)
                    foreach (Order order in os.Values)
                        if (order.Status == OrderStatus.New && order.Type == OrderType.Market)
                            if (!res.ContainsKey(order.InstrumentID))
                                res.Add(order.Instrument.ID, new VirtualPosition(order.Instrument.ID, order.OrderDate, order.Unit));
                            else
                                res[order.Instrument.ID].Unit += order.Unit;

            //if (positions == null)            
            //    return res;


            return res;
        }

        /// <summary>
        /// Function: Returns a dictionary all virtual positions for linked to all instruments exisiting at a given time. Where orders aggregated are only New Market orders.
        /// Virtual positions represent an object with an aggregated unit of positions and orders.
        /// Example: The portfolio has 1 long position in asset A and 1 long order to be executed in the same asset, then the virtual position's unit is 2 for that asset.        
        /// </summary>    
        /// <param name="timestamp">reference timestamp for orders retrieval
        /// </param>
        public Dictionary<int, VirtualPosition> PositionNewMarketOrders(DateTime timestamp)
        {
            Dictionary<int, Dictionary<string, Order>> orders = OpenOrders(timestamp, false);
            List<Position> positions = Positions(timestamp);

            if (positions == null && orders == null)
                return null;

            Dictionary<int, VirtualPosition> res = new Dictionary<int, VirtualPosition>();

            if (positions != null)
                foreach (Position position in positions)
                    res.Add(position.Instrument.ID, new VirtualPosition(position.Instrument.ID, timestamp, position.Unit));

            if (orders != null)
                foreach (Dictionary<string, Order> os in orders.Values)
                    foreach (Order order in os.Values)
                        //if (order.Status == OrderStatus.New && order.Type == OrderType.Market)
                        if (order.Type == OrderType.Market)
                            if (res.ContainsKey(order.InstrumentID))
                                res[order.InstrumentID].Unit += order.Unit;
                            else
                                res.Add(order.Instrument.ID, new VirtualPosition(order.Instrument.ID, order.OrderDate, order.Unit));

            return res;
        }

        /// <summary>
        /// Function: Returns a dictionary all virtual positions for linked to all instruments exisiting at a given time. Where orders aggregated are only OPEN orders.
        /// Virtual positions represent an object with an aggregated unit of positions and orders.
        /// Example: The portfolio has 1 long position in asset A and 1 long order to be executed in the same asset, then the virtual position's unit is 2 for that asset.        
        /// </summary>    
        /// <param name="timestamp">reference timestamp for orders retrieval
        /// </param>
        /// <param name="aggregated">true if orders are to retrieved from subportfolios
        /// </param>
        public Dictionary<int, VirtualPosition> PositionOpenOrders(DateTime timestamp, bool aggregated)
        {
            Dictionary<int, Dictionary<string, Order>> orders = OpenOrders(timestamp, aggregated);
            List<Position> positions = Positions(timestamp, aggregated);

            if (positions == null && orders == null)
                return null;

            Dictionary<int, VirtualPosition> res = new Dictionary<int, VirtualPosition>();

            foreach (Position position in positions)
                res.Add(position.Instrument.ID, new VirtualPosition(position.Instrument.ID, timestamp, position.Unit));


            if (orders != null)
                foreach (Dictionary<string, Order> os in orders.Values)
                    foreach (Order order in os.Values)
                        //if (order.Status == OrderStatus.New && order.Type == OrderType.Market)
                        if (res.ContainsKey(order.InstrumentID))
                            res[order.InstrumentID].Unit += order.Unit;
                        else
                            res.Add(order.Instrument.ID, new VirtualPosition(order.Instrument.ID, order.OrderDate, order.Unit));

            return res;
        }

        /// <summary>
        /// Function: Returns a dictionary all virtual positions for linked to all instruments exisiting at a given time including all subportfolios.
        /// Virtual positions represent an object with an aggregated unit of positions and orders.
        /// Example: The portfolio has 1 long position in asset A and 1 long order to be executed in the same asset, then the virtual position's unit is 2 for that asset.        
        /// </summary>    
        /// <param name="timestamp">reference timestamp for orders retrieval
        /// </param>
        public Dictionary<int, VirtualPosition> AggregatedPositionOrders(DateTime timestamp)
        {
            Dictionary<int, Dictionary<string, Order>> orders = OpenOrders(timestamp, true);
            List<Position> positions = Positions(timestamp, true);

            Dictionary<int, VirtualPosition> res = new Dictionary<int, VirtualPosition>();

            if (positions == null && orders == null)
                return res;


            if (positions != null)
                foreach (Position position in positions)
                    res.Add(position.Instrument.ID, new VirtualPosition(position.Instrument.ID, timestamp, position.Unit));

            if (orders != null)
                foreach (Dictionary<string, Order> os in orders.Values.ToList())
                    foreach (Order order in os.Values.ToList())
                        if (order.Type == OrderType.Market)
                            if (res.ContainsKey(order.InstrumentID))
                                res[order.Instrument.ID].Unit += order.Unit;
                            else
                                res.Add(order.Instrument.ID, new VirtualPosition(order.Instrument.ID, order.OrderDate, order.Unit));

            return res;
        }

        /// <summary>
        /// Function: Returns the latest timestamp for which there a position was changed prior to and including the reference date.
        /// </summary>    
        /// <param name="date">reference date.
        /// </param>
        public DateTime GetLastTimestamp(DateTime date)
        {
            if ((LastTimestamp == DateTime.MinValue && _positionHistoryMemory_date.Keys.Count != 0))
            {
                LastTimestamp = (from d in _positionHistoryMemory_date.Keys orderby d descending select d).First();
                return LastTimestamp;
            }

            if (LastTimestamp != DateTime.MinValue && date >= LastTimestamp)
                return LastTimestamp;

            if (_positionHistoryMemory_date.Keys.Count != 0)
            {
                for (int i = _orderedDates.Count - 1; i >= 0; i--)
                    if (date >= _orderedDates[i])
                        return _orderedDates[i];
            }

            if (_loadedDates.ContainsKey(date.Date))
                return DateTime.MinValue;

            DateTime t = Factory.LastPositionTimestamp(this, date);
            if (t > LastTimestamp)
                LastTimestamp = t;
            return t;
        }

        Dictionary<string, string> _realizedCarry = new Dictionary<string, string>();

        /// <summary>
        /// Function: Returns the latest timestamp for which there a position was changed prior to and including the reference date.
        /// </summary>    
        /// <param name="date">reference date.
        /// </param>
        public void RealizeCarryAggregatedPositions(Instrument instrument, DateTime timestamp)
        {
            string key = instrument.ID + " " + timestamp;
            if (_realizedCarry.ContainsKey(key))
                return;
            _realizedCarry.Add(key, key);

            DateTime latestdate = GetLastTimestamp(timestamp);
            List<Position> posList = Positions(latestdate);

            if (posList == null || posList.Count == 0)
                return;

            List<VirtualPosition> allPositions = new List<VirtualPosition>();

            Position[] pos = posList.ToArray();
            for (int i = 0; i < pos.Count(); i++)
            {
                Position p = pos[i];
                if (p.Unit != 0.0)
                {
                    if (p.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio)
                        Portfolio.FindPortfolio(p.Instrument).RealizeCarryAggregatedPositions(instrument, timestamp);
                    else if (p.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy)
                    {
                        Strategy strategy = Strategy.FindStrategy(p.Instrument);
                        if (strategy.Portfolio != null)
                            strategy.Portfolio.RealizeCarryAggregatedPositions(instrument, timestamp);
                        else if (p.Instrument == instrument)
                            p.RealizeCarryCost(timestamp);

                    }
                    else if (p.Instrument == instrument)
                        p.RealizeCarryCost(timestamp);
                }
            }
        }

        /// <summary>
        /// Property: Returns the latest timestamp for which there a position was changed.
        /// </summary>    
        [Newtonsoft.Json.JsonIgnore]
        public DateTime LastTimestamp
        {
            get
            {
                if (_lastTimestamp != DateTime.MinValue)
                    return _lastTimestamp;

                return _lastTimestamp;
            }
            set
            {
                this._lastTimestamp = value;
            }
        }

        /// <summary>
        /// Property: Returns the latest timestamp for which there an order was changed.
        /// </summary>    
        [Newtonsoft.Json.JsonIgnore]
        public DateTime LastOrderTimestampDB
        {
            get
            {
                DateTime ret = Factory.LastOrderTimestamp(this, DateTime.MinValue);
                this.LoadPositionOrdersMemory(ret, false);
                return ret;
            }
            private set
            {
                this._lastTimestamp = value;
            }
        }

        /// <summary>
        /// Property: Returns the first timestamp for which there a position was changed.
        /// </summary>    
        public DateTime FirstTimestamp
        {
            get
            {
                return _firstTimestamp;
            }
            private set
            {
                this._firstTimestamp = value;
            }
        }

        /// <summary>
        /// Function: Update the status or properties of an order within this portfolio.
        /// </summary>    
        /// <param name="order">reference order to update.
        /// </param>
        /// <param name="onlyMemory">False if the change should affect persistent storage.
        /// </param>
        public void UpdateOrder(Order order, Boolean onlyMemory)
        {
            lock (objLock)
            {
                string key = order.ID;

                if (order.Aggregated)
                {
                    if (_orderMemory_aggregated.ContainsKey(key))
                        _orderMemory_aggregated[key] = order;

                    if (_orderMemory_aggregated_instrument_orderDate.ContainsKey(order.InstrumentID))
                        if (_orderMemory_aggregated_instrument_orderDate[order.InstrumentID].ContainsKey(order.OrderDate))
                            if (_orderMemory_aggregated_instrument_orderDate[order.InstrumentID][order.OrderDate].ContainsKey(order.ID))
                                _orderMemory_aggregated_instrument_orderDate[order.InstrumentID][order.OrderDate][order.ID] = order;

                    if (_orderMemory_aggregated_orderDate.ContainsKey(order.OrderDate))
                        if (_orderMemory_aggregated_orderDate[order.OrderDate].ContainsKey(order.Instrument.ID))
                            if (_orderMemory_aggregated_orderDate[order.OrderDate][order.InstrumentID].ContainsKey(order.ID))
                                _orderMemory_aggregated_orderDate[order.OrderDate][order.InstrumentID][order.ID] = order;

                    if (_orderNewMemory_aggregated.ContainsKey(key))
                        _orderNewMemory_aggregated[key] = order;
                    else if (!onlyMemory)
                        Factory.UpdateOrder(order);

                }
                else
                {
                    if (_orderMemory.ContainsKey(key))
                        _orderMemory[key] = order;

                    if (_orderMemory_instrument_orderDate.ContainsKey(order.Instrument.ID))
                        if (_orderMemory_instrument_orderDate[order.InstrumentID].ContainsKey(order.OrderDate))
                            if (_orderMemory_instrument_orderDate[order.InstrumentID][order.OrderDate].ContainsKey(order.ID))
                                _orderMemory_instrument_orderDate[order.InstrumentID][order.OrderDate][order.ID] = order;

                    if (_orderMemory_orderDate.ContainsKey(order.OrderDate))
                        if (_orderMemory_orderDate[order.OrderDate].ContainsKey(order.InstrumentID))
                            if (_orderMemory_orderDate[order.OrderDate][order.InstrumentID].ContainsKey(order.ID))
                                _orderMemory_orderDate[order.OrderDate][order.InstrumentID][order.ID] = order;

                    if (_orderNewMemory.ContainsKey(key))
                        _orderNewMemory[key] = order;
                    else if (!onlyMemory)
                        Factory.UpdateOrder(order);
                }
            }
        }

        /// <summary>
        /// Function: Save new positions and orders to persistent memory.
        /// </summary>    
        public void SaveNewPositions()
        {
            lock (objLock)
            {
                if (SimulationObject)
                    return;

                DateTime[] dts = _positionNewHistoryMemory.Keys.ToArray();
                foreach (DateTime dt in dts)
                {

                    int[] ids = _positionNewHistoryMemory[dt].Keys.ToArray();
                    foreach (int id in ids)
                    {
                        Position v = null;
                        Position p = _positionNewHistoryMemory[dt][id];
                        Factory.AddNewPosition(p);
                        _positionNewHistoryMemory[dt].TryRemove(id, out v);
                    }

                    ConcurrentDictionary<int, Position> v2 = null;
                    _positionNewHistoryMemory.TryRemove(dt, out v2);
                }

                string[] oids = _orderNewMemory.Keys.ToArray();
                foreach (string oid in oids)
                {
                    Order v = null;
                    Order o = _orderNewMemory[oid];
                    Factory.AddNewOrder(o);
                    _orderNewMemory.TryRemove(oid, out v);
                }

                Factory.SaveNewPositions(this);
            }

        }

        /// <summary>
        /// Function: Update the status or properties of an order accross the tree of all relevant portfolios and sub-portfolios.
        /// </summary>    
        /// <param name="o">reference order to update accross the tree.
        /// </param>
        /// <param name="type">order's status.
        /// </param>
        /// <param name="unit">order's units.
        /// </param>
        /// <param name="executionLevel">order's execution level.
        /// </param>
        /// <param name="executionDate">order's execution date.
        /// </param>
        /// <remarks>When the status is booked the order is updated going up the tree from this to parent and so on.
        /// Otherwise, the order is updatedgoing down the tree.
        /// </remarks>
        public void UpdateOrderTree(Order o, OrderStatus type, double unit, double executionLevel, DateTime executionDate)
        {
            UpdateOrderTree(o, type, unit, executionLevel, executionDate, null, null, null);
        }

        /// <summary>
        /// Function: Update the status or properties of an order accross the tree of all relevant portfolios and sub-portfolios.
        /// </summary>    
        /// <param name="o">reference order to update accross the tree.
        /// </param>
        /// <param name="type">order's status.
        /// </param>
        /// <param name="unit">order's units.
        /// </param>
        /// <param name="executionLevel">order's execution level.
        /// </param>
        /// <param name="executionDate">order's execution date.
        /// </param>
        /// <param name="client">client connection  to execution platform linked to this order
        /// </param>
        /// <param name="destination">destination within the client connection.
        /// </param>
        /// <param name="account">account the order is sent to through the destination and the client connection.
        /// </param>
        /// <remarks>When the status is booked the order is updated going up the tree from this to parent and so on.
        /// Otherwise, the order is updated going down the tree.
        /// </remarks>
        public void UpdateOrderTree(Order o, OrderStatus type, double unit, double executionLevel, DateTime executionDate, string client, string destination, string account)
        {
            lock (objLock)
            {
                if (type == OrderStatus.Booked)
                {
                    if (ParentPortfolio != null)
                        ParentPortfolio.UpdateOrderTree(o, type, unit, executionLevel, executionDate, client, destination, account);
                }
                else
                {
                    Dictionary<int, Dictionary<string, Order>> orders = Orders(o.OrderDate);

                    if (orders != null && orders.Count != 0)
                    {
                        foreach (Dictionary<string, Order> os in orders.Values.ToList())
                            foreach (Order order in os.Values.ToList())
                                if (order.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio || (order.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && ((Strategy)order.Instrument).Portfolio != null))
                                {
                                    Portfolio portfolio = order.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio ? (Portfolio)order.Instrument : ((Strategy)order.Instrument).Portfolio;
                                    portfolio.UpdateOrderTree(o, type, unit, executionLevel, executionDate, client, destination, account);
                                }
                    }

                    List<Position> positions = Positions(o.OrderDate);
                    if (positions != null && positions.Count != 0)
                    {
                        foreach (Position position in positions.ToList())
                            if (position.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio || (position.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && ((Strategy)position.Instrument).Portfolio != null))
                            {
                                Portfolio portfolio = position.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio ? (Portfolio)position.Instrument : ((Strategy)position.Instrument).Portfolio;
                                portfolio.UpdateOrderTree(o, type, unit, executionLevel, executionDate, client, destination, account);
                            }
                    }
                }

                Order target = FindOrder(o.ID, false);
                if (target != null)
                {
                    if (!double.IsNaN(unit))
                        target.Unit = unit;
                    if (type == OrderStatus.Executed)
                    {
                        target.ExecutionLevel = executionLevel;
                        target.ExecutionDate = executionDate;
                    }
                    else if (type == OrderStatus.NotExecuted)
                        target.ExecutionDate = executionDate;

                    if (client != null)
                        target.Client = client;

                    if (destination != null)
                        target.Destination = destination;

                    if (account != null)
                        target.Account = account;

                    target.Status = type;
                }


                Order target_aggregated = FindOrder(o.ID, true);
                if (target_aggregated != null)
                {
                    if (!double.IsNaN(unit))
                        target_aggregated.Unit = unit;

                    if (type == OrderStatus.Executed)
                    {
                        target_aggregated.ExecutionLevel = executionLevel;
                        target_aggregated.ExecutionDate = executionDate;
                    }
                    else if (type == OrderStatus.NotExecuted)
                        target_aggregated.ExecutionDate = executionDate;

                    if (client != null)
                        target_aggregated.Client = client;

                    if (destination != null)
                        target_aggregated.Destination = destination;

                    if (account != null)
                        target_aggregated.Account = account;

                    target_aggregated.Status = type;
                }
            }
        }

        /// <summary>
        /// Function: Update units of an order with a differential accross the tree of all relevant portfolios and parent-portfolios.
        /// </summary>    
        /// <param name="refOrder">reference order to update accross the tree.
        /// </param>
        /// <param name="type">order's status.
        /// </param>
        /// <remarks>The order is updated going up the tree from this to parent and so on.
        /// </remarks>
        public void UpdateOrderTree(Order refOrder, double order_diff)
        {
            lock (objLock)
            {
                if (ParentPortfolio != null)
                    ParentPortfolio.UpdateOrderTree(refOrder, order_diff);

                Order order = FindOrder(refOrder.ID, true);

                if (order == null)
                    CreateOrder(refOrder);
                else
                    order.Unit += order_diff;
            }
        }

        /// <summary>
        /// Delegate: Skeleton for a delegate function to customely transform and filter unit sizes.
        /// </summary>       
        /// <param name="instrument">Instrument valued base instrument.
        /// </param>
        /// <param name="unit">string valued name of the Strategy class implemention.
        /// </param>
        public delegate double OrderUnitCalculationEvent(Instrument instrument, double unit);
        public OrderUnitCalculationEvent OrderUnitCalculation = null;

        /// <summary>
        /// Function: Calculate the relevant unit after it passes through a filter.
        /// Example: a standard filter is to transform an order size from 1.9 to 2 since fractional contracts cannot be traded.
        /// </summary>    
        /// <param name="instrument">instrument the order is linked to
        /// </param>
        /// <param name="unit">units to pass through the filter.
        /// </param>
        /// <remarks>Filters can be added through OrderUnitCalculation Event handler.
        /// </remarks>
        public double OrderUnitCalculationFilter(Instrument instrument, double unit)
        {
            if (OrderUnitCalculation != null)
                return OrderUnitCalculation(instrument, unit);
            else if (MasterPortfolio.OrderUnitCalculation != null)
                return MasterPortfolio.OrderUnitCalculation(instrument, unit);
            return unit;
        }

        /// <summary>
        /// Function: Create an order but not transmit
        /// </summary>    
        /// <param name="instrument">instrument the order is linked to
        /// </param>
        /// <param name="orderDate">date at which the order is created. DateTime.Now if live trading.
        /// </param>
        /// <param name="unit">units of contracts
        /// </param>
        /// <param name="type">Type of order
        /// </param>
        /// <param name="limit">Value of limit if it is a limit order.
        /// </param>
        public Order CreateOrder(Instrument instrument, DateTime orderDate, double unit, OrderType type, double limit)
        {
            lock (objLock)
            {
                if (double.IsNaN(unit) || double.IsInfinity(unit))
                    throw new Exception("Unit is NaN");

                unit = OrderUnitCalculationFilter(instrument, unit);

              
                //if (instrument.InstrumentType == Kernel.InstrumentType.Future)
                //    Console.WriteLine("Create Order: " + this + " " + instrument + " " + orderDate + " " + unit);

                if (!_quickAddedInstruments.ContainsKey(instrument) && !IsReserve(instrument) && unit != 0.0)
                {
                    if (Strategy != null)
                        Strategy.AddInstrument(instrument, orderDate);

                    _quickAddedInstruments.Add(instrument, "");
                }

                if (instrument.InstrumentType == InstrumentType.Portfolio)
                {
                    Portfolio portfolio = Portfolio.FindPortfolio(instrument);
                    portfolio.UpdateNotionalOrder(orderDate, unit, TimeSeriesType.Last);
                    unit = (unit == 0.0 ? 0.0 : 1.0);
                }

                if (instrument.InstrumentType == InstrumentType.Strategy)
                {
                    Strategy strategy = Strategy.FindStrategy(instrument);
                    BusinessDay orderDate_local = strategy.Calendar.GetBusinessDay(orderDate);
                    if (strategy.Portfolio != null)
                    {
                        strategy.UpdateAUMOrder(orderDate, unit);
                        unit = (unit == 0.0 ? 0.0 : 1.0);
                    }
                }

                Order o = null;
                if (type == OrderType.Market)
                {
                    Dictionary<int, Dictionary<string, Order>> orders = this.Orders(orderDate, false);
                    if (orders != null && orders.ContainsKey(instrument.ID))
                        foreach (Order oo in orders[instrument.ID].Values)
                            if (oo.Type == OrderType.Market && oo.Status == OrderStatus.New)
                                o = oo;
                }

                double oldUnit = o == null ? 0.0 : ((instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio || (instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && Strategy.FindStrategy(instrument).Portfolio != null)) ? 0.0 : o.Unit);
                double newUnit = unit;
                Order order = o == null ? new Order(this.ID, instrument.ID, newUnit, orderDate, DateTime.MaxValue, type, limit, OrderStatus.New, 0.0) : new Order(o.ID, this.ID, instrument.ID, newUnit, orderDate, DateTime.MaxValue, type, limit, OrderStatus.New, 0.0, false);
                UpdateOrderTree(order, newUnit - oldUnit);

                AddOrderMemory(order);
                return order;
            }
        }

        /// <summary>
        /// Function: Add order into internal memory. Not persistent storage.
        /// </summary>    
        /// <param name="order">reference order.
        /// </param>
        public void AddOrderMemory(Order order)
        {
            lock (objLock)
            {
                DateTime orderDate = order.OrderDate.Date;
                Instrument instrument = order.Instrument;

                if (!order.Aggregated)
                {
                    if (!_orderMemory.ContainsKey(order.ID))
                    {
                        _orderMemory.TryAdd(order.ID, order);

                        if (!_orderNewMemory.ContainsKey(order.ID))
                            _orderNewMemory.TryAdd(order.ID, order);
                    }
                    else
                        _orderMemory[order.ID] = order;

                    if (_orderMemory_orderDate.ContainsKey(orderDate))
                        if (_orderMemory_orderDate[orderDate].ContainsKey(instrument.ID))
                            if (_orderMemory_orderDate[orderDate][instrument.ID].ContainsKey(order.ID))
                                _orderMemory_orderDate[orderDate][instrument.ID][order.ID] = order;
                            else
                                _orderMemory_orderDate[orderDate][instrument.ID].TryAdd(order.ID, order);
                        else
                        {
                            _orderMemory_orderDate[orderDate].TryAdd(instrument.ID, new ConcurrentDictionary<string, Order>());
                            _orderMemory_orderDate[orderDate][instrument.ID].TryAdd(order.ID, order);
                        }
                    else
                    {
                        _orderMemory_orderDate.TryAdd(orderDate, new ConcurrentDictionary<int, ConcurrentDictionary<string, Order>>());
                        _orderMemory_orderDate[orderDate].TryAdd(instrument.ID, new ConcurrentDictionary<string, Order>());
                        _orderMemory_orderDate[orderDate][instrument.ID].TryAdd(order.ID, order);
                    }


                    if (_orderMemory_instrument_orderDate.ContainsKey(instrument.ID))
                        if (_orderMemory_instrument_orderDate[instrument.ID].ContainsKey(orderDate))
                            if (_orderMemory_instrument_orderDate[instrument.ID][orderDate].ContainsKey(order.ID))
                                _orderMemory_instrument_orderDate[instrument.ID][orderDate][order.ID] = order;
                            else
                                _orderMemory_instrument_orderDate[instrument.ID][orderDate].TryAdd(order.ID, order);

                        else
                        {
                            _orderMemory_instrument_orderDate[instrument.ID].TryAdd(orderDate, new ConcurrentDictionary<string, Order>());
                            _orderMemory_instrument_orderDate[instrument.ID][orderDate].TryAdd(order.ID, order);
                        }
                    else
                    {
                        _orderMemory_instrument_orderDate.TryAdd(instrument.ID, new ConcurrentDictionary<DateTime, ConcurrentDictionary<string, Order>>());
                        _orderMemory_instrument_orderDate[instrument.ID].TryAdd(orderDate, new ConcurrentDictionary<string, Order>());
                        _orderMemory_instrument_orderDate[instrument.ID][orderDate].TryAdd(order.ID, order);
                    }

                    if (_orderNewMemory.ContainsKey(order.ID))
                        _orderNewMemory[order.ID] = order;
                }
                else
                {
                    if (!_orderMemory_aggregated.ContainsKey(order.ID))
                    {
                        _orderMemory_aggregated.TryAdd(order.ID, order);

                        if (!_orderNewMemory_aggregated.ContainsKey(order.ID))
                            _orderNewMemory_aggregated.TryAdd(order.ID, order);
                    }
                    else
                        _orderMemory_aggregated[order.ID] = order;

                    if (_orderMemory_aggregated_orderDate.ContainsKey(orderDate))
                        if (_orderMemory_aggregated_orderDate[orderDate].ContainsKey(order.Instrument.ID))
                            if (_orderMemory_aggregated_orderDate[orderDate].ContainsKey(order.Instrument.ID))
                                _orderMemory_aggregated_orderDate[orderDate][order.Instrument.ID][order.ID] = order;
                            else
                                _orderMemory_aggregated_orderDate[orderDate][order.Instrument.ID].TryAdd(order.ID, order);
                        else
                        {
                            _orderMemory_aggregated_orderDate[orderDate].TryAdd(order.Instrument.ID, new ConcurrentDictionary<string, Order>());
                            _orderMemory_aggregated_orderDate[orderDate][order.Instrument.ID].TryAdd(order.ID, order);
                        }
                    else
                    {
                        _orderMemory_aggregated_orderDate.TryAdd(orderDate, new ConcurrentDictionary<int, ConcurrentDictionary<string, Order>>());
                        _orderMemory_aggregated_orderDate[orderDate].TryAdd(order.Instrument.ID, new ConcurrentDictionary<string, Order>());
                        _orderMemory_aggregated_orderDate[orderDate][order.Instrument.ID].TryAdd(order.ID, order);
                    }

                    if (_orderMemory_aggregated_instrument_orderDate.ContainsKey(order.Instrument.ID))
                        if (_orderMemory_aggregated_instrument_orderDate[order.Instrument.ID].ContainsKey(orderDate))
                        {
                            if (!_orderMemory_aggregated_instrument_orderDate[order.Instrument.ID][orderDate].ContainsKey(order.ID))
                                _orderMemory_aggregated_instrument_orderDate[order.Instrument.ID][orderDate].TryAdd(order.ID, order);
                        }

                        else
                        {
                            _orderMemory_aggregated_instrument_orderDate[order.Instrument.ID].TryAdd(orderDate, new ConcurrentDictionary<string, Order>());
                            _orderMemory_aggregated_instrument_orderDate[order.Instrument.ID][orderDate].TryAdd(order.ID, order);
                        }
                    else
                    {
                        _orderMemory_aggregated_instrument_orderDate.TryAdd(order.Instrument.ID, new ConcurrentDictionary<DateTime, ConcurrentDictionary<string, Order>>());
                        _orderMemory_aggregated_instrument_orderDate[order.Instrument.ID].TryAdd(orderDate, new ConcurrentDictionary<string, Order>());
                        _orderMemory_aggregated_instrument_orderDate[order.Instrument.ID][orderDate].TryAdd(order.ID, order);
                    }

                    if (_orderNewMemory_aggregated.ContainsKey(order.ID))
                        _orderNewMemory_aggregated[order.ID] = order;
                }
            }
        }

        /// <summary>
        /// Function: Create a market order such that the entire target position is a given number of units.
        /// </summary>    
        /// <param name="instrument">instrument the order is linked to
        /// </param>
        /// <param name="orderDate">date at which the order is created. DateTime.Now if live trading.
        /// </param>
        /// <param name="unit">target units of contracts in final position
        /// </param>
        public Order CreateTargetMarketOrder(Instrument instrument, DateTime orderDate, double unit)
        {
            OrderType type = OrderType.Market;
            double limit = 0;

            unit = OrderUnitCalculationFilter(instrument, unit);

            Position oldPosition = FindPosition(instrument, orderDate);

            double oldUnit = (oldPosition == null ? 0.0 : ((instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio || (instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && Strategy.FindStrategy(instrument).Portfolio != null)) ? 0.0 : oldPosition.Unit));

            Dictionary<string, Order> oldOrder = FindOpenOrder(instrument, orderDate, false);
            if (oldOrder != null)
                foreach (Order order in oldOrder.Values.ToList())
                    if (order.Type == OrderType.Market && order.OrderDate != orderDate)
                        oldUnit += ((instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio || (instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && Strategy.FindStrategy(instrument).Portfolio != null)) ? 0.0 : order.Unit);

            double newUnit = unit - oldUnit;

            return CreateOrder(instrument, orderDate, newUnit, type, limit);
        }

        /// <summary>
        /// Function: Create an aggregegated order for internal use.
        /// </summary>    
        /// <param name="baseOrder">refence order to create the aggregate order from.
        /// </param>
        internal void CreateOrder(Order baseOrder)
        {
            Order order = new Order(baseOrder, this);

            DateTime orderDate = order.OrderDate.Date;

            if (!(order.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio || (order.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && Strategy.FindStrategy(order.Instrument).Portfolio != null)))// && !IsReserve(instrument))
                AddOrderMemory(order);
        }

        /// <summary>
        /// Function: Book orders that have been executed at or prior to a given timestamp.
        /// </summary>    
        /// <param name="timestamp">reference timestamp for bookings.
        /// </param>
        public List<Position> BookOrders(DateTime timestamp)
        {
            Dictionary<int, Dictionary<string, Order>> orders = OpenOrders(timestamp, false);
            List<Position> positions = new List<Position>();

            if (this == MasterPortfolio && Strategy != null)
            {
                double aum_change = Strategy.GetOrderAUMChange(timestamp, TimeSeriesType.Last);
                if (!double.IsNaN(aum_change) && !double.IsInfinity(aum_change) && aum_change != 0)
                {
                    double current_aum = Strategy.GetAUM(timestamp, TimeSeriesType.Last);
                    double target_aum = current_aum + aum_change;

                    Strategy.AddMemoryPoint(timestamp, target_aum, Strategy._aum_id_do_not_use, (int)TimeSeriesType.Last);

                    Strategy.AddMemoryPoint(timestamp, aum_change, Strategy._aum_chg_id_do_not_use, (int)TimeSeriesType.Last);
                    Strategy.AddMemoryPoint(timestamp, 0, Strategy._aum_ord_chg_id_do_not_use, (int)TimeSeriesType.Last);

                    Strategy.Portfolio.UpdateReservePosition(timestamp, aum_change, Strategy.Portfolio.Currency);

                    positions.Add(new Position(ID, StrategyID, 1, timestamp, 1, timestamp, 1, timestamp, false));
                }
            }

            if (orders != null && orders.Count != 0)
            {
                foreach (Dictionary<string, Order> os in orders.Values.ToList())
                {
                    foreach (Order order in os.Values.ToList())
                    {
                        if (order.Unit == 0 && !(order.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && ((Strategy)order.Instrument).Portfolio != null))
                            UpdateOrderTree(order, OrderStatus.Booked, double.NaN, double.NaN, timestamp);

                        else if (order.Status == OrderStatus.Executed || (order.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && ((Strategy)order.Instrument).Portfolio != null && order.Status != OrderStatus.Booked && ((Strategy)order.Instrument).Calendar.GetBusinessDay(timestamp) != null))
                        {
                            Position p = this.FindPosition(order.Instrument, timestamp);

                            if (order.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy)
                            {
                                Strategy strategy = Strategy.FindStrategy(order.Instrument);

                                if (strategy.Portfolio != null)
                                {
                                    double current_aum = strategy.GetAUM(timestamp, TimeSeriesType.Last);
                                    double aum_change = strategy.GetOrderAUMChange(timestamp, TimeSeriesType.Last);

                                    if (double.IsNaN(aum_change) || double.IsInfinity(aum_change))
                                        aum_change = 0;

                                    UpdatePositions(timestamp);

                                    if (aum_change != 0)
                                    {
                                        Dictionary<int, Dictionary<string, Order>> nonExecuted = strategy.Portfolio.NonExecutedOrders(timestamp, true);

                                        if (nonExecuted == null || (nonExecuted != null && nonExecuted.Count == 0))
                                        {
                                            double target_aum = current_aum + aum_change;

                                            strategy.AddMemoryPoint(timestamp, target_aum, Strategy._aum_id_do_not_use, (int)TimeSeriesType.Last);

                                            strategy.AddMemoryPoint(timestamp, 0, Strategy._aum_ord_chg_id_do_not_use, (int)TimeSeriesType.Last);
                                            strategy.AddMemoryPoint(timestamp, aum_change, Strategy._aum_chg_id_do_not_use, (int)TimeSeriesType.Last);

                                            positions.Add(CreatePosition(order.Instrument, timestamp, order.Unit, current_aum, true, false, true, false, false, false, false));
                                            UpdateOrderTree(order, OrderStatus.Booked, double.NaN, double.NaN, timestamp);

                                            double updateValue = aum_change;

                                            if (strategy.Portfolio.ParentPortfolio != null)
                                            {
                                                strategy.Portfolio.UpdateReservePosition(timestamp, updateValue, strategy.Portfolio.Currency);
                                                strategy.Portfolio.ParentPortfolio.UpdateReservePosition(timestamp, -updateValue, strategy.Portfolio.Currency);
                                            }
                                        }
                                        else
                                            UpdateOrderTree(order, OrderStatus.NotExecuted, double.NaN, double.NaN, timestamp);
                                    }
                                    else
                                        UpdateOrderTree(order, OrderStatus.Booked, double.NaN, double.NaN, timestamp);
                                }
                                else if (!IsReserve(order.Instrument))
                                {
                                    if (p == null)
                                        positions.Add(CreatePosition(order.Instrument, timestamp, order.Unit, order.ExecutionLevel));
                                    else
                                        positions.Add(p.UpdatePosition(timestamp, order.Unit, order.ExecutionLevel, RebalancingType.Reserve, UpdateType.UpdateUnits));

                                    UpdateOrderTree(order, OrderStatus.Booked, double.NaN, double.NaN, DateTime.MaxValue);
                                }
                                else
                                    SystemLog.Write("ORDER NOT BOOKED " + order + " " + this);
                            }
                            else if (!IsReserve(order.Instrument))
                            {
                                if (!double.IsNaN(order.ExecutionLevel))
                                {
                                    if (p == null)
                                        positions.Add(CreatePosition(order.Instrument, timestamp, order.Unit, order.ExecutionLevel));
                                    else
                                        positions.Add(p.UpdatePosition(timestamp, order.Unit, order.ExecutionLevel, RebalancingType.Reserve, UpdateType.UpdateUnits));

                                    UpdateOrderTree(order, OrderStatus.Booked, double.NaN, double.NaN, DateTime.MaxValue);
                                }
                                else
                                    SystemLog.Write("ORDER NOT BOOKED " + order + " " + this);
                            }
                            else
                                SystemLog.Write("ORDER NOT BOOKED " + order + " " + this);
                        }
                    }
                }
            }

            return positions.Count == 0 ? null : positions;
        }

        /// <summary>
        /// Function: Re-book orders that have been executed at a given timestamp.
        /// </summary>    
        /// <param name="timestamp">reference timestamp for bookings.
        /// </param>
        public List<Position> ReBookOrders(DateTime timestamp)
        {
            Dictionary<int, Dictionary<string, Order>> orders = Orders(timestamp, false);
            List<Position> positions = new List<Position>();

            if (this == MasterPortfolio && Strategy != null)
            {
                double aum_change = Strategy.GetAUMChange(timestamp, TimeSeriesType.Last);
                if (!double.IsNaN(aum_change) && !double.IsInfinity(aum_change))
                {
                    double current_aum = Strategy.GetAUM(timestamp, TimeSeriesType.Last);
                    double target_aum = current_aum + aum_change;

                    Strategy.AddMemoryPoint(timestamp, target_aum, Strategy._aum_id_do_not_use, (int)TimeSeriesType.Last);
                    Strategy.AddMemoryPoint(timestamp, aum_change, Strategy._aum_chg_id_do_not_use, (int)TimeSeriesType.Last);
                    Strategy.AddMemoryPoint(timestamp, 0, Strategy._aum_ord_chg_id_do_not_use, (int)TimeSeriesType.Last);

                    if (aum_change != 0)
                        Strategy.Portfolio.UpdateReservePosition(timestamp, aum_change, Strategy.Portfolio.Currency);
                }
            }

            if (orders != null && orders.Count != 0)
            {
                foreach (Dictionary<string, Order> os in orders.Values)
                    foreach (Order order in os.Values)
                    {
                        if ((order.Status == OrderStatus.Booked && order.ExecutionDate == timestamp) || (order.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && ((Strategy)order.Instrument).Portfolio != null && order.Status != OrderStatus.Booked && ((Strategy)order.Instrument).Calendar.GetBusinessDay(timestamp) != null))
                        {
                            Position p = this.FindPosition(order.Instrument, timestamp);

                            if (order.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio)
                            {
                                Portfolio portfolio = Portfolio.FindPortfolio(order.Instrument);

                                double portolio_val = portfolio[timestamp];
                                portfolio.BookOrders(timestamp);

                                UpdatePositions(timestamp);

                                positions.Add(CreatePosition(order.Instrument, timestamp, order.Unit, portolio_val, true, false, true, false, false, false, false));
                                Console.WriteLine("Rebook: " + order);
                            }
                            else if (order.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy)
                            {
                                Strategy strategy = Strategy.FindStrategy(order.Instrument);

                                if (strategy.Portfolio != null)
                                {
                                    double current_aum = strategy.GetAUM(timestamp, TimeSeriesType.Last);
                                    double aum_change = strategy.GetOrderAUMChange(timestamp, TimeSeriesType.Last);

                                    if (double.IsNaN(aum_change) || double.IsInfinity(aum_change))
                                        aum_change = 0;

                                    UpdatePositions(timestamp);
                                    if (aum_change != 0)
                                    {
                                        Dictionary<int, Dictionary<string, Order>> nonExecuted = strategy.Portfolio.NonExecutedOrders(timestamp, true);

                                        if (nonExecuted == null || (nonExecuted != null && nonExecuted.Count == 0))
                                        {
                                            double target_aum = current_aum + aum_change;


                                            strategy.AddMemoryPoint(timestamp, target_aum, Strategy._aum_id_do_not_use, (int)TimeSeriesType.Last);
                                            strategy.AddMemoryPoint(timestamp, 0, Strategy._aum_ord_chg_id_do_not_use, (int)TimeSeriesType.Last);
                                            strategy.AddMemoryPoint(timestamp, aum_change, Strategy._aum_chg_id_do_not_use, (int)TimeSeriesType.Last);

                                            double updateValue = aum_change;



                                            positions.Add(CreatePosition(order.Instrument, timestamp, order.Unit, current_aum, true, false, true, false, false, false, false));
                                            Console.WriteLine("Rebook: " + order);

                                            if (strategy.Portfolio.ParentPortfolio != null)
                                            {
                                                strategy.Portfolio.UpdateReservePosition(timestamp, updateValue, strategy.Portfolio.Currency);
                                                strategy.Portfolio.ParentPortfolio.UpdateReservePosition(timestamp, -updateValue, strategy.Portfolio.Currency);
                                            }
                                        }
                                        else
                                        {
                                            //strategy.AddMemoryPoint(timestamp, 0, Strategy._aum_chg_id_do_not_use, (int)ttype);
                                            //UpdateOrderTree(order, OrderStatus.NotExecuted, double.NaN, double.NaN, timestamp);
                                        }
                                    }
                                }
                                else if (!IsReserve(order.Instrument))
                                {
                                    if (p == null)
                                        positions.Add(CreatePosition(order.Instrument, timestamp, order.Unit, order.ExecutionLevel));
                                    else
                                        positions.Add(p.UpdatePosition(timestamp, order.Unit, order.ExecutionLevel, RebalancingType.Reserve, UpdateType.UpdateUnits));

                                    Console.WriteLine("Rebook: " + order);
                                }
                                else
                                    SystemLog.Write("ORDER NOT BOOKED " + order + " " + this);
                            }
                            else if (!IsReserve(order.Instrument))
                            {
                                double executionLevel = order.ExecutionLevel;
                                if (!double.IsNaN(executionLevel))
                                {
                                    if (p == null)
                                        positions.Add(CreatePosition(order.Instrument, timestamp, order.Unit, executionLevel));
                                    else
                                        positions.Add(p.UpdatePosition(timestamp, order.Unit, executionLevel, RebalancingType.Reserve, UpdateType.UpdateUnits));

                                    Console.WriteLine("Rebook: " + order);
                                }
                                else
                                    SystemLog.Write("ORDER NOT BOOKED " + order + " " + this);
                            }
                            else
                                SystemLog.Write("ORDER NOT BOOKED " + order + " " + this);
                        }
                    }
            }
            return positions.Count == 0 ? null : positions;
        }

        /// <summary>
        /// Function: Hedge FX by performing an internal transfer units from non-base reserve assets such that no naked positions are help in a non-base reserve asset.
        /// </summary>    
        /// <param name="timestamp">reference timestamp for the hedge.
        /// </param>
        public void HedgeFX(DateTime timestamp)
        {
            // Hedge Currency Exposures
            List<Currency> ccys = new List<Currency>();
            List<Position> positions = Positions(timestamp);
            if (positions != null)
                foreach (Position pos in positions)
                    if (!ccys.Contains(pos.Instrument.Currency) && pos.Instrument.Currency != Currency)
                        ccys.Add(pos.Instrument.Currency);

            foreach (Currency ccy in ccys)
            {
                double ccyvalue = this[timestamp, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, ccy];
                if (!double.IsNaN(ccyvalue) && ccyvalue != 0.0)
                {
                    double ccyvlaue_ccy = CurrencyPair.Convert(ccyvalue, timestamp, Currency, ccy);

                    UpdateReservePosition(timestamp, -ccyvalue, ccy);
                    UpdateReservePosition(timestamp, ccyvlaue_ccy, Currency);

                    if (this[timestamp, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRollType.Last, ccy] != 0)
                        Console.WriteLine("Not Hedged");
                }
            }
        }

        /// <summary>
        /// Function: Margin the futures positions by adjusting the base-reserve asset to reflect the cash margining of the future.
        /// </summary>    
        /// <param name="timestamp">reference timestamp for the margin.
        /// </param>
        public void MarginFutures(DateTime timestamp)
        {
            // Margin All Future Positions            
            List<Position> positions = Positions(timestamp);

            if (positions != null)
                foreach (Position pos in positions)
                    if (pos.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Future && !double.IsNaN(pos.Instrument[timestamp, TimeSeriesType.Last, TimeSeriesRollType.Exact]))
                    {
                        Instrument instrument = pos.Instrument;
                        double unit = pos.Unit;

                        this.UpdatePositions(timestamp);

                        pos.UpdatePosition(timestamp, 0, instrument[timestamp, TimeSeriesType.Last, TimeSeriesRollType.Last] * (instrument as Future).PointSize, RebalancingType.Reserve, UpdateType.OverrideUnits);
                        CreatePosition(instrument, timestamp, unit, instrument[timestamp, TimeSeriesType.Last, TimeSeriesRollType.Last] * (instrument as Future).PointSize);
                    }
        }

        /// <summary>
        /// Function: Manage Corporate Actions for the positions.
        /// </summary>    
        /// <param name="timestamp">reference timestamp.
        /// </param>
        public void ManageCorporateActions(DateTime timestamp)
        {
            List<Position> positions = Positions(timestamp);
            if (positions != null)
                foreach (Position pos in positions)
                    this.ManageCorporateAction(pos, timestamp);
        }

        /// <summary>
        /// Function: Check if a corporate action has been processed.
        /// </summary>    
        /// <param name="action">reference corporate action.
        /// </param>
        private bool ProcessedCorporateAction(CorporateAction action)
        {
            return Factory.ProcessedCorporateAction(this, action);
        }

        /// <summary>
        /// Function: Process the corporate action and mark as processed.
        /// </summary>    
        /// <param name="action">reference corporate action.
        /// </param>
        /// <param name="timestamp">reference timestamp.
        /// </param>
        private void ProcessCorporateAction(CorporateAction action)
        {
            Factory.ProcessCorporateAction(this, action);
        }

        /// <summary>
        /// Function: Manage Corporate Actions for a given position.
        /// </summary>    
        /// <param name="position">reference position.
        /// </param>
        /// <param name="timestamp">reference timestamp.
        /// </param>
        public void ManageCorporateAction(Position position, DateTime timestamp)
        {
            lock (objLock)
            {
                Security instrument = position.Instrument as Security;

                if (instrument == null)
                    return;

                double ct = instrument[timestamp, TimeSeriesType.Last, TimeSeriesRollType.Last];

                BusinessDay bd = instrument.Calendar.GetClosestBusinessDay(timestamp, TimeSeries.DateSearchType.Next);

                if (bd.DateTime >= timestamp)
                {
                    DateTime refDate = bd.AddBusinessDays(-1).DateTime;

                    List<CorporateAction> actions = instrument.CorporateActions(refDate.Date);

                    if (actions != null && !double.IsNaN(ct))
                    {
                        foreach (CorporateAction action in actions)
                        {
                            if (!this.ProcessedCorporateAction(action))
                            {
                                if (action.Type != "Stock Split")
                                {
                                    Dictionary<string, Order> orders = position.Portfolio.FindOrder(position.Instrument, timestamp.AddDays(-1));
                                    if (orders != null)
                                        foreach (Order order in orders.Values.ToList())
                                            if (order != null)
                                            {
                                                double unit = order.Unit + action.Amount / ct;
                                                unit = position.Portfolio.OrderUnitCalculationFilter(position.Instrument, unit);
                                                OrderStatus st = order.Status;
                                                order.Status = OrderStatus.New;
                                                order.Unit = unit;
                                                order.Status = st;
                                            }


                                    if (action.Amount != 0)
                                        position.Portfolio.UpdateReservePosition(timestamp, action.Amount * position.Unit, position.Instrument.Currency);
                                }
                                else if (action.Type == "Stock Split")
                                {
                                    Dictionary<string, Order> orders = position.Portfolio.FindOrder(position.Instrument, timestamp.AddDays(-1));
                                    if (orders != null)
                                        foreach (Order order in orders.Values.ToList())
                                            if (order != null)
                                            {
                                                double unit = order.Unit * action.Amount;
                                                unit = position.Portfolio.OrderUnitCalculationFilter(position.Instrument, unit);
                                                OrderStatus st = order.Status;
                                                order.Status = OrderStatus.New;
                                                order.Unit = unit;
                                                order.Status = st;
                                            }


                                    if (action.Amount != 0)
                                    {
                                        double unit = position.Unit * action.Amount;
                                        unit = position.Portfolio.OrderUnitCalculationFilter(position.Instrument, unit);
                                        position.UpdatePosition(timestamp, unit, 0, RebalancingType.Reserve, UpdateType.OverrideUnits);
                                    }
                                }

                                this.ProcessCorporateAction(action);
                            }
                        }
                    }
                }
            }
        }

        private Dictionary<Instrument, string> _quickInstrumentDB = new Dictionary<Instrument, string>();

        /// <summary>
        /// Function: Add instrument to internal list of instruments linked to this portfolio during this runtime
        /// </summary>    
        /// <param name="instrument">reference instrument.
        /// </param>
        private void AddInstrument(Instrument instrument)
        {
            if (!_quickInstrumentDB.ContainsKey(instrument))
            {
                _quickInstrumentDB.Add(instrument, "");
                InstrumentList.Add(instrument);
            }

            if (ParentPortfolio != null)
                ParentPortfolio.AddInstrument(instrument);
        }

        /// <summary>
        /// Function: Update and create aggregated positions for a given instrument
        /// </summary>    
        /// <param name="instrument">reference instrument.
        /// </param>
        /// <param name="executionLevel">level of exeucution for this update
        /// </param>
        /// <param name="unit_diff">differential of units by which the existing units will change. New Unit = Old Unit + unit_diff.
        /// </param>
        /// <param name="date">reference date.
        /// </param>
        /// <remarks> Positions are updated recursively up the tree.        
        /// </remarks>
        internal void UpdateAggregatedPositionTree(Instrument instrument, double executionLevel, double unit_diff, DateTime date)
        {
            if (ParentPortfolio != null)
                ParentPortfolio.UpdateAggregatedPositionTree(instrument, executionLevel, unit_diff, date);

            Position position = FindPosition(instrument, date, true);
            double aggregated_unit = (position != null ? position.Unit : 0.0) + unit_diff;

            if (System.Math.Abs(aggregated_unit) < _tolerance)
                aggregated_unit = 0.0;

            CreatePosition(instrument, date, aggregated_unit, executionLevel, true, false, false, false, true, false, true);
        }

        /// <summary>
        /// Function: Update units of a position with a unit differential
        /// </summary>    
        /// <param name="instrument">reference instrument.
        /// </param>
        /// <param name="unit_diff">differential of units by which the existing units will change. New Unit = Old Unit + unit_diff.
        /// </param>
        /// <remarks> Positions are updated recursively up the tree.        
        /// </remarks>
        public void UpdatePositionTree(Position p, double unit_diff)
        {
            if (ParentPortfolio != null)
            {
                ParentPortfolio.UpdatePositions(p.Timestamp);
                ParentPortfolio.UpdatePositionTree(p, unit_diff);
            }

            Position position = FindPosition(p.Instrument, p.Timestamp, true);
            double aggregated_unit = (position != null ? position.Unit : 0.0) + unit_diff;

            if (System.Math.Abs(aggregated_unit) < _tolerance)
                aggregated_unit = 0.0;

            if (p.Instrument.InstrumentType != Kernel.InstrumentType.Strategy || (p.Instrument.InstrumentType == Kernel.InstrumentType.Strategy && (p.Instrument as Strategy).Portfolio == null))
            {

                if (position != null)
                    position.Unit = aggregated_unit;
                else
                    position = new Position(this.ID, p.InstrumentID, aggregated_unit, p.Timestamp, p.Strike, p.InitialStrikeTimestamp, p.InitialStrike, p.StrikeTimestamp, true);

                if (!_positionHistoryMemory_date_aggregated.ContainsKey(p.Timestamp))
                    _positionHistoryMemory_date_aggregated.TryAdd(p.Timestamp, new ConcurrentDictionary<int, Position>());

                if (!_positionHistoryMemory_date_aggregated[p.Timestamp].ContainsKey(p.Instrument.ID))
                    _positionHistoryMemory_date_aggregated[p.Timestamp].TryAdd(p.Instrument.ID, position);

                if (_positionHistoryMemory_date_aggregated[p.Timestamp].ContainsKey(p.Instrument.ID))
                    _positionHistoryMemory_date_aggregated[p.Timestamp][p.Instrument.ID] = position;

                if (!_positionHistoryMemory_instrument_aggregated.ContainsKey(p.Instrument.ID))
                    _positionHistoryMemory_instrument_aggregated.TryAdd(p.Instrument.ID, new ConcurrentDictionary<DateTime, Position>());

                if (!_positionHistoryMemory_instrument_aggregated[p.Instrument.ID].ContainsKey(p.Timestamp))
                    _positionHistoryMemory_instrument_aggregated[p.Instrument.ID].TryAdd(p.Timestamp, position);

                if (_positionHistoryMemory_instrument_aggregated[p.Instrument.ID].ContainsKey(p.Timestamp))
                    _positionHistoryMemory_instrument_aggregated[p.Instrument.ID][p.Timestamp] = position;
            }
        }

        /// <summary>
        /// Function: Update the memory and persistant storage regarding a given position.
        /// </summary>    
        /// <param name="position">reference position.
        /// </param>
        /// <param name="timestamp">reference timestamp.
        /// </param>
        /// <param name="addNew">True if the position is to be added to memory if it doesn't exist.
        /// </param>
        /// <param name="onlyMemory">True if persistent memory is not to be changed.
        /// </param>
        /// <remarks> Mostly for internal use only
        /// </remarks>
        public void UpdatePositionMemory(Position p, DateTime timestamp, Boolean addNew, Boolean onlyMemory)
        {
            if (addNew)
            {
                if (p.Aggregated)
                {
                    if (!(p.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio || (p.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && Strategy.FindStrategy(p.Instrument).Portfolio != null)))
                    {
                        if (!_positionHistoryMemory_instrument_aggregated.ContainsKey(p.Instrument.ID))
                            _positionHistoryMemory_instrument_aggregated.TryAdd(p.Instrument.ID, new ConcurrentDictionary<DateTime, Position>());

                        if (!_positionHistoryMemory_instrument_aggregated[p.Instrument.ID].ContainsKey(timestamp))
                            _positionHistoryMemory_instrument_aggregated[p.Instrument.ID].TryAdd(timestamp, p);
                        else
                            _positionHistoryMemory_instrument_aggregated[p.Instrument.ID][timestamp] = p;

                        if (!_positionHistoryMemory_date_aggregated.ContainsKey(timestamp))
                            _positionHistoryMemory_date_aggregated.TryAdd(timestamp, new ConcurrentDictionary<int, Position>());

                        if (!_positionHistoryMemory_date_aggregated[timestamp].ContainsKey(p.Instrument.ID))
                            _positionHistoryMemory_date_aggregated[timestamp].TryAdd(p.Instrument.ID, p);
                        else
                            _positionHistoryMemory_date_aggregated[timestamp][p.Instrument.ID] = p;

                        if (!(SimulationObject))
                        {
                            if (!_positionNewHistoryMemory_aggregated.ContainsKey(timestamp))
                                _positionNewHistoryMemory_aggregated.TryAdd(timestamp, new ConcurrentDictionary<int, Position>());

                            if (!_positionNewHistoryMemory_aggregated[timestamp].ContainsKey(p.Instrument.ID))
                                _positionNewHistoryMemory_aggregated[timestamp].TryAdd(p.Instrument.ID, p);
                            else
                                _positionNewHistoryMemory_aggregated[timestamp][p.Instrument.ID] = p;
                        }
                    }
                }
                else
                {
                    if (!_positionHistoryMemory_instrument.ContainsKey(p.Instrument.ID))
                        _positionHistoryMemory_instrument.TryAdd(p.Instrument.ID, new ConcurrentDictionary<DateTime, Position>());

                    if (!_positionHistoryMemory_instrument[p.Instrument.ID].ContainsKey(timestamp))
                        _positionHistoryMemory_instrument[p.Instrument.ID].TryAdd(timestamp, p);
                    else
                        _positionHistoryMemory_instrument[p.Instrument.ID][timestamp] = p;

                    if (!_positionHistoryMemory_date.ContainsKey(timestamp))
                    {
                        if (!_orderedDates.Contains(p.Timestamp))
                        {
                            if (_orderedDates.Count > 0 && _orderedDates[_orderedDates.Count - 1] > p.Timestamp)
                            {
                                _orderedDates.Add(p.Timestamp);
                                _orderedDates = _orderedDates.OrderBy(x => x).ToList();
                            }
                            else
                                _orderedDates.Add(p.Timestamp);
                        }

                        _positionHistoryMemory_date.TryAdd(timestamp, new ConcurrentDictionary<int, Position>());
                    }

                    if (!_positionHistoryMemory_date[timestamp].ContainsKey(p.Instrument.ID))
                        _positionHistoryMemory_date[timestamp].TryAdd(p.Instrument.ID, p);
                    else
                        _positionHistoryMemory_date[timestamp][p.Instrument.ID] = p;

                    if (!(SimulationObject))
                    {
                        if (!_positionNewHistoryMemory.ContainsKey(timestamp))
                            _positionNewHistoryMemory.TryAdd(timestamp, new ConcurrentDictionary<int, Position>());

                        if (!_positionNewHistoryMemory[timestamp].ContainsKey(p.Instrument.ID))
                            _positionNewHistoryMemory[timestamp].TryAdd(p.Instrument.ID, p);
                        else
                            _positionNewHistoryMemory[timestamp][p.Instrument.ID] = p;
                    }
                }
            }
            else
            {
                if (p.Aggregated)
                {
                    if (!(p.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio || (p.Instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && Strategy.FindStrategy(p.Instrument).Portfolio != null)))// && !IsReserve(instrument))
                    {
                        if (_positionHistoryMemory_instrument_aggregated.ContainsKey(p.Instrument.ID) && _positionHistoryMemory_instrument_aggregated[p.Instrument.ID].ContainsKey(timestamp))
                            _positionHistoryMemory_instrument_aggregated[p.Instrument.ID][timestamp] = p;

                        if (_positionHistoryMemory_date_aggregated.ContainsKey(timestamp) && _positionHistoryMemory_date_aggregated[timestamp].ContainsKey(p.Instrument.ID))
                            _positionHistoryMemory_date_aggregated[timestamp][p.Instrument.ID] = p;

                        if (!(SimulationObject))
                        {
                            if (_positionNewHistoryMemory_aggregated.ContainsKey(timestamp) && _positionNewHistoryMemory_aggregated[timestamp].ContainsKey(p.Instrument.ID))
                                _positionNewHistoryMemory_aggregated[timestamp][p.Instrument.ID] = p;
                        }
                    }
                }
                else
                {
                    if (_positionHistoryMemory_date.ContainsKey(timestamp) && _positionHistoryMemory_date[timestamp].ContainsKey(p.Instrument.ID))
                        _positionHistoryMemory_date[timestamp][p.Instrument.ID] = p;

                    if (_positionHistoryMemory_instrument.ContainsKey(p.Instrument.ID) && _positionHistoryMemory_instrument[p.Instrument.ID].ContainsKey(timestamp))
                        _positionHistoryMemory_instrument[p.Instrument.ID][timestamp] = p;

                    if (!(SimulationObject))
                    {
                        if (_positionNewHistoryMemory.ContainsKey(timestamp) && _positionNewHistoryMemory[timestamp].ContainsKey(p.Instrument.ID))
                            _positionNewHistoryMemory[timestamp][p.Instrument.ID] = p;

                    }
                }
            }

            if (!onlyMemory && !addNew && _loadedDates.Count != 0)
                Factory.UpdatePositionMemory(p, timestamp, addNew);
        }

        private Dictionary<Instrument, string> _quickAddedInstruments = new Dictionary<Instrument, string>();
        public static TimeSpan tt = (DateTime.Now - DateTime.Now);

        /// <summary>
        /// Function: Create a position
        /// </summary>    
        /// <param name="instrument">reference instrument.
        /// </param>
        /// <param name="timestamp">reference timestamp.
        /// </param>
        /// <param name="unit">number of units of the instrument.
        /// </param>
        /// <param name="executionLevel">execution level of the current number of units
        /// </param>
        public Position CreatePosition(Instrument instrument, DateTime timestamp, double unit, double executionLevel)
        {
            UpdatePositions(timestamp);
            return CreatePosition(instrument, timestamp, unit, executionLevel, false, false, true, true, true, true, false);
        }
        internal Position CreatePosition(Instrument instrument, DateTime timestamp, double unit, double executionLevel, Boolean updateIfExists, Boolean onlyUpdateTimestamp, Boolean updateReserve, Boolean updateStrategy, Boolean realizeCarryCost, Boolean realizeCarryCostRecursive, Boolean aggregated)
        {
            DateTime t1 = DateTime.Now;
            if (double.IsNaN(unit) || double.IsInfinity(unit))
                throw new Exception("Unit is NaN");

            if (LastTimestamp > timestamp)
                throw new Exception("Timestamp is prior to LatestTimestamp: " + timestamp + " " + LastTimestamp);

            AddInstrument(instrument);

            if (!_quickAddedInstruments.ContainsKey(instrument) && !IsReserve(instrument) && !aggregated && !updateIfExists && !onlyUpdateTimestamp && unit != 0.0)
            {
                if (Strategy != null)
                    Strategy.AddInstrument(instrument, timestamp);

                _quickAddedInstruments.Add(instrument, "");
            }

            double instrument_value_gross = 0.0;
            if (instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy)
            {
                Strategy strategy = Strategy.FindStrategy(instrument);
                if (strategy.Portfolio != null)
                {
                    if (strategy.Portfolio.ParentPortfolio == null)
                        strategy.Portfolio.ParentPortfolio = this;

                    double aum = strategy.GetAUM(timestamp, TimeSeriesType.Last);
                    double aum_chg = strategy.GetAUMChange(timestamp, TimeSeriesType.Last);
                    instrument_value_gross = aum - aum_chg;
                }
                else
                    instrument_value_gross = instrument[timestamp, TimeSeriesType.Last, DataProvider.DefaultProvider, TimeSeriesRoll];
            }
            else
                instrument_value_gross = executionLevel;

            if (instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio)
            {
                Portfolio sub_portfolio = Portfolio.FindPortfolio(instrument);
                sub_portfolio.UpdateNotional(timestamp, executionLevel, onlyUpdateTimestamp);

                unit = (unit == 0.0 ? 0.0 : 1.0);
            }
            else if (instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && !onlyUpdateTimestamp && updateStrategy)
            {
                Strategy strategy = Strategy.FindStrategy(instrument);

                if (strategy.Portfolio != null)
                {
                    executionLevel = instrument_value_gross;
                    strategy.UpdateAUM(timestamp, instrument_value_gross, true);
                    unit = (unit == 0.0 ? 0.0 : 1.0);
                }
            }

            if (realizeCarryCostRecursive)
                MasterPortfolio.RealizeCarryAggregatedPositions(instrument, timestamp);

            if (updateReserve && !IsReserve(instrument))
            {
                double unit_diff = unit;
                double value = 0;

                Position p = FindPosition(instrument, timestamp);
                if (p != null)
                {
                    if (instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio || (instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && Strategy.FindStrategy(instrument).Portfolio != null))
                    {
                        unit = (unit == 0.0 ? 0.0 : 1.0);
                        unit_diff = 1.0;
                        value = -(executionLevel - instrument_value_gross);
                    }
                    else
                    {

                        unit_diff = unit - p.Unit;

                        // if this is set, then doesn't margin carry costs if unit_diff == 0
                        if (unit_diff != 0.0 || realizeCarryCost)
                        {
                            if (instrument.FundingType == FundingType.ExcessReturn) value = 0;
                            else
                            {
                                double master_unit = p.MasterUnit(timestamp);
                                master_unit = double.IsNaN(master_unit) ? 0.0 : master_unit;
                                double prct = p.Unit;
                                prct *= (master_unit == 0 || Math.Sign(master_unit) != Math.Sign(p.Unit) ? 0.0 : Math.Max(-1.0, Math.Min(1.0, master_unit / p.Unit)));

                                value = -executionLevel * unit_diff + instrument.CarryCost(p.StrikeTimestamp, timestamp, TimeSeriesType.Last, (master_unit > 0 ? PositionType.Long : PositionType.Short)) * prct;
                            }
                        }
                    }
                }
                else
                {
                    if (instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Portfolio || (instrument.InstrumentType == AQI.AQILabs.Kernel.InstrumentType.Strategy && Strategy.FindStrategy(instrument).Portfolio != null))
                    {
                        unit = (unit == 0.0 ? 0.0 : 1.0);
                        unit_diff = 1.0;

                        value = -Math.Max(0, executionLevel);
                    }
                    else
                        value = -(executionLevel - (instrument.FundingType == AQI.AQILabs.Kernel.FundingType.ExcessReturn ? instrument_value_gross : 0.0)) * unit_diff;
                }

                if (value != 0)
                    UpdateReservePosition(timestamp, value, instrument.Currency, false, instrument.InstrumentType == Kernel.InstrumentType.Strategy);
            }

            if (aggregated ? _positionHistoryMemory_instrument_aggregated.ContainsKey(instrument.ID) && _positionHistoryMemory_instrument_aggregated[instrument.ID].ContainsKey(timestamp) : _positionHistoryMemory_instrument.ContainsKey(instrument.ID) && _positionHistoryMemory_instrument[instrument.ID].ContainsKey(timestamp))
            {
                // Update Position

                Position p = (aggregated ? _positionHistoryMemory_instrument_aggregated[instrument.ID][timestamp] : _positionHistoryMemory_instrument[instrument.ID][timestamp]);
                if (updateIfExists || p.Unit == 0)
                {
                    double unit_diff = unit;
                    if (!onlyUpdateTimestamp)
                    {
                        unit_diff = unit - p.Unit;

                        // if this is set, then doesn't reset carry costs
                        if (unit_diff != 0 || realizeCarryCost)
                        {
                            double pold = p.Strike;

                            if (instrument.FundingType == AQI.AQILabs.Kernel.FundingType.ExcessReturn)
                                p.Strike = IsReserve(instrument) ? unit : (Math.Abs(unit) < Math.Abs(p.Unit) ? p.Strike * unit / p.Unit : p.Strike + executionLevel * unit_diff);
                            else
                                p.Strike += IsReserve(instrument) ? unit_diff : executionLevel * unit_diff;

                            p.StrikeTimestamp = timestamp;
                            p.Timestamp = timestamp;

                            if (unit == 0)
                                p.Strike = 0;

                            if (p.InitialStrikeTimestamp == timestamp)
                                p.InitialStrike = p.Strike;

                            p.Unit = unit;
                        }
                    }

                    UpdatePositionMemory(p, timestamp, false, false);
                    if (timestamp > LastTimestamp)
                        LastTimestamp = timestamp;

                    if (!onlyUpdateTimestamp)
                        if (!aggregated)
                            UpdateAggregatedPositionTree(instrument, executionLevel, unit_diff, timestamp);

                    tt += (DateTime.Now - t1);
                    return p;
                }
                else
                    throw new Exception("Position Already Exists for: " + instrument + " old: " + p.Unit + " new: " + unit);
            }
            else
            {
                // Create New Position
                Position oldposition = FindLatestPosition(instrument, aggregated);

                double _unit, _strike, _initialStrike;
                DateTime _strikeTimestamp, _initialStrikeTimestamp;

                if (onlyUpdateTimestamp)
                {
                    if (oldposition != null)
                    {
                        _unit = oldposition.Unit;
                        _strike = oldposition.Strike;
                        _strikeTimestamp = oldposition.StrikeTimestamp;
                    }
                    else
                    {
                        _unit = unit;
                        _strike = IsReserve(instrument) ? executionLevel : executionLevel * unit;
                        _strikeTimestamp = timestamp;
                    }
                }
                else
                {
                    _unit = unit;
                    _strike = IsReserve(instrument) ? executionLevel : executionLevel * unit;
                    _strikeTimestamp = timestamp;
                }

                if (oldposition != null)
                {
                    if (oldposition.InitialStrikeTimestamp == timestamp)
                        _initialStrike = IsReserve(instrument) ? executionLevel : executionLevel * unit;
                    else
                        _initialStrike = oldposition.InitialStrike;

                    _initialStrikeTimestamp = oldposition.InitialStrikeTimestamp;
                }
                else
                {
                    _initialStrike = IsReserve(instrument) ? executionLevel : executionLevel * unit;
                    _initialStrikeTimestamp = timestamp;
                }

                Position p = new Position(this.ID, instrument.ID, _unit, timestamp, _strike, _initialStrikeTimestamp, _initialStrike, _strikeTimestamp, aggregated);

                UpdatePositionMemory(p, timestamp, true, false);



                if (timestamp > LastTimestamp)
                    LastTimestamp = timestamp;


                if (!onlyUpdateTimestamp)
                    if (!aggregated)
                        UpdateAggregatedPositionTree(instrument, executionLevel, unit, timestamp);
                tt += (DateTime.Now - t1);
                return p;
            }
        }

        /// <summary>
        /// Function: Find a portfolio based off a given instrument
        /// </summary>    
        /// <param name="instrument">reference instrument.
        /// </param>
        /// <param name="loadPositionsInMemory">True if the positions are to be loaded automatically into memory.
        /// </param>
        public static Portfolio FindPortfolio(Instrument instrument, Boolean loadPositionsInMemory)
        {
            return Factory.FindPortfolio(instrument, loadPositionsInMemory);
        }

        /// <summary>
        /// Function: Find a portfolio based off a given instrument
        /// </summary>    
        /// <param name="instrument">reference instrument.
        /// </param>
        public static Portfolio FindPortfolio(Instrument instrument)
        {
            return Factory.FindPortfolio(instrument);
        }

        /// <summary>
        /// Function: Create a portfolio
        /// </summary>    
        /// <param name="instrument">base instrument.
        /// </param>
        /// <param name="deposit">deposit base reserve instrument.
        /// </param>
        /// <param name="borrow">borrow base reserve instrument for shorting.
        /// </param>
        /// <param name="parent">parent portfolio in tree structure.
        /// </param>
        public static Portfolio CreatePortfolio(Instrument instrument, Instrument deposit, Instrument borrow, Portfolio parent)
        {
            return Factory.CreatePortfolio(instrument, deposit, borrow, parent);
        }

        /// <summary>
        /// Function: Create a portfolio
        /// </summary>    
        /// <param name="name">Name
        /// </param>
        /// <param name="date">Creating date
        /// </param>
        /// <param name="ccy">Base currency
        /// </param>
        /// <param name="simulated">True if not stored persistently.
        /// </param>
        public static Portfolio CreatePortfolio(string name, BusinessDay date, Currency ccy, bool simulated)
        {
            ConstantStrategy main_cash_strategy = ConstantStrategy.CreateStrategy(name + "/" + ccy + "/Cash", ccy, date, 1.0, simulated);

            Instrument portfolio_instrument = Instrument.CreateInstrument(name + "/Portfolio", InstrumentType.Portfolio, name + "/Portfolio", ccy, FundingType.TotalReturn, simulated);
            Portfolio portfolio = Portfolio.CreatePortfolio(portfolio_instrument, main_cash_strategy, main_cash_strategy, null);
            portfolio.TimeSeriesRoll = TimeSeriesRollType.Last;
            portfolio.AddReserve(ccy, main_cash_strategy, main_cash_strategy);

            return portfolio;
        }
    }
}