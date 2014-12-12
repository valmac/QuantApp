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
    /// Class that stores objects and allows for easy query on the objects properties.    
    /// </summary>
    public class M
    {
        public static M Base = new M();

        private List<object> singularity = new List<object>();

        /// <summary>
        /// Function: Add an object        
        /// </summary>
        /// <param name="data">object to be added</param>
        public void Add(object data)
        {
            singularity.Add(data);
        }

        /// <summary>
        /// Function: Remove an object        
        /// </summary>
        /// <param name="data">object to be removed</param>
        public void Remove(object data)
        {
            if (singularity.Contains(data))
                singularity.Remove(data);
        }

        /// <summary>
        /// Operator: Add an object        
        /// </summary>
        /// <param name="y">object to be added</param>
        public static M operator +(M x, object y)
        {
            x.Add(y);
            return x;
        }

        /// <summary>
        /// Operator: Remove an object        
        /// </summary>
        /// <param name="y">object to be removed</param>
        public static M operator -(M x, object y)
        {
            x.Remove(y);
            return x;
        }


        /// <summary>
        /// Function: Query the M through a predicate
        /// </summary>
        /// <param name="predicate">query</param>
        public List<object> this[Func<object, bool> predicate]
        {
            get
            {                
                return singularity.Where(predicate).Select(x => x).ToList();
            }
        }

        /// <summary>
        /// Function: Returns the value of a property for a given object
        /// </summary>
        /// <param name="T">Type of return</param>
        /// <param name="x">object</param>
        /// <param name="property">Property Name</param>
        public static T V<T>(object x, string property)
        {
            object nul = null;
            if (typeof(T) == typeof(string))
                nul = "";
            else if (typeof(T) == typeof(int))
                nul = int.MinValue;
            else if (typeof(T) == typeof(double))
                nul = double.NaN;
            else if (typeof(T) == typeof(DateTime))
                nul = DateTime.MinValue;
            else if (typeof(T) == typeof(bool))
                nul = false;

            var obj = (x.GetType().GetProperty(property) != null ? x.GetType().GetProperty(property).GetValue(x, null) : null);

            if (obj == null)
                return (T)nul;

            Type objType = obj.GetType();
            if (objType == typeof(T))
                return (T)obj;
            else
                return (T)nul;
        }
    }
}
