﻿using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Cqrs;
using Lykke.Job.BlockchainOperationsExecutor.Core;
using Lykke.Job.BlockchainOperationsExecutor.Core.Services.Blockchains;
using Lykke.Job.BlockchainOperationsExecutor.Workflow.Commands;
using Lykke.Job.BlockchainOperationsExecutor.Workflow.Events;

namespace Lykke.Job.BlockchainOperationsExecutor.Workflow.CommandHandlers
{
    [UsedImplicitly]
    public class BroadcastTransactionCommandsHandler
    {
        private readonly ILog _log;
        private readonly IBlockchainApiClientProvider _apiClientProvider;

        public BroadcastTransactionCommandsHandler(
            ILog log,
            IBlockchainApiClientProvider apiClientProvider)
        {
            _log = log;
            _apiClientProvider = apiClientProvider;
        }

        [UsedImplicitly]
        public async Task<CommandHandlingResult> Handle(BroadcastTransactionCommand command, IEventPublisher publisher)
        {
            var apiClient = _apiClientProvider.Get(command.BlockchainType);

            if (await apiClient.BroadcastTransactionAsync(command.OperationId, command.SignedTransaction))
            {
                _log.WriteInfo(nameof(BroadcastTransactionCommand), command.OperationId, "API said that transaction is already broadcasted");
            }

            ChaosKitty.Meow();

            publisher.PublishEvent(new TransactionBroadcastedEvent
            {
                OperationId = command.OperationId
            });

            return CommandHandlingResult.Ok();
        }
    }
}