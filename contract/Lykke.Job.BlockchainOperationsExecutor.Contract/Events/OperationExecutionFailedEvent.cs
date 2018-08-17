﻿using System;
using JetBrains.Annotations;
using MessagePack;

namespace Lykke.Job.BlockchainOperationsExecutor.Contract.Events
{
    /// <summary>
    /// Operation execution is failed
    /// </summary>
    [PublicAPI]
    [MessagePackObject]
    public class OperationExecutionFailedEvent
    {
        /// <summary>
        /// Lykke unique operation ID
        /// </summary>
        [Key(0)]
        public Guid OperationId { get; set; }

        /// <summary>
        /// Transaction ID
        /// </summary>
        [Key(1)]
        public Guid TransactionId { get; set; }

        /// <summary>
        /// Error description
        /// </summary>
        [Key(2)]
        public string Error { get; set; }

        /// <summary>
        /// Error code
        /// </summary>
        [Key(3)]
        public OperationExecutionErrorCode ErrorCode { get; set; }
    }
}
