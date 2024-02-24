using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace BleTools.Full.Infrastructure;

internal static class BluetoothAddress
{
	public static ulong Parse(string address)
	{
		if (BitConverter.IsLittleEndian == false)
			throw new NotSupportedException("Big-endian environments are not supported.");

		var bytesRaw = PhysicalAddress.Parse(address).GetAddressBytes();
		if (bytesRaw.Length > sizeof(ulong))
			throw new ArgumentException(nameof(address), $"Invalid address {address}");
		Array.Reverse(bytesRaw);

		Span<byte> target = stackalloc byte[sizeof(ulong)];
		bytesRaw.CopyTo(target);

		return MemoryMarshal.Read<ulong>(target);
	}
}