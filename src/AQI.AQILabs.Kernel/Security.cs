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

namespace AQI.AQILabs.Kernel
{
    /// <summary>
    /// Class containing the corporate action data. No processing logic is contained here. The processing logic is in the portfolio.
    /// </summary>
    public class CorporateAction : IEquatable<CorporateAction>
    {
        public bool Equals(CorporateAction other)
        {
            if (((object)other) == null)
                return false;
            return Security.ID == other.Security.ID && ExDate == other.ExDate && RecordDate == other.RecordDate && DeclaredDate == other.DeclaredDate && PayableDate == other.PayableDate && Amount == other.Amount && Frequency == other.Frequency && Type == other.Type;
        }
        public override bool Equals(object other)
        {
            try { return Equals((CorporateAction)other); }
            catch { return false; }
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(CorporateAction x, CorporateAction y)
        {
            if (((object)x) == null && ((object)y) == null)
                return true;
            else if (((object)x) == null)
                return false;

            return x.Equals(y);
        }
        public static bool operator !=(CorporateAction x, CorporateAction y)
        {
            return !(x == y);
        }

        public string ID { get; set; }

        /// <summary>
        /// Property: Security linked to the Corporate Action
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Security Security 
        { 
            get
            {
                if (SecurityID == -1)
                    return null;
                return Instrument.FindInstrument(SecurityID) as Security;
            }
        }

        /// <summary>
        /// Property: ID of the security linked to the Corporate Action
        /// </summary>
        public int SecurityID { get; set; }

        /// <summary>
        /// Property: Declared Date
        /// Date in which the action was announced
        /// </summary>
        public DateTime DeclaredDate { get; set; }

        /// <summary>
        /// Property: Ex Date
        /// The date at which a stock will trade "cum ex" (without entitlement). So for example in a normal cash dividend, if the exdate is 25.11.2008 then the stock will trade without the right to the cash dividendfrom the 25.11.2008 onwards. Cum (latin for with) and Ex (latin for without).
        /// Expiry date / Expiration date
        /// 1) The date at which an option or a warrant expires, and therefore cannot be exercised any longer.
        /// 2) The date at which a Tender Offer expires, ie the day up until shareholders can tender their shares to the offer.
        /// </summary>
        public DateTime ExDate { get; set; }

        /// <summary>
        /// Property: Ex Date
        /// The date at which your positions will be recorded in order to calculate your entitlements. So for example; if the positions in your account on record date are 100,000 shares and a cash dividend pays EUR 0.25 per share then your entitlement will be calculated as 100,000 x EUR 0.25 = EUR 25,000.
        /// </summary>
        public DateTime RecordDate { get; set; }

        /// <summary>
        /// Property: Pay date. When the action is paid.        
        /// </summary>
        public DateTime PayableDate { get; set; }

        /// <summary>
        /// Property: Amount
        /// </summary>
        public double Amount { get; set; }

        /// <summary>
        /// Property: Frequency
        /// </summary>
        public string Frequency { get; set; }

        /// <summary>
        /// Property: Type
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public CorporateAction(string ID, int securityID, DateTime declaredDate, DateTime exDate, DateTime recordDate, DateTime payableDate, double amount, string frequency, string type)
        {
            this.ID = ID;
            this.SecurityID = securityID;
            this.DeclaredDate = declaredDate;
            this.ExDate = exDate;
            this.RecordDate = recordDate;
            this.PayableDate = payableDate;

            this.Amount = amount;

            this.Frequency = frequency;
            this.Type = type;
        }

        /// <summary>
        /// Function: string representation of the action.
        /// </summary>
        public override string ToString()
        {
            return ExDate.ToShortDateString() + " (" + Type + ") " + Amount;
        }
    }

    /// <summary>
    /// Security class containing relevant information like exchange and ISIN while also logic for management of corporate actions.
    /// </summary>
    public class Security : Instrument
    {
        new public static AQI.AQILabs.Kernel.Factories.ISecurityFactory Factory = null;
        private static Dictionary<int, Security> _securityIdDB = new Dictionary<int, Security>();

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

        /// <summary>
        /// Function: List of corporate actions
        /// </summary>
        public List<CorporateAction> CorporateActions()
        {
            return Factory.CorporateActions(this);
        }

        /// <summary>
        /// Function: List of corporate actions for a given date
        /// </summary>
        public List<CorporateAction> CorporateActions(DateTime date)
        {
            return Factory.CorporateActions(this, date);
        }

        /// <summary>
        /// Function: Add corporate action to memory and persistent storage
        /// </summary>
        public void AddCorporateAction(CorporateAction action)
        {
            Factory.AddCorporateAction(this, action);
        }

        /// <summary>
        /// Function: Add set of corporate actions to memory and persistent storage
        /// </summary>
        public void AddCorporateAction(Dictionary<DateTime, List<CorporateAction>> actions)
        {
            Factory.AddCorporateAction(this, actions);
        }

        /// <summary>
        /// Function: Remove security from internal memory. Nothing to do with persistent storage
        /// </summary>
        public static void CleanMemory(Security security)
        {
            if (_securityIdDB.ContainsKey(security.ID))
            {
                _securityIdDB[security.ID] = null;
                _securityIdDB.Remove(security.ID);
            }
        }

        private  string _isin = null;

        /// <summary>
        /// Constructor
        /// </summary>
        public Security(Instrument instrument, string isin, int exchangeID)
            : base(instrument)
        {
            this._isin = isin;
            this.ExchangeID = exchangeID;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        [Newtonsoft.Json.JsonConstructor]
        public Security(int id, string isin, int exchangeID)
            : base(Instrument.FindCleanInstrument(id))
        {
            this._isin = isin;
            this.ExchangeID = exchangeID;
        }

        /// <summary>
        /// Property: ISIN value
        /// </summary>
        public string Isin
        {
            get
            {
                return _isin;
            }
            set
            {
                if (value == null)
                    throw new Exception("Value is NULL");
                this._isin = value;

                if (!SimulationObject)
                    Factory.SetProperty(this, "Isin", value);
            }
        }

        /// <summary>
        /// Property: ID of exchange this security is traded on
        /// </summary>
        public int ExchangeID { get; set; }

        /// <summary>
        /// Property: Exchange this security is traded on
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Exchange Exchange
        {
            get
            {
                return Exchange.FindExchange(ExchangeID);
            }
            set
            {
                if (value == null)
                    throw new Exception("Value is NULL");

                this.ExchangeID = value.ID;

                if (!SimulationObject)
                    Factory.SetProperty(this, "ExchangeID", value.ID);                
            }
        }

        //[Newtonsoft.Json.JsonIgnore]
        //new public Calendar Calendar
        //{
        //    get
        //    {
        //        return Exchange.Calendar;
        //    }
        //}

        //new public void Remove()
        //{
        //    Factory.Remove(this);
        //    base.Remove();
        //}

        /// <summary>
        /// Function: Create security
        /// </summary>        
        public static Security CreateSecurity(Instrument instrument, string isin, Exchange exchange)
        {
            return Factory.CreateSecurity(instrument, isin, exchange);
        }

        /// <summary>
        /// Function: Find security
        /// </summary>        
        public static Security FindSecurity(Instrument instrument)
        {
            return Factory.FindSecurity(instrument);
        }

    }
}
