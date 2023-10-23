using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBXplorer.Configuration;
using NBXplorer.Logging;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NBitcoin;
using NBXplorer.Backends.DBTrie;
using NetMQ;
using NetMQ.Sockets;

namespace NBXplorer
{

	public class PublisherService : EventHostedServiceBase
	{
		protected override string ServiceName => "Publisher";

		private NBXplorerNetworkProvider _networkProvider;
		private ChainProvider _chainProvider;
		private ILogger Logger = null;
		private int ZmqPubPort = 0;
		private int ZmqSendHighWatermark = 0;
		private string PubChain = null;
		private NBXplorerNetwork Network = null;
		private SlimChain Chain = null;

		public PublisherService(IConfiguration configuration,
			ILoggerFactory loggerFactory,
			NBXplorerNetworkProvider networkProvider,
			ChainProvider chains,
			EventAggregator eventAggregator) : base(eventAggregator)
		{
			_networkProvider = networkProvider;
			_chainProvider = chains;
			var supportedChains = configuration.GetOrDefault<string>("chains", "btc")
				.Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(t => t.ToUpperInvariant());

			ZmqPubPort = configuration.GetOrDefault<int>("zmq_pub_port", 2000);
			ZmqSendHighWatermark = configuration.GetOrDefault<int>("zmq_pub_port", 50000);

			PubChain = supportedChains.FirstOrDefault();
			if (string.IsNullOrEmpty(PubChain))
			{
				throw new Exception("Please set chains in settings.config");
			}

			Network = _networkProvider.GetFromCryptoCode(PubChain);
			if (this.Network == null)
			{
				throw new Exception($"Invalid Network code: {this.PubChain}");
			}

			Chain = _chainProvider.GetChain(this.Network);
			if (this.Chain == null)
			{
				throw new Exception($"Invalid Chain code: {this.PubChain}");
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

				var slimBlockHeader = Chain.GetBlock(rawBlockEvent.Block.GetHash());
				var block = new ChainBlockMessage(this.Network, slimBlockHeader, rawBlockEvent.Block);
				var msg = $"NETWORK_{this.Network.CryptoCode.ToUpper()}|BLOCK||{JsonConvert.SerializeObject(block)}";
				PubSocket.SendFrame(msg);
				Logger.LogInformation($"block: {block.BlockHash} ({block.BlockHeight})");

				foreach (var item in rawBlockEvent.Block.Transactions)
				{
					int i = 0;
					foreach (var output in item.Outputs)
					{
						var txn = new ChainTransactionMessage(this.Network, item, output, i);

						txn.BlockHash = slimBlockHeader.Hash.ToString();
						txn.BlockHeight = (ulong) slimBlockHeader.Height;
						var msgTxn =
							$"NETWORK_{this.Network.CryptoCode.ToUpper()}|TRANSACTION|{this.Network.CryptoCode}|{JsonConvert.SerializeObject(txn)}";
						PubSocket.SendFrame(msgTxn);
						i++;
					}
					//Logger.LogInformation($"txn: {item.GetHash()?.ToString()} - {block.BlockHeight}");
				}

			}
			else if (evt is RawTransactionEvent transactionEvent)
			{

				int i = 0;
				foreach (var output in transactionEvent.Transaction.Outputs)
				{
					var txn = new ChainTransactionMessage(this.Network, transactionEvent.Transaction, output, i);
					var msg =
						$"NETWORK_{this.Network.CryptoCode.ToUpper()}|TRANSACTION|{this.Network.CryptoCode}|{JsonConvert.SerializeObject(txn)}";
					PubSocket.SendFrame(msg);
					i++;
				}
				//Logger.LogInformation($"txn: {transactionEvent.Transaction.GetHash()?.ToString()}");
			}

			return Task.CompletedTask;
		}
	}
}