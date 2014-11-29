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

using Quartz;
using Quartz.Impl;

namespace AQI.AQILabs.Kernel
{
    /// <summary>
    /// Class managing the logic of the Quartz based scheduler for the execution of strategy logic.
    /// </summary>
    public class StrategyJobExecutor
    {
 
        private Strategy _strategy = null;

        /// <summary>
        /// Constructor
        /// </summary>
        public StrategyJobExecutor(Strategy strategy)
        {
            _strategy = strategy;
        }

        IScheduler _sched = null;

        /// <summary>
        /// Function: start job with a given schedule
        /// </summary>
        /// <param name="schedule">Quartz formatted schedule</param>
        public void StartJob(string schedule)
        {
            // First we must get a reference to a scheduler
            ISchedulerFactory sf = new StdSchedulerFactory();
            _sched = sf.GetScheduler();


            // jobs can be scheduled before sched.start() has been called

            // job 1 will run every X minute

            string jobID = "Strategy Job " + _strategy.ID + " " + schedule;

            IJobDetail job = JobBuilder.Create<StrategyJob>()
                .WithIdentity(jobID, "Strategy Group")
                .Build();

            ICronTrigger trigger = (ICronTrigger)TriggerBuilder.Create()
                .WithIdentity(jobID, "Strategy Group")
                //.WithCronSchedule("0 0/" + minutes + " * * * ?")
                .WithCronSchedule(schedule)
                .Build();

            job.JobDataMap["Strategy"] = _strategy;            

            DateTimeOffset ft = _sched.ScheduleJob(job, trigger);

            // All of the jobs have been added to the scheduler, but none of the
            // jobs
            // will run until the scheduler has been started
            _sched.Start();
        }

        /// <summary>
        /// Function: stop job
        /// </summary>
        public void StopJob()
        {
            if (_sched != null)
            {
                _sched.Shutdown(true);
            }
        }
    }

    /// <summary>
    /// Class representing the strategy execution job
    /// </summary>
    public class StrategyJob : IJob
    {
        private static DateTime Round(DateTime dateTime, TimeSpan interval)
        {
            var halfIntervelTicks = (interval.Ticks + 1) >> 1;

            return dateTime.AddTicks(halfIntervelTicks - ((dateTime.Ticks + halfIntervelTicks) % interval.Ticks));
        }

        public Boolean Executing = false;

        /// <summary>
        /// Function: implemention of Quartz.Net job in the following steps.
        /// 1) PreNAV Calculation
        /// 2) Manage Corporate Actions
        /// 3) Margin Futures
        /// 4) Hedge FX
        /// 5) NAV Calculations
        /// 6) Execute Logic
        /// 7) Post Logic Execution
        /// 8) Submit Orders
        /// 9) Save data and new positions
        /// </summary>
        public virtual void Execute(IJobExecutionContext context)
        {
            Executing = true;

            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-GB");


            JobDataMap dataMap = context.JobDetail.JobDataMap;

            Strategy strategy = (Strategy)dataMap["Strategy"];

            // This job simply prints out its job name and the
            // date and time that it is running
            JobKey jobKey = context.JobDetail.Key;
            DateTime date = DateTime.Now;
            date = Round(date, new TimeSpan(0, 1, 0));

            if (strategy.JobCalculation != null)
                strategy.JobCalculation(date);
            else
            {
                SystemLog.Write(date, null, SystemLog.Type.Production, string.Format("StrategyJob says: {0} executing: {1}", jobKey, strategy));

                DateTime t1 = DateTime.Now;
                strategy.Tree.PreNAVCalculation(date);
                DateTime t2 = DateTime.Now;
                strategy.Tree.ManageCorporateActions(date);
                DateTime t3 = DateTime.Now;
                strategy.Tree.MarginFutures(date);
                DateTime t4 = DateTime.Now;

                strategy.Tree.HedgeFX(date);
                DateTime t5 = DateTime.Now;
                strategy.Tree.NAVCalculation(date);
                DateTime t6 = DateTime.Now;

                strategy.Tree.ExecuteLogic(date);
                DateTime t7 = DateTime.Now;
                strategy.Tree.PostExecuteLogic(date);
                DateTime t8 = DateTime.Now;
                strategy.Portfolio.SubmitOrders(date);
                DateTime t9 = DateTime.Now;

                TimeSpan d1 = t2 - t1;
                TimeSpan d2 = t3 - t2;
                TimeSpan d3 = t4 - t3;
                TimeSpan d4 = t5 - t4;
                TimeSpan d5 = t6 - t5;
                TimeSpan d6 = t7 - t6;
                TimeSpan d8 = t9 - t8;

                if (!strategy.SimulationObject)
                {
                    //DateTime t1 = DateTime.Now;
                    strategy.Tree.SaveNewPositions();
                    //DateTime t2 = DateTime.Now;
                    //SystemLog.Write("Position Save Time: " + (t2 - t1));
                    //t1 = DateTime.Now;
                    // THINK ABOUT THIS
                    strategy.Tree.Save();
                    //t2 = DateTime.Now;
                    //SystemLog.Write("Strategy Save Time: " + (t2 - t1));
                }

                SystemLog.Write(DateTime.Now, null, SystemLog.Type.Production, string.Format("StrategyJob says: {0} executed: {1}", jobKey, strategy));
            }

            Executing = false;
        }
    }
}
