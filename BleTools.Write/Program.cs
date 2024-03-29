﻿using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BleTools.Write
{
	internal static class Program
	{
		public static async Task<int> Main(string[] args)
		{
			if (args.Length != 4)
			{
				Console.WriteLine(@"Usage: BleTools.Write bluetooth-address service characteristic value

Writes GATT service characteristic

Arguments:
  0: bluetooth-address    MAC address of the Bluetooth LE device (Required)
  1: service              GATT service UUID (Required)
  2: characteristic       GATT service characteristic UUID (Required)
  3: value                The new characteristic value (passed as UTF-8 string) (Required)

Options:
  -h, --help     Show help message");
				return -1;
			}

			var bluetoothAddress = args[0];
			var nativeAddress = ParseAddress(bluetoothAddress);
			var serviceId = Guid.Parse(args[1]);
			var characteristicId = Guid.Parse(args[2]);
			var value = args[3];

			using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(nativeAddress);
			var connectionParameters = BluetoothLEPreferredConnectionParameters.ThroughputOptimized;
			device.RequestPreferredConnectionParameters(connectionParameters);

			using var service = device.GetGattService(serviceId);
			service.Session.MaintainConnection = true;
			var characteristic = service.GetCharacteristics(characteristicId).Single();

			var buffer = FormatString(value);
			var writeResult = await characteristic.WriteValueAsync(buffer);

			if (writeResult != GattCommunicationStatus.Success)
			{
				Console.WriteLine($"Write failed: {writeResult}. Service / characteristic {serviceId} / {characteristicId} (device {bluetoothAddress}).");
				return -1;
			}

			Console.WriteLine($"Value '{value}' written to service / characteristic {serviceId} / {characteristicId} (device {bluetoothAddress}).");
			return 0;
		}

		private static ulong ParseAddress(string address)
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

		private static IBuffer FormatString(string value, Encoding? encoding = null)
		{
			var writer = new DataWriter();
			writer.WriteBytes((encoding ?? Encoding.UTF8).GetBytes(value));
			return writer.DetachBuffer();
		}
	}
}
