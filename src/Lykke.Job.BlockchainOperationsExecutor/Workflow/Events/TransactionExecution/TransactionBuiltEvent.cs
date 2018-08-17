﻿using System;
using MessagePack;

namespace Lykke.Job.BlockchainOperationsExecutor.Workflow.Events.TransactionExecution
{
    /// <summary>
    /// Blockchain transaction is built event
    /// </summary>
    [MessagePackObject(keyAsPropertyName: true)]
    public class TransactionBuiltEvent
    {
        /// <summary>
        /// Lykke unique transaction ID
        /// </summary>
        public Guid TransactionId { get; set; }

        /// <summary>
        /// Blockchain transaction context
        /// </summary>
        public string TransactionContext { get; set; }

        /// <summary>
        /// Source address context
        /// </summary>
        public string FromAddressContext { get; set; }
    }
}