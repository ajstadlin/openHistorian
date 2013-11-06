﻿//******************************************************************************************************
//  EncodedNodeScannerBase`2.cs - Gbtc
//
//  Copyright © 2013, Grid Protection Alliance.  All Rights Reserved.
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
//  5/7/2013 - Steven E. Chisholm
//       Generated original version of source code. 
//     
//******************************************************************************************************

using System;
using GSF.IO;

namespace openHistorian.Collections.Generic.TreeNodes
{
    /// <summary>
    /// Base class for reading from a node that is encoded and must be read sequentally through the node.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public abstract class EncodedNodeScannerBase<TKey, TValue>
        : TreeScannerBase<TKey, TValue>
        where TKey : class, ISortedTreeKey<TKey>, new()
        where TValue : class, ISortedTreeValue<TValue>, new()
    {
        private int m_nextOffset;
        private bool m_skipNextRead;
        TKey m_prevKey;
        TValue m_prevValue;

        protected EncodedNodeScannerBase(byte level, int blockSize, BinaryStreamBase stream, Func<TKey, byte, uint> lookupKey, byte version)
            : base(level, blockSize, stream, lookupKey, version)
        {
            m_prevKey = new TKey();
            m_prevValue = new TValue();
            m_skipNextRead = false;
        }

        /// <summary>
        /// Decodes the next record from the byte array into the provided key and value.
        /// </summary>
        /// <param name="stream">the start of the next record.</param>
        /// <param name="key">the key to write to.</param>
        /// <param name="value">the value to write to.</param>
        /// <returns></returns>
        protected abstract unsafe int DecodeRecord(byte* stream, TKey key, TValue value);

        /// <summary>
        /// Occurs when a new node has been reached and any encoded data that has been generated needs to be cleared.
        /// </summary>
        protected abstract void ResetEncoder();
        
        /// <summary>
        /// Using <see cref="TreeScannerBase{TKey,TValue}.Pointer"/> advance to the next KeyValue
        /// </summary>
        protected override void ReadNext()
        {
            if (m_skipNextRead)
            {
                m_skipNextRead = false;
                KeyMethods.Copy(m_prevKey, CurrentKey);
                ValueMethods.Copy(m_prevValue, CurrentValue);
            }
            else
            {
                InternalRead();
            }
        }

        /// <summary>
        /// Using <see cref="TreeScannerBase{TKey,TValue}.Pointer"/> advance to the search location of the provided <see cref="key"/>
        /// </summary>
        /// <param name="key">the key to advance to</param>
        protected override int FindKey(TKey key)
        {
            OnNoadReload();
            int nextReadIndex = 0;
            bool indexFound = false;

            //Find the first occurance where the key that 
            //is read is greater than or equal to the search key.
            //or the end of the stream is encountered.
            while (!indexFound && nextReadIndex < RecordCount)
            {
                nextReadIndex++;
                InternalRead();
                if (KeyMethods.IsGreaterThanOrEqualTo(CurrentKey, key))
                    indexFound = true;
            }

            if (indexFound)
            {
                m_skipNextRead = true;
                KeyMethods.Copy(CurrentKey, m_prevKey);
                ValueMethods.Copy(CurrentValue, m_prevValue);
                return nextReadIndex - 1;
            }
            else
            {
                m_skipNextRead = false;
                return nextReadIndex;
            }
        }

        /// <summary>
        /// Occurs when a node's data is reset.
        /// Derived classes can override this 
        /// method if fields need to be reset when a node is loaded.
        /// </summary>
        protected override void OnNoadReload()
        {
            m_nextOffset = 0;
            ResetEncoder();
        }

        /// <summary>
        /// Executes a read of the next set of data.
        /// </summary>
        /// <returns></returns>
        private unsafe void InternalRead()
        {
            m_nextOffset += DecodeRecord(Pointer + m_nextOffset, CurrentKey, CurrentValue);
        }
    }
}