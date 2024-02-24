using System.Text;

using Windows.Storage.Streams;

namespace BleTools.Full.Infrastructure;

public static class CharacteristicValue
{
	public static string AsString(this IBuffer? buffer, Encoding? encoding = null)
	{
		var reader = DataReader.FromBuffer(buffer);
		var input = new byte[reader.UnconsumedBufferLength];
		reader.ReadBytes(input);
		return (encoding ?? Encoding.UTF8).GetString(input);
	}

	public static IBuffer FromString(string value, Encoding? encoding = null)
	{
		var writer = new DataWriter();
		writer.WriteBytes((encoding ?? Encoding.UTF8).GetBytes(value));
		return writer.DetachBuffer();
	}
}