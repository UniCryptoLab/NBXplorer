using System;
using NBitcoin;
using Newtonsoft.Json;

namespace NBXplorer
{
	public class ChainTransactionMessage
	{
		public ChainTransactionMessage(NBXplorerNetwork network, Transaction txn, TxOut output, int index)
		{
			this.Network = $"NETWORK_{network.CryptoCode}";
			this.Hash = txn.GetHash()?.ToString();
			this.Symbol = network.CryptoCode;
			this.To = output.ScriptPubKey.GetDestinationAddress(network.NBitcoinNetwork)?.ToString();
			this.Amount = output.Value.ToDecimal(MoneyUnit.BTC);
			this.TxnTime = DateTime.UtcNow;
			this.TransferId = $"v3.{this.Network}.{this.Hash}.{index}";
		}
		
		/// <summary>
		/// 区块链网络类型
		/// </summary>
		[JsonProperty("network")]
		public string Network { get; set; }

		/// <summary>
		/// 区块高度
		/// </summary>
		[JsonProperty("block_height")]
		public ulong? BlockHeight { get; set; }

		/// <summary>
		/// 区块Id
		/// </summary>
		[JsonProperty("block_hash")]
		public string BlockHash { get; set; }

		/// <summary>
		/// Transaction Hash
		/// </summary>
		[JsonProperty("hash")]
		public string Hash { get; set; }

		/// <summary>
		/// 转账Id
		/// </summary>
		[JsonProperty("transferId")]
		public string TransferId { get; set; }


		/// <summary>
		/// 合约地址
		/// </summary>
		[JsonProperty("contract_address")]
		public string ContractAddress { get; set; }

		/// <summary>
		/// 货币代码
		/// </summary>
		[JsonProperty("symbol")]
		public string Symbol { get; set; }

		/// <summary>
		/// 来源地址
		/// </summary>
		[JsonProperty("from")]
		public string From { get; set; }

		/// <summary>
		/// 目标地址
		/// </summary>
		[JsonProperty("to")]
		public string To { get; set; }

		/// <summary>
		/// 支付金额
		/// </summary>
		[JsonProperty("amount")]
		public decimal Amount { get; set; }


		/// <summary>
		/// 时间戳
		/// </summary>
		[JsonProperty("txn_time")]
		[JsonConverter(typeof(MillisecondEpochConverter))]
		public DateTime TxnTime { get; set; }
        
		/// <summary>
		/// 是否是外部钱包
		/// </summary>
		[JsonProperty("is_external")]
		public bool IsExternal { get; set; }
	}
}