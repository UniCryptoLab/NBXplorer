using System;
using NBitcoin;
using NBitcoin.Protocol.Behaviors;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;


namespace NBXplorer
{
	public class ChainBlockMessage
	{
		public ChainBlockMessage(NBXplorerNetwork network, SlimChainedBlock block, Block rawBlock)
		{
			this.Network = $"NETWORK_{network.CryptoCode.ToUpper()}";
			this.BlockHeight = (ulong) block.Height;
			this.BlockHash = block.Hash.ToString();
			this.ParentBlockHash = block.Previous.ToString();
			this.BlockTime = rawBlock.Header.BlockTime.DateTime;
		}
		
		/// <summary>
		/// 区块链代码
		/// </summary>
		[JsonProperty("network")]
		public string Network { get; set; }

		/// <summary>
		/// 区块高度
		/// </summary>
		[JsonProperty("block_height")]
		public ulong BlockHeight { get; set; }

		/// <summary>
		/// 区块Id
		/// </summary>
		[JsonProperty("block_hash")]
		public string BlockHash { get; set; }

		/// <summary>
		/// Transaction Hash
		/// </summary>
		[JsonProperty("parent_block_hash")]
		public string ParentBlockHash { get; set; }

		/// <summary>
		/// 时间戳
		/// </summary>
		[JsonProperty("block_time")]
		[JsonConverter(typeof(MillisecondEpochConverter))]
		public DateTime BlockTime { get; set; }
	}
}