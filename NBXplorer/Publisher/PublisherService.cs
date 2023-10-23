using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBXplorer.Configuration;
using NBXplorer.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NBitcoin.Protocol;
using NBXplorer.Backends.DBTrie;
using NetMQ;
using NetMQ.Sockets;

namespace NBXplorer
{

	public class PublisherService : EventHostedServiceBase
	{
		protected override string ServiceName => "Publisher";
		
		private NBXplorerNetworkProvider _networkProvider;
		private Dictionary<string, NBXplorerNetwork> networkMap = new Dictionary<string, NBXplorerNetwork>();
		private ILogger Logger = null;
		private int ZmqPubPort = 0;
		private int ZmqSendHighWatermark = 0;
		public PublisherService(IConfiguration configuration, 
			ILoggerFactory loggerFactory,
			NBXplorerNetworkProvider networkProvider,
			EventAggregator eventAggregator) : base(eventAggregator)
		{
			_networkProvider = networkProvider;
			var supportedChains = configuration.GetOrDefault<string>("chains", "btc")
				.Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(t => t.ToUpperInvariant());

			ZmqPubPort = configuration.GetOrDefault<int>("zmq_pub_port", 2000);
			ZmqSendHighWatermark = configuration.GetOrDefault<int>("zmq_pub_port", 50000);

			foreach (var chain in supportedChains)
			{
				var network = networkProvider.GetFromCryptoCode(chain);
				if (network != null)
				{
					networkMap.Add(chain, network);
				}
			}
			Logger = loggerFactory.CreateLogger("NBXplorer.Publisher");
			
		}
		
		
		protected override void SubscribeToEvents()
		{

			Subscribe<RawBlockEvent>();
			Subscribe<Models.NewBlockEvent>();
			Subscribe<RawTransactionEvent>();

		}

		private PublisherSocket PubSocket = null;

		public override async Task ProcessEvents(CancellationToken cancellationToken)
		{
			using (PubSocket = new PublisherSocket())
			{
				PubSocket.Options.SendHighWatermark = ZmqSendHighWatermark;
				var address = $"tcp://*:{ZmqPubPort}";
				PubSocket.Bind(address);
				Logs.Explorer.LogInformation($"Data publisher bind at {address}, with HWM: {ZmqSendHighWatermark}");
				while (await EventsChannel.Reader.WaitToReadAsync(cancellationToken))
				{
					if (EventsChannel.Reader.TryRead(out var evt))
					{
						try
						{
							await ProcessEvent(evt, cancellationToken);
						}
						catch when (cancellationToken.IsCancellationRequested)
						{
							throw;
						}
						catch (Exception ex)
						{
							Logs.Explorer.LogError(ex, $"Unhandled exception in {this.GetType().Name}");
						}
					}
				}
			}
		}
		
		protected override Task ProcessEvent(object evt, CancellationToken cancellationToken)
		{
			//区块事件
			if (evt is RawBlockEvent rawBlockEvent)
			{
				if (networkMap.TryGetValue(rawBlockEvent.Network.CryptoCode, out var netowork))
				{
					var block = new ChainBlockMessage(netowork, rawBlockEvent);
					var msg = $"NETWORK_{netowork.CryptoCode.ToUpper()}|BLOCK||{JsonConvert.SerializeObject(block)}";
					PubSocket.SendFrame(msg);
					Logger.LogInformation($"block: {block.BlockHash} ({block.BlockHeight})");

					foreach (var item in rawBlockEvent.Block.Transactions)
					{
						int i = 0;
						foreach (var output in item.Outputs)
						{
							var txn = new ChainTransactionMessage(netowork, item, output, i);
							txn.BlockHash = block.BlockHash;
							txn.BlockHeight = block.BlockHeight;
							var msgTxn =
								$"NETWORK_{netowork.CryptoCode.ToUpper()}|TRANSACTION|{netowork.CryptoCode}|{JsonConvert.SerializeObject(txn)}";
							PubSocket.SendFrame(msgTxn);
							i++;
						}
						//Logger.LogInformation($"txn: {item.GetHash()?.ToString()} - {block.BlockHeight}");
					}
				}
			}
			else if (evt is RawTransactionEvent transactionEvent)
			{
				if (networkMap.TryGetValue(transactionEvent.Network.CryptoCode, out var netowrk))
				{
					int i = 0;
					foreach (var output in transactionEvent.Transaction.Outputs)
					{
						var txn = new ChainTransactionMessage(netowrk, transactionEvent.Transaction, output, i);
						var msg =
							$"NETWORK_{netowrk.CryptoCode.ToUpper()}|TRANSACTION|{netowrk.CryptoCode}|{JsonConvert.SerializeObject(txn)}";
						PubSocket.SendFrame(msg);
						i++;
					}
					//Logger.LogInformation($"txn: {transactionEvent.Transaction.GetHash()?.ToString()}");
				}
				
			}
			return Task.CompletedTask;
		}
	}
}