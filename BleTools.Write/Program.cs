using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BleTools.Write
{
	internal class Program
	{
		private static ulong ParseAddress(string address)
		{
			if (BitConverter.IsLittleEndian == false)
				throw new NotImplementedException("Have no support for big-endian.");

			var bytesRaw = PhysicalAddress.Parse(address).GetAddressBytes();
			if (bytesRaw.Length > sizeof(ulong))
				throw new ArgumentException(nameof(address), $"Invalid address {address}");
			Array.Reverse(bytesRaw);

			Span<byte> target = stackalloc byte[sizeof(ulong)];
			bytesRaw.CopyTo(target);

			return MemoryMarshal.Read<ulong>(target);
		}

		public static IBuffer FormatString(string value, Encoding? encoding = null)
		{
			var writer = new DataWriter();
			writer.WriteBytes((encoding ?? Encoding.UTF8).GetBytes(value));
			return writer.DetachBuffer();
		}

		static async Task<int> Main(string[] args)
		{
			if (args.Length != 4)
			{
				Console.WriteLine("Requires {address} {service} {characteristic} {value}");
				return -1;
			}

			var address = ParseAddress(args[0]);
			var serviceId = Guid.Parse(args[1]);
			var characteristicId = Guid.Parse(args[2]);
			var value = args[3];

			using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);

			using var service = device.GetGattService(serviceId);
			service.Session.MaintainConnection = true;
			var characteristic = service.GetCharacteristics(characteristicId).Single();

			var buffer = FormatString(value);
			var writeResult = await characteristic.WriteValueAsync(buffer);
			if (writeResult != GattCommunicationStatus.Success)
			{
				Console.WriteLine($"Write failed: {writeResult}");
				return -1;
			}

			Console.WriteLine("Value written");
			return 0;
		}
	}


}
