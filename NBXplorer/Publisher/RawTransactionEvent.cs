
using NBitcoin;

namespace NBXplorer
{

	public class RawTransactionEvent
	{
		public RawTransactionEvent(Transaction transaction, NBXplorerNetwork network)
		{
			Transaction = transaction;
			Network = network;
		}

		public Transaction Transaction { get; set; }
		public NBXplorerNetwork Network { get; set; }
	}
}