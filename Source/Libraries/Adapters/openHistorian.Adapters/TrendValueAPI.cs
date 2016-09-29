﻿//******************************************************************************************************
//  TrendValueAPI.cs - Gbtc
//
//  Copyright © 2016, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  09/13/2016 - Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using GSF;
using GSF.Collections;
using GSF.Data;
using GSF.Data.Model;
using GSF.Snap;
using GSF.Snap.Filters;
using GSF.Snap.Services;
using GSF.Snap.Services.Reader;
using GSF.Threading;
using openHistorian.Model;
using openHistorian.Snap;
using CancellationToken = GSF.Threading.CancellationToken;

namespace openHistorian.Adapters
{
    /// <summary>
    /// Defines an API for publishing <see cref="TrendValue"/> instances from the openHistorian.
    /// </summary>
    public static class TrendValueAPI
    {
        private static string s_instanceName;
        private static int s_portNumber;

        static TrendValueAPI()
        {
            LoadConnectionParameters();
        }

        /// <summary>
        /// Gets configured instance name for historian.
        /// </summary>
        public static string InstanceName => s_instanceName;

        /// <summary>
        /// Gets configured port number for historian.
        /// </summary>
        public static int PortNumber => s_portNumber;

        internal static void LoadConnectionParameters()
        {
            try
            {
                using (AdoDataConnection connection = new AdoDataConnection("systemSettings"))
                {
                    TableOperations<IaonOutputAdapter> operations = new TableOperations<IaonOutputAdapter>(connection);
                    IaonOutputAdapter record = operations.QueryRecords(limit: 1, restriction: new RecordRestriction
                    {
                        FilterExpression = "TypeName = 'openHistorian.Adapters.LocalOutputAdapter'"
                    })
                    .FirstOrDefault();

                    if ((object)record == null)
                        throw new NullReferenceException("Primary openHistorian adapter instance not found.");

                    Dictionary<string, string> settings = record.ConnectionString.ParseKeyValuePairs();
                    string setting;

                    if (!settings.TryGetValue("port", out setting) || !int.TryParse(setting, out s_portNumber))
                        s_portNumber = Connection.DefaultHistorianPort;

                    if (!settings.TryGetValue("instanceName", out s_instanceName) || string.IsNullOrWhiteSpace(s_instanceName))
                        s_instanceName = record.AdapterName ?? "PPA";
                }
            }
            catch
            {
                s_instanceName = "PPA";
                s_portNumber = Connection.DefaultHistorianPort;
            }
        }

        /// <summary>
        /// Estimates a decent plot resolution for given time range.
        /// </summary>
        /// <param name="startTime">Start time of query.</param>
        /// <param name="stopTime">Stop time of query.</param>
        /// <returns>Plot resolution for given time range.</returns>
        public static Resolution EstimatePlotResolution(DateTime startTime, DateTime stopTime)
        {
            Resolution plotResolution = Resolution.Full;

            long span = (stopTime - startTime).Ticks;

            if (span > 0)
            {
                if (span <= Ticks.PerMinute)
                    plotResolution = Resolution.Full;
                else if (span <= Ticks.PerMinute * 5L)
                    plotResolution = Resolution.TenPerSecond;
                else if (span <= Ticks.PerMinute * 30L)
                    plotResolution = Resolution.EverySecond;
                else if (span <= Ticks.PerHour * 3L)
                    plotResolution = Resolution.Every10Seconds;
                else if (span <= Ticks.PerHour * 8L)
                    plotResolution = Resolution.Every30Seconds;
                else if (span <= Ticks.PerDay)
                    plotResolution = Resolution.EveryMinute;
                else if (span <= Ticks.PerDay * 7L)
                    plotResolution = Resolution.Every10Minutes;
                else if (span <= Ticks.PerDay * 21L)
                    plotResolution = Resolution.Every30Minutes;
                else // if (span <= Ticks.PerDay * 42L)
                    plotResolution = Resolution.EveryHour;
            }

            return plotResolution;
        }

        /// <summary>
        /// Read historian data from server.
        /// </summary>
        /// <param name="database">Client database to use for query.</param>
        /// <param name="startTime">Start time of query.</param>
        /// <param name="stopTime">Stop time of query.</param>
        /// <param name="measurementIDs">Measurement IDs to query - or <c>null</c> for all available points.</param>
        /// <param name="resolution">Resolution for data query.</param>
        /// <param name="seriesLimit">Maximum number of points per series.</param>
        /// <param name="forceLimit">Flag that determines if series limit should be strictly enforced.</param>
        /// <param name="cancellationToken">Cancellation token for query.</param>
        /// <returns>Enumeration of <see cref="TrendValue"/> instances read for time range.</returns>
        public static IEnumerable<TrendValue> GetHistorianData(ClientDatabaseBase<HistorianKey, HistorianValue> database, DateTime startTime, DateTime stopTime, ulong[] measurementIDs, Resolution resolution, int seriesLimit, bool forceLimit, ICancellationToken cancellationToken = null)
        {
            if ((object)cancellationToken == null)
                cancellationToken = new CancellationToken();

            TimeSpan resolutionInterval = resolution.GetInterval();
            SeekFilterBase<HistorianKey> timeFilter;
            MatchFilterBase<HistorianKey, HistorianValue> pointFilter = null;
            HistorianKey key = new HistorianKey();
            HistorianValue value = new HistorianValue();

            // Set data scan resolution
            if (resolution == Resolution.Full)
            {
                timeFilter = TimestampSeekFilter.CreateFromRange<HistorianKey>(startTime, stopTime);
            }
            else
            {
                BaselineTimeInterval interval = BaselineTimeInterval.Second;

                if (resolutionInterval.Ticks < Ticks.PerMinute)
                    interval = BaselineTimeInterval.Second;
                else if (resolutionInterval.Ticks < Ticks.PerHour)
                    interval = BaselineTimeInterval.Minute;
                else if (resolutionInterval.Ticks == Ticks.PerHour)
                    interval = BaselineTimeInterval.Hour;

                startTime = startTime.BaselinedTimestamp(interval);
                stopTime = stopTime.BaselinedTimestamp(interval);

                timeFilter = TimestampSeekFilter.CreateFromIntervalData<HistorianKey>(startTime, stopTime, resolutionInterval, new TimeSpan(TimeSpan.TicksPerMillisecond));
            }

            Dictionary<ulong, DataRow> metadata = LocalOutputAdapter.Instances[InstanceName].Measurements;

            // Setup point ID selections
            if ((object)measurementIDs != null)
                pointFilter = PointIdMatchFilter.CreateFromList<HistorianKey, HistorianValue>(measurementIDs);
            else
                measurementIDs = metadata.Keys.ToArray();

            // Start stream reader for the provided time window and selected points
            if ((object)database == null)
                yield break;

            Dictionary<ulong, long> pointCounts = new Dictionary<ulong, long>(measurementIDs.Length);
            Dictionary<ulong, long> intervals = new Dictionary<ulong, long>(measurementIDs.Length);
            Dictionary<ulong, ulong> lastTimes = new Dictionary<ulong, ulong>(measurementIDs.Length);
            double range = (stopTime - startTime).TotalSeconds;
            long estimatedPointCount = (long)(range / resolutionInterval.TotalSeconds.NotZero(1.0D));
            ulong pointID, timestamp, resolutionSpan = (ulong)resolutionInterval.Ticks, baseTicks = (ulong)UnixTimeTag.BaseTicks.Value;
            long pointCount;
            DataRow row;

            if (resolutionSpan <= 1UL)
                resolutionSpan = Ticks.PerSecond;

            if (seriesLimit < 1)
                seriesLimit = 1;

            // Estimate total measurement counts per point so decimation intervals for each series can be calculated
            foreach (ulong measurementID in measurementIDs)
            {
                if (resolution == Resolution.Full)
                    pointCounts[measurementID] = metadata.TryGetValue(measurementID, out row) ? (long)(int.Parse(row["FramesPerSecond"].ToString()) * range) : 2;
                else
                    pointCounts[measurementID] = estimatedPointCount;
            }

            foreach (ulong measurementID in pointCounts.Keys)
                intervals[measurementID] = (pointCounts[measurementID] / seriesLimit).NotZero(1L);

            lock (database)
            {
                TreeStream<HistorianKey, HistorianValue> stream = database.Read(SortedTreeEngineReaderOptions.Default, timeFilter, pointFilter);

                while (stream.Read(key, value) && !cancellationToken.IsCancelled)
                {
                    pointID = key.PointID;
                    timestamp = key.Timestamp;
                    pointCount = pointCounts[pointID];

                    if (pointCount++ % intervals[pointID] == 0 || (!forceLimit && timestamp - lastTimes.GetOrAdd(pointID, 0UL) > resolutionSpan))
                        yield return new TrendValue
                        {
                            ID = (long)pointID,
                            Timestamp = (timestamp - baseTicks) / (double)Ticks.PerMillisecond,
                            Value = value.AsSingle
                        };

                    pointCounts[pointID] = pointCount;
                    lastTimes[pointID] = timestamp;
                }
            }
        }
    }
}