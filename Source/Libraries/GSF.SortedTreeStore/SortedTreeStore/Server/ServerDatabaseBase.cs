﻿//******************************************************************************************************
//  ServerDatabaseBase.cs - Gbtc
//
//  Copyright © 2014, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  12/8/2012 - Steven E. Chisholm
//       Generated original version of source code. 
//
//******************************************************************************************************

using System;
using GSF.SortedTreeStore.Client;

namespace GSF.SortedTreeStore.Server
{
    /// <summary>
    /// Represents the server side of a single database.
    /// </summary>
    public abstract class ServerDatabaseBase : IDisposable
    {
        /// <summary>
        /// Gets basic information about the current Database.
        /// </summary>
        public abstract DatabaseInfo Info { get; }
        
        /// <summary>
        /// Forces a soft commit on the database. A soft commit 
        /// only commits data to memory. This allows other clients to read the data.
        /// While soft committed, this data could be lost during an unexpected shutdown.
        /// Soft commits usually occur within microseconds. 
        /// </summary>
        public abstract void SoftCommit();

        /// <summary>
        /// Forces a commit to the disk subsystem. Once this returns, the data will not
        /// be lost due to an application crash or unexpected shutdown.
        /// Hard commits can take 100ms or longer depending on how much data has to be committed. 
        /// This requires two consecutive hardware cache flushes.
        /// </summary>
        public abstract void HardCommit();

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public abstract void Dispose();

        /// <summary>
        /// Creates a <see cref="LocalClientDatabase{TKey,TValue}"/>
        /// </summary>
        /// <returns></returns>
        public abstract ClientDatabaseBase CreateLocalClientDatabase();
    }
}