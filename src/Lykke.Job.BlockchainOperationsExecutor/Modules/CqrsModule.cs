﻿using System.Collections.Generic;
using Autofac;
using Common.Log;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging;
using Inceptum.Messaging.Contract;
using Inceptum.Messaging.RabbitMq;
using Lykke.Cqrs;
using Lykke.Job.BlockchainOperationsExecutor.Contract;
using Lykke.Job.BlockchainOperationsExecutor.Contract.Commands;
using Lykke.Job.BlockchainOperationsExecutor.Contract.Events;
using Lykke.Job.BlockchainOperationsExecutor.Core;
using Lykke.Job.BlockchainOperationsExecutor.Settings.JobSettings;
using Lykke.Job.BlockchainOperationsExecutor.Workflow;
using Lykke.Job.BlockchainOperationsExecutor.Workflow.CommandHandlers;
using Lykke.Job.BlockchainOperationsExecutor.Workflow.Commands;
using Lykke.Job.BlockchainOperationsExecutor.Workflow.Sagas;
using Lykke.Messaging;

namespace Lykke.Job.BlockchainOperationsExecutor.Modules
{
    public class CqrsModule : Module
    {
        private static readonly string Self = BlockchainOperationsExecutorBoundedContext.Name;

        private readonly CqrsSettings _settings;
        private readonly ChaosSettings _chaosSettings;
        private readonly ILog _log;

        public CqrsModule(CqrsSettings settings, ChaosSettings chaosSettings, ILog log)
        {
            _settings = settings;
            _chaosSettings = chaosSettings;
            _log = log;
        }

        protected override void Load(ContainerBuilder builder)
        {
            if (_chaosSettings != null)
            {
                ChaosKitty.StateOfChaos = _chaosSettings.StateOfChaos;
            }

            builder.Register(context => new AutofacDependencyResolver(context)).As<IDependencyResolver>().SingleInstance();

            var rabbitMqSettings = new RabbitMQ.Client.ConnectionFactory
            {
                Uri = _settings.RabbitConnectionString
            };
            var messagingEngine = new MessagingEngine(_log,
                new TransportResolver(new Dictionary<string, TransportInfo>
                {
                    {
                        "RabbitMq",
                        new TransportInfo(rabbitMqSettings.Endpoint.ToString(), rabbitMqSettings.UserName,
                            rabbitMqSettings.Password, "None", "RabbitMq")
                    }
                }),                
                new RabbitMqTransportFactory());

            builder.Register(c => new RetryDelayProvider(
                    _settings.SourceAddressLockingRetryDelay,
                    _settings.WaitForTransactionRetryDelay))
                .AsSelf();

            // Sagas
            builder.RegisterType<OperationExecutionSaga>();

            // Command handlers
            builder.RegisterType<StartOperationExecutionCommandsHandler>();
            builder.RegisterType<BuildTransactionCommandsHandler>();
            builder.RegisterType<SignTransactionCommandsHandler>();
            builder.RegisterType<BroadcastTransactionCommandsHandler>();
            builder.RegisterType<WaitForTransactionEndingCommandsHandler>();
            builder.RegisterType<ReleaseSourceAddressLockCommandsHandler>();
            builder.RegisterType<ForgetBroadcastedTransactionCommandsHandler>();

            builder.Register(ctx => CreateEngine(ctx, messagingEngine))
                .As<ICqrsEngine>()
                .SingleInstance()
                .AutoActivate();
        }

        private CqrsEngine CreateEngine(IComponentContext ctx, IMessagingEngine messagingEngine)
        {
            var defaultRetryDelay = (long)_settings.RetryDelay.TotalMilliseconds;

            const string defaultPipeline = "commands";
            const string defaultRoute = "self";

            return new CqrsEngine(
                _log,
                ctx.Resolve<IDependencyResolver>(),
                messagingEngine,
                new DefaultEndpointProvider(),
                true,
                Register.DefaultEndpointResolver(new RabbitMqConventionEndpointResolver(
                    "RabbitMq",
                    "messagepack",
                    environment: "lykke")),

                Register.BoundedContext(Self)
                    .FailedCommandRetryDelay(defaultRetryDelay)

                    .ListeningCommands(typeof(StartOperationExecutionCommand))
                    .On(defaultRoute)
                    .WithCommandsHandler<StartOperationExecutionCommandsHandler>()
                    .PublishingEvents(typeof(OperationExecutionStartedEvent))
                    .With(defaultPipeline)

                    .ListeningCommands(typeof(BuildTransactionCommand))
                    .On(defaultRoute)
                    .WithCommandsHandler<BuildTransactionCommandsHandler>()
                    .PublishingEvents(typeof(TransactionBuiltEvent))
                    .With(defaultPipeline)

                    .ListeningCommands(typeof(SignTransactionCommand))
                    .On(defaultRoute)
                    .WithCommandsHandler<SignTransactionCommandsHandler>()
                    .PublishingEvents(typeof(TransactionSignedEvent))
                    .With(defaultPipeline)

                    .ListeningCommands(typeof(BroadcastTransactionCommand))
                    .On(defaultRoute)
                    .WithCommandsHandler<BroadcastTransactionCommandsHandler>()
                    .PublishingEvents(typeof(TransactionBroadcastedEvent))
                    .With(defaultPipeline)

                    .ListeningCommands(typeof(WaitForTransactionEndingCommand))
                    .On(defaultRoute)
                    .WithCommandsHandler<WaitForTransactionEndingCommandsHandler>()
                    .PublishingEvents(
                        typeof(OperationExecutionCompletedEvent),
                        typeof(OperationExecutionFailedEvent))
                    .With(defaultPipeline)
                    
                    .ListeningCommands(typeof(ReleaseSourceAddressLockCommand))
                    .On(defaultRoute)
                    .WithCommandsHandler<ReleaseSourceAddressLockCommandsHandler>()
                    .PublishingEvents(typeof(SourceAddressLockReleasedEvent))
                    .With(defaultPipeline)

                    .ListeningCommands(typeof(ForgetBroadcastedTransactionCommand))
                    .On(defaultRoute)
                    .WithCommandsHandler<ForgetBroadcastedTransactionCommandsHandler>()
                    .PublishingEvents(typeof(BroadcastedTransactionForgottenEvent))
                    .With(defaultPipeline)

                    .ProcessingOptions(defaultRoute).MultiThreaded(8).QueueCapacity(1024),

                Register.Saga<OperationExecutionSaga>($"{Self}.saga")
                    .ListeningEvents(typeof(OperationExecutionStartedEvent))
                    .From(Self)
                    .On(defaultRoute)
                    .PublishingCommands(typeof(BuildTransactionCommand))
                    .To(Self)
                    .With(defaultPipeline)

                    .ListeningEvents(typeof(TransactionBuiltEvent))
                    .From(Self)
                    .On(defaultRoute)
                    .PublishingCommands(typeof(SignTransactionCommand))
                    .To(Self)
                    .With(defaultPipeline)

                    .ListeningEvents(typeof(TransactionSignedEvent))
                    .From(Self)
                    .On(defaultRoute)
                    .PublishingCommands(typeof(BroadcastTransactionCommand))
                    .To(Self)
                    .With(defaultPipeline)

                    .ListeningEvents(typeof(TransactionBroadcastedEvent))
                    .From(Self)
                    .On(defaultRoute)
                    .PublishingCommands(typeof(WaitForTransactionEndingCommand))
                    .To(Self)
                    .With(defaultPipeline)

                    .ListeningEvents(
                        typeof(OperationExecutionCompletedEvent),
                        typeof(OperationExecutionFailedEvent))
                    .From(Self)
                    .On(defaultRoute)
                    .PublishingCommands(typeof(ReleaseSourceAddressLockCommand))
                    .To(Self)
                    .With(defaultPipeline)

                    .ListeningEvents(typeof(SourceAddressLockReleasedEvent))
                    .From(Self)
                    .On(defaultRoute)
                    .PublishingCommands(typeof(ForgetBroadcastedTransactionCommand))
                    .To(Self)
                    .With(defaultPipeline)

                    .ListeningEvents(typeof(BroadcastedTransactionForgottenEvent))
                    .From(Self)
                    .On(defaultRoute));
        }
    }
}
