/*
 * Portions Copyright (c) 2011-2013 AQI Capital Advisors Limited.  All Rights Reserved.
 * This file contains Original Code and/or Modifications of Original Code as defined in and that are subject to the AQI Public Source License Version 1.0 (the 'License').
 * You may not use this file except in compliance with the License.  Please obtain a copy of the License at http://www.aqicapital.com/home/open/ and read it before using this file.
 * The Original Code and all software distributed under the License are distributed on an 'AS IS' basis, WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, AND AQI HEREBY DISCLAIMS ALL SUCH WARRANTIES,
 * INCLUDING WITHOUT LIMITATION, ANY WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, QUIET ENJOYMENT OR NON-INFRINGEMENT.
 * Please see the License for the specific language governing rights and limitations under the License.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;
using System.ComponentModel;

using AQI.AQILabs.Kernel.Factories;


namespace AQI.AQILabs.Kernel
{
    /// <summary>
    /// Class managing the connection to the market through all linked brokers.
    /// The Market Class enables the usage of multiple broker technologies through one system.
    /// Instructions define how the Market Class routes orders for specific constracts for a given strategy allowing the 
    /// developer to customise the execution process.    
    /// </summary>
    public class Market : MarshalByRefObject
    {
        private static Dictionary<string, OrderRecord> orderDict = new Dictionary<string, OrderRecord>();
        public static IMarketFactory Factory = null;        
        public delegate void UpdateEvent(OrderRecord record);

        /// <summary>        
        /// Delegate: The Submit defines the delegate that implementes the order submission function through a given client.
        /// </summary>
        public delegate void SubmitType(Order order);

        /// <summary>
        /// Structure representing the skeleton of a market connection.
        /// ClientConnection contains a list of destinations representing names of the brokers/markets the order are routed through.
        /// The SubmitFunction holds the delegate that implementes the order submission function through this client.
        /// </summary>
        public struct ClientConnection
        {
            public string Name;
            public List<string> Destinations;
            public SubmitType SubmitFunction;

            public ClientConnection(string Name, List<string> Destinations, SubmitType SubmitFunction)
            {
                this.Name = Name;
                if (Destinations == null || (Destinations != null && Destinations.Count == 0))
                {
                    this.Destinations = new List<string>();
                    this.Destinations.Add("Empty");
                }
                else
                    this.Destinations = Destinations;

                this.SubmitFunction = SubmitFunction;
            }
        }


        /// <summary>        
        /// Function: Add a connection used by the market class to route orders.
        /// </summary>
        /// <param name="connection">reference connection</param>
        public static void AddConnection(Market.ClientConnection connection)
        {
            if (!_connectionDB.ContainsKey(connection.Name))
                _connectionDB.Add(connection.Name, connection);
            //Factory.AddConnection(connection);
        }

        /// <summary>        
        /// Function: Remove a connection.
        /// </summary>
        /// <param name="name">name of the connection</param>
        public static void RemoveConnection(string name)
        {
            if (_connectionDB.ContainsKey(name))
                _connectionDB.Remove(name);

            //Factory.RemoveConnection(name);
        }

        protected static List<Portfolio> portfolios = new List<Portfolio>();

        /// <summary>        
        /// Function: Add a portfolio to be monitored and managed by the Market.
        /// </summary>
        /// <param name="portfolio">reference portfolio</param>
        public static void AddPortfolio(Portfolio portfolio)
        {
            if (!portfolios.Contains(portfolio))
                portfolios.Add(portfolio);
        }

        /// <summary>        
        /// Function: Remove a portfolio that has been monitored and managed by the Market.
        /// </summary>
        /// <param name="portfolio">reference portfolio</param>
        public static void RemovePortfolio(Portfolio portfolio)
        {
            if (portfolios.Contains(portfolio))
                portfolios.Remove(portfolio);
        }

        /// <summary>        
        /// Function: Submit all new orders for a given portfolio generated at a specific time
        /// </summary>
        /// <param name="portfolio">reference portfolio</param>
        /// <param name="orderDate">reference date</param>
        public static object[] SubmitOrders(DateTime orderDate, Portfolio portfolio)
        {
            Dictionary<int, Dictionary<string, Order>> orders = portfolio.OpenOrders(orderDate, true);

            if (orders != null && orders.Count != 0)
            {
                List<Order> ordersSubmit = new List<Order>();
                foreach (int i in orders.Keys)
                {
                    Dictionary<string, Order> os = orders[i];
                    foreach (string orderID in os.Keys.ToList())
                    {
                        Order order = os[orderID];

                        if (!order.Portfolio.MasterPortfolio.Strategy.Simulating)
                        {
                            if (order.OrderDate == Calendar.Close(order.OrderDate))
                                portfolio.UpdateOrderTree(order, OrderStatus.Submitted, double.NaN, double.NaN, DateTime.MaxValue);
                            if (order.Unit != 0.0 && !ordersSubmit.Contains(order))
                                ordersSubmit.Add(order);
                        }

                        Submit(order);
                    }
                }
            }

            return null;
        }

        /// <summary>        
        /// Function: Submit a specific order
        /// </summary>
        /// <param name="order">reference order</param>        
        public static object Submit(Order order)
        {            
            if (order.Unit != 0 && order.Status == OrderStatus.New)
            {
                Instrument instrument = order.Instrument;

                Dictionary<int, Dictionary<int, Instruction>> list = Instructions();
                Instruction defaultInstruction = list.ContainsKey(0) && list[0].ContainsKey(0) ? list[0][0] : null;
                Instruction portfolioDefault = list.ContainsKey(order.Portfolio.MasterPortfolio.ID) && list[order.Portfolio.MasterPortfolio.ID].ContainsKey(0) ? list[order.Portfolio.MasterPortfolio.ID][0] : null;

                Instruction instruction = GetInstruction(order);

                if (instruction != null && (order.Client == null || string.IsNullOrWhiteSpace(order.Client)))
                {
                    if (instruction.Client == "Inherit")
                    {
                        if (portfolioDefault == null || portfolioDefault.Client == "Inherit")
                        {
                            order.Client = defaultInstruction.Client;
                            order.Destination = defaultInstruction.Destination;
                            order.Account = defaultInstruction.Account;
                        }
                        else
                        {
                            order.Client = portfolioDefault.Client;
                            order.Destination = portfolioDefault.Destination;
                            order.Account = portfolioDefault.Account;
                        }

                    }
                    else
                    {
                        order.Client = instruction.Client;
                        order.Destination = instruction.Destination;
                        order.Account = instruction.Account;
                    }
                    order.Portfolio.MasterPortfolio.UpdateOrderTree(order, OrderStatus.Submitted, double.NaN, double.NaN, DateTime.MaxValue, order.Client, order.Destination, order.Account);
                }
                else
                    order.Portfolio.MasterPortfolio.UpdateOrderTree(order, OrderStatus.Submitted, double.NaN, double.NaN, DateTime.MaxValue);


                if (order.Portfolio.MasterPortfolio.Strategy.Simulating)
                    return null;

                OrderRecord orderRecord = new OrderRecord()
                {
                    Date = order.OrderDate.TimeOfDay.ToString(),
                    OrderID = order.ID,
                    Name = order.Instrument.Name,
                    Side = order.Unit > 0 ? "Long" : "Short",
                    Type = order.Type.ToString(),
                    Unit = Math.Abs(order.Unit).ToString(),
                    Price = (decimal)order.Limit,
                    Status = "New",
                    RootPortfolioID = order.Portfolio.MasterPortfolio.ID,
                    ParentPortfolioID = order.Portfolio.ID
                };

                DateTime t1 = DateTime.Now;

                Dictionary<string, Market.ClientConnection> clientConnections = _connectionDB;// Factory.GetClientConnetions();

                if (clientConnections.ContainsKey(order.Client))
                {
                    if (clientConnections[order.Client].SubmitFunction != null)
                        clientConnections[order.Client].SubmitFunction(order);
                }

                if (order.OrderDate.Date == DateTime.Today)
                {
                    if (orderDict.ContainsKey(orderRecord.OrderID))
                        orderDict[orderRecord.OrderID] = orderRecord;
                    else
                        orderDict.Add(orderRecord.OrderID, orderRecord);

                    UpdateRecord(orderRecord);
                }
                return orderRecord;
            }
            return null;

        }

        /// <summary>        
        /// Function: Receive execution levels for a given portfolio. This function is usually used for historical simulations.
        /// </summary>
        /// <param name="executionDate">reference date for the executed orders</param>   
        /// <param name="portfolio">reference portfolio</param>
        public static void ReceiveExecutionLevels(DateTime executionDate, Portfolio portfolio)
        {
            Dictionary<int, Dictionary<string, Order>> orders = portfolio.OpenOrders(executionDate, true);
            if (orders != null)
            {
                foreach (Dictionary<string, Order> os in orders.Values)
                    foreach (Order order in os.Values)
                    {
                        if (order.Status == OrderStatus.Submitted)
                        {
                            Instrument instrument = order.Instrument;
                            BusinessDay date = Calendar.FindCalendar("WE").GetBusinessDay(executionDate);

                            if (date != null)
                            {
                                double executionLevel = instrument[executionDate, TimeSeriesType.Last, instrument.InstrumentType == InstrumentType.Strategy ? TimeSeriesRollType.Last : TimeSeriesRollType.Last];

                                if (instrument.InstrumentType == InstrumentType.Future)
                                    executionLevel *= (instrument as Future).PointSize;


                                if (!double.IsNaN(executionLevel))
                                {
                                    if (order.Unit != 0.0)
                                    {
                                        if (true)
                                        {
                                            if (instrument.InstrumentType == InstrumentType.Future)
                                            {
                                                double n_contracts = order.Unit;

                                                double exec_fee = 0.0;

                                                Dictionary<int, Dictionary<int, Instruction>> instructions = Factory.Instructions();

                                                if (instructions.ContainsKey(portfolio.ID))
                                                {
                                                    if (instructions[portfolio.ID].ContainsKey(instrument.ID))
                                                        exec_fee = instructions[portfolio.ID][instrument.ID].ExecutionFee;

                                                    else if (instructions[portfolio.ID].ContainsKey((instrument as Future).UnderlyingID))
                                                        exec_fee = instructions[portfolio.ID][(instrument as Future).UnderlyingID].ExecutionFee;

                                                    else if (instructions[portfolio.ID].ContainsKey(0))
                                                        exec_fee = instructions[portfolio.ID][0].ExecutionFee;
                                                }

                                                if (instructions.ContainsKey(0))
                                                {
                                                    if (instructions[0].ContainsKey(instrument.ID))
                                                        exec_fee = instructions[0][instrument.ID].ExecutionFee;
                                                    else if (instructions[0].ContainsKey(0))
                                                        exec_fee = instructions[0][0].ExecutionFee;
                                                }

                                                if (order.Unit > 0)
                                                    executionLevel += exec_fee;
                                                else if (order.Unit < 0)
                                                    executionLevel -= exec_fee;

                                                Instrument underlying = (instrument as Future).Underlying;
                                                double value = underlying.ExecutionCost;
                                                if (value < 0)
                                                {
                                                    value = -value;
                                                    if (order.Unit > 0)
                                                        executionLevel += value * executionLevel;
                                                    else
                                                        executionLevel -= value * executionLevel;
                                                }
                                                else
                                                {
                                                    value *= (instrument as Future).PointSize;
                                                    if (order.Unit > 0)
                                                        executionLevel += value;
                                                    else
                                                        executionLevel -= value;
                                                }
                                            }                                            
                                        }
                                    }

                                    portfolio.UpdateOrderTree(order, OrderStatus.Executed, double.NaN, executionLevel, executionDate);
                                }
                                else
                                    portfolio.UpdateOrderTree(order, OrderStatus.NotExecuted, double.NaN, executionLevel, executionDate);
                            }
                        }
                    }
            }
        }

        /// <summary>        
        /// Function: Add an instruction to the market class
        /// </summary>
        /// <param name="instruction">reference instruction</param>   
        public static void AddInstruction(Instruction instruction)
        {
            Factory.AddInstruction(instruction);
        }

        /// <summary>        
        /// Function: returns an instruction for a given order
        /// </summary>
        /// <param name="order">reference order</param>  
        public static Instruction GetInstruction(Order order)
        {
            Dictionary<int, Dictionary<int, Instruction>> list = Instructions();

            int masterID = order.Portfolio.MasterPortfolio.ID;

            Instruction inherit = new Instruction(null, null, "Inherit", " ", " ", 0);

            Instruction defaultInstruction = list.ContainsKey(0) && list[0].ContainsKey(0) ? list[0][0] : null;
            Instruction portfolioDefault = list.ContainsKey(masterID) && list[masterID].ContainsKey(0) ? list[masterID][0] : null;

            if (list.ContainsKey(masterID) && list[masterID].ContainsKey(order.InstrumentID))
                return list[masterID][order.InstrumentID];
            else if (portfolioDefault != null)
                return portfolioDefault;
            else
                return inherit;
        }

        /// <summary>        
        /// Function: returns a dictionary of all instructions.
        /// The dictionary's keys are portfolio IDs and the values are a second set of dictionaries.
        /// The second set of dictionary's keys are instrument IDs and the values are the instructions.
        /// </summary>
        public static Dictionary<int, Dictionary<int, Instruction>> Instructions()
        {
            return Factory.Instructions();
        }


        private static Dictionary<string, Market.ClientConnection> _connectionDB = new Dictionary<string, Market.ClientConnection>();
        /// <summary>        
        /// Function: returns a dictionary of all destinations categories by their respective clients.
        /// The dictionary's keys are clients and the values are a second set of lists containing the respective destinations.        
        /// </summary>
        public static Dictionary<string, List<string>> ClientsDestinations()
        {            
            Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();

            result.Add("Simulator", new List<string> { "Default" });
            result.Add("Inherit", new List<string> { " " });

            foreach (Market.ClientConnection connection in _connectionDB.Values)
                if (!result.ContainsKey(connection.Name))
                    result.Add(connection.Name, connection.Destinations);

            return result;
        }


        /// <summary>        
        /// Function: Execute simulated orders during live paper trading.
        /// </summary>
        public static void ExecuteSimulatedOrders()
        {
            foreach (Portfolio portfolio in portfolios.ToList())
            {
                if (!(portfolio.MasterPortfolio != null && portfolio.MasterPortfolio == portfolio))
                    RemovePortfolio(portfolio);

                if (portfolio.Strategy != null && !portfolio.Strategy.Simulating)
                {
                    try
                    {
                        if (portfolio != null)
                        {
                            bool book = false;
                            DateTime t = DateTime.Now;
                            Dictionary<int, Dictionary<string, Order>> orders = portfolio.OpenOrders(t, true);
                            if (orders != null)
                            {
                                foreach (Dictionary<string, Order> os in orders.Values.ToList())
                                {
                                    foreach (Order order in os.Values.ToList())
                                        if (order.Status == OrderStatus.Submitted && order.Client == "Simulator")
                                            if (order.OrderDate.Date == DateTime.Today)
                                            {
                                                try
                                                {
                                                    double last = order.Instrument[t, TimeSeriesType.Last, TimeSeriesRollType.Last];
                                                    double last_5h = order.Instrument[t.AddHours(-5), TimeSeriesType.Last, TimeSeriesRollType.Last];

                                                    //if (last != last_5h)
                                                    {
                                                        double bid = order.Instrument[t, TimeSeriesType.Bid, TimeSeriesRollType.Last];
                                                        double ask = order.Instrument[t, TimeSeriesType.Ask, TimeSeriesRollType.Last];

                                                        if (double.IsNaN(bid))
                                                            bid = last;

                                                        if (double.IsNaN(ask))
                                                            ask = last;

                                                        if (order.Type == OrderType.Market || (order.Limit >= ask && order.Unit > 0) || (order.Limit <= bid && order.Unit < 0))
                                                        {
                                                            OrderRecord ord = Market.GetOrderRecord(order.ID);
                                                            if (ord != null)
                                                            {
                                                                ord.Price = (decimal)(order.Unit < 0 ? bid : ask) * (order.Instrument.InstrumentType == InstrumentType.Future ? (decimal)(order.Instrument as Future).PointSize : 1);
                                                                portfolio.UpdateOrderTree(order, OrderStatus.Executed, double.NaN, (double)ord.Price, t);
                                                                ord.Status = "Executed";

                                                                book = true;
                                                                Market.RemoveOrderRecord(order.ID);

                                                                UpdateRecord(ord);
                                                            }
                                                            else
                                                            {

                                                                book = true;
                                                                portfolio.UpdateOrderTree(order, OrderStatus.Executed, double.NaN, (order.Unit < 0 ? bid : ask), t);
                                                            }
                                                        }
                                                    }
                                                }
                                                catch (Exception e)
                                                {
                                                    Console.WriteLine(e);
                                                }
                                            }
                                }
                            }
                            int num = portfolio.MasterPortfolio.Strategy.Tree.BookOrders(t);

                            if (book || num != 0)
                                UpdateRecord(null);

                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }
            }
        }


        private static Market.UpdateEvent _updateDB;
        /// <summary>        
        /// Function: Add an update function that is called by the Market connection when an order status is changed.
        /// This function is usually used to implement a UI change but can be used for any external updating of information linked to an order.
        /// </summary>
        public static void AddUpdateEvent(Market.UpdateEvent updateEvent)
        {
            _updateDB += updateEvent;            
        }

        /// <summary>        
        /// Function: remove an update function that is called by the Market connection when an order status is changed.
        /// </summary>
        public static void RemoveUpdateEvent(Market.UpdateEvent updateEvent)
        {
            _updateDB -= updateEvent;            
        }

        /// <summary>        
        /// Function: update all the update events added to the market with this record
        /// </summary>
        public static void UpdateRecord(OrderRecord record)
        {
            try
            {
                if (_updateDB != null)
                    _updateDB(record);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }            
        }

        /// <summary>        
        /// Function: Update and a fill value and time for a specific order. This is usually called by the market connection when the brokers reverts with a fill confirmation.
        /// </summary>
        public static void RecordFillValue(Order order, DateTime fillTime, double fillValue)
        {
            order.Portfolio.MasterPortfolio.UpdateOrderTree(order, OrderStatus.Executed, double.NaN, fillValue, fillTime);
            OrderRecord record = Market.GetOrderRecord(order.ID);
            record.Price = (decimal)order.ExecutionLevel;
            record.Status = "Executed";
            UpdateRecord(record);
            Market.RemoveOrderRecord(order.ID);
        }

        /// <summary>        
        /// Function: Returns the OrderRecord object related to an order ID.
        /// The OrderRecord is an object that implements a notification mechanism to inform external systems about changes to the order.
        /// This is implemented outside of the Order object in order to keep the internal process as quick and lightweight as possible.
        /// </summary>
        public static OrderRecord GetOrderRecord(string id)
        {
            if (orderDict.ContainsKey(id))
                return orderDict[id];
            return null;
        }

        /// <summary>        
        /// Function: Remove an order record when it is not required.        
        /// </summary>
        private static void RemoveOrderRecord(string id)
        {
            if (orderDict.ContainsKey(id))
                orderDict.Remove(id);
        }
    }

    /// <summary>
    /// Class containing the information required by the Market Class to route an order through the correct broker technology.
    /// The Instruction is defined by:
    /// Portfolio -> The portfolio the instruction is linked to
    /// Instrument -> The instrument  the instruction is meant to transact
    /// Client -> The string identifier for the API connection used to route the order. Could be EMSX API, QuickFIX, IBAPI, IGAPI, etc.
    /// Destination -> The string identifier to which broker/router to use for the defined client. If the Client is EMSX, the destination is the broker identifier
    /// Account -> The account where the order should be routed to and settled
    /// ExecutionFee -> Fee that is not passed through the API information if any
    /// </summary>
    public class Instruction
    {
        /// <summary>
        /// Property: returns the portfolio the instruction is linked to
        /// </summary>
        public Portfolio Portfolio { get; private set; }

        /// <summary>
        /// Property: returns the instrument the instruction is meant to transact
        /// </summary>
        public Instrument Instrument { get; private set; }

        /// <summary>
        /// Property: returns the string identifier for the API connection used to route the order. Could be EMSX API, QuickFIX, IBAPI, IGAPI, etc.
        /// </summary>
        public string Client { get; set; }

        /// <summary>
        /// Property: returns the string identifier to which broker/router to use for the defined client. If the Client is EMSX, the destination is the broker identifier
        /// </summary>
        public string Destination { get; set; }

        /// <summary>
        /// Property: returns the account where the order should be routed to and settled
        /// </summary>
        public string Account { get; set; }

        /// <summary>
        /// Property: returns the fee that is not passed through the API information if any
        /// </summary>
        public double ExecutionFee { get; set; }

        public Instruction(Portfolio portfolio, Instrument instrument, string client, string destination, string account, double executionfee)
        {
            Portfolio = portfolio;
            Instrument = instrument;
            Client = client;
            Destination = destination;
            Account = account;
            ExecutionFee = executionfee;
        }
    }

    /// <summary>
    /// The OrderRecord class is used to notify external systems regarding status changes.
    /// This is usually used by UI updates.
    /// </summary>
    public class OrderRecord : NotifyPropertyChangedBase, IEquatable<OrderRecord>
    {        
        public OrderRecord()
        {
        }
        
        public bool Equals(OrderRecord other)
        {
            if (((object)other) == null)
                return false;
            return _orderID == other._orderID;
        }
        public override bool Equals(object other)
        {
            try { return Equals((OrderRecord)other); }
            catch { return false; }
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(OrderRecord x, OrderRecord y)
        {
            if (((object)x) == null && ((object)y) == null)
                return true;
            else if (((object)x) == null)
                return false;

            return x.Equals(y);
        }
        public static bool operator !=(OrderRecord x, OrderRecord y)
        {
            return !(x == y);
        }

        private string date = "";
        public string Date
        {
            get { return date; }
            set { date = value; OnPropertyChanged("Date"); }
        }

        private string _name = "";
        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged("Name"); }
        }
       
        private string _side = "";
        public string Side
        {
            get { return _side; }
            set { _side = value; OnPropertyChanged("Side"); }
        }

        private string _ordType = "";
        public string Type
        {
            get { return _ordType; }
            set { _ordType = value; OnPropertyChanged("Type"); }
        }

        private decimal _price = 0m;
        public decimal Price
        {
            get { return _price; }
            set { _price = value; OnPropertyChanged("Price"); }
        }

        private string _ordQty;
        public string Unit
        {
            get { return _ordQty; }
            set { _ordQty = value; OnPropertyChanged("Unit"); }
        }

        private string _status { get; set; }
        public string Status
        {
            get { return _status; }
            set { _status = value; OnPropertyChanged("Status"); }
        }

        private string _orderID = "(unset)";
        public string OrderID
        {
            get { return _orderID; }
            set { _orderID = value; OnPropertyChanged("OrderID"); }
        }

        private int _rootPortfolioId { get; set; }
        public int RootPortfolioID
        {
            get { return _rootPortfolioId; }
            set { _rootPortfolioId = value; OnPropertyChanged("MasterID"); }
        }

        private int _parentPortfolioId { get; set; }
        public int ParentPortfolioID
        {
            get { return _parentPortfolioId; }
            set { _parentPortfolioId = value; OnPropertyChanged("PortfolioID"); }
        }

    }

    public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Members

        /// <summary>
        /// Raised when a property on this object has a new value.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

        #endregion // INotifyPropertyChanged Members
    }
}
