using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NBXplorer
{

	public class MillisecondEpochConverter : DateTimeConverterBase
	{
		private static readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var timestamp = (ulong) ((DateTime) value - _epoch).TotalMilliseconds;
			writer.WriteRawValue(timestamp.ToString());
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
			JsonSerializer serializer)
		{
			if (reader.Value == null)
			{
				return null;
			}

			var diff = (long) reader.Value;
			var dt = _epoch.AddMilliseconds(diff);
			return dt;
		}
	}
}