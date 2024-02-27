using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace BleTools.Infrastructure;

internal static class BluetoothAddress
{
	public static ulong Parse(string address)
	{
		if (BitConverter.IsLittleEndian == false)
			throw new NotSupportedException("Big-endian environments are not supported.");

		var bytesRaw = PhysicalAddress.Parse(address).GetAddressBytes();
		if (bytesRaw.Length > sizeof(ulong))
			throw new ArgumentException($"Invalid address {address}", nameof(address));
		Array.Reverse(bytesRaw);

		Span<byte> target = stackalloc byte[sizeof(ulong)];
		bytesRaw.CopyTo(target);

		return MemoryMarshal.Read<ulong>(target);
	}

	public static string Format(ulong nativeAddress)
	{
		if (BitConverter.IsLittleEndian == false)
			throw new NotSupportedException("Big-endian environments are not supported.");

		Span<byte> addressSpan = stackalloc byte[sizeof(ulong)];
		MemoryMarshal.Write(addressSpan, nativeAddress);
		addressSpan.Reverse();

		// Skip trailing zero bytes and format rest to hex with ':' separator.
		var result = new StringBuilder();
		foreach (var byteValue in addressSpan)
		{
			if (result.Length == 0)
			{
				if (byteValue == 0) continue;
			}
			else
			{
				result.Append(':');
			}

			result.Append($"{byteValue:X2}");
		}

		return result.ToString();
	}
}