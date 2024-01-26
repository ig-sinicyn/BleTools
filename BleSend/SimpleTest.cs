using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using BleSend.Infrastructure;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Devices.Sms;
using Windows.Devices.Enumeration;
using System.Collections.Concurrent;
using System;

namespace BleSend;

public static class SimpleTest
{
	private const string deviceMac = "DC:A6:32:60:C9:56";
	private static readonly Guid _serviceId = new Guid("00000000-f813-4ae9-9174-6efbee940ae2");
	private static readonly Guid _characteristicUnsafe = new Guid("00000001-f813-4ae9-9174-6efbee940ae2");
	private static readonly Guid _characteristicSignedRequired = new Guid("00000002-f813-4ae9-9174-6efbee940ae2");
	private static readonly Guid _characteristicFullRequired = new Guid("00000003-f813-4ae9-9174-6efbee940ae2");

	public static async Task<string> DiscoverDeviceIdAsync()
	{
		var bluetoothAddress = BluetoothAddress.Parse(deviceMac);
		var selector = BluetoothLEDevice.GetDeviceSelectorFromBluetoothAddress(bluetoothAddress);

		var device = await DiscoverAsync(selector, _ => true);

		return device.Id;
	}

	public static async Task PairAsync(string deviceId)
	{
		using var device = await BluetoothLEDevice.FromIdAsync(deviceId);

		var pairing = device.DeviceInformation.Pairing;
		if (pairing.IsPaired)
		{
			Console.WriteLine($"{device.Name} paired");
			return;
		}

		var customPairing = pairing.Custom;
		var ceremoniesSelected = DevicePairingKinds.ProvidePin
			| DevicePairingKinds.ConfirmOnly
			| DevicePairingKinds.ConfirmPinMatch
			| DevicePairingKinds.DisplayPin;
		var protectionLevel = DevicePairingProtectionLevel.EncryptionAndAuthentication;
		customPairing.PairingRequested += PairingRequestedHandler;
		var pairResult = await customPairing.PairAsync(
			ceremoniesSelected,
			protectionLevel);

		Console.WriteLine($"Pairing result: {pairResult.Status}");
	}


	private static void PairingRequestedHandler(DeviceInformationCustomPairing sender,
		DevicePairingRequestedEventArgs args)
	{
		args.Accept(args.Pin);
		Console.WriteLine("Wait to approve");
	}

	public static async Task TestServiceAsync(string deviceId)
	{
		using var device = await BluetoothLEDevice.FromIdAsync(deviceId);
		Console.WriteLine($"Get {device.Name} device");


		using var service = await GetServiceAsync(device, _serviceId);
		Console.WriteLine($"Get {service.Uuid} service");
		service.Session.MaintainConnection = true;

		await ReadCharacteristicValueAsync(service, _characteristicUnsafe);
		await ReadCharacteristicValueAsync(service, _characteristicSignedRequired);
		await ReadCharacteristicValueAsync(service, _characteristicFullRequired);

		await WriteCharacteristicValueAsync(service, _characteristicUnsafe);
		await WriteCharacteristicValueAsync(service, _characteristicSignedRequired);
		await WriteCharacteristicValueAsync(service, _characteristicFullRequired);

		service.Session.MaintainConnection = false;
		service.Dispose();
	}



	private static async Task<GattDeviceService> GetServiceAsync(BluetoothLEDevice device, Guid serviceId)
	{
		GattDeviceService? service = null;
		int tryCount = 0;
		while (service == null) //This is to make sure all services are found.
		{
			tryCount++;
			var candidates = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
			if (candidates.Status == GattCommunicationStatus.Success)
			{
				foreach (var candidateService in candidates.Services)
				{
					if (candidateService.Uuid == serviceId)
					{
						service = candidateService;
						Console.WriteLine("Service connected in " + tryCount + " tries");
						break;
					}
					else
					{
						candidateService.Dispose();
					}
				}
			}

			if (service == null && tryCount > 5) //make this larger if failed
			{
				Console.WriteLine("Failed to connect to service");
				throw new InvalidComObjectException("Failed to connect to service");
			}

			await Task.Delay(TimeSpan.FromMicroseconds(100));
		}

		return service;
	}

	private static async Task<GattCharacteristic> GetCharacteristicAsync(GattDeviceService service,
		Guid characteristicId)
	{
		GattCharacteristic? characteristic = null;
		int tryCount = 0;
		while (characteristic == null) //This is to make sure all characteristics are found.
		{
			tryCount++;
			var characteristics = await service.GetCharacteristicsAsync();
			if (characteristics.Status == GattCommunicationStatus.Success
				&& characteristics.Characteristics.FirstOrDefault(x => x.Uuid == characteristicId) is { } found)
			{
				characteristic = found;
				Console.WriteLine("Characteristic connected in " + tryCount + " tries");
			}
			else if (tryCount > 5) //make this larger if failed
			{
				Console.WriteLine("Failed to connect to characteristic");
				throw new InvalidComObjectException("Failed to connect to characteristic");
			}

			await Task.Delay(TimeSpan.FromMicroseconds(100));
		}

		return characteristic;
	}

	private static async Task ReadCharacteristicValueAsync(GattDeviceService service, Guid characteristicId)
	{
		var ch = await GetCharacteristicAsync(service, characteristicId);

		var readResult = await ch.ReadValueAsync();

		if (readResult.Status == GattCommunicationStatus.Success)
			Console.WriteLine($" * {characteristicId}: {readResult.Value.AsString()}");
		else
			Console.WriteLine($" * {characteristicId}: Fail ({readResult.Status})");
	}

	private static async Task WriteCharacteristicValueAsync(GattDeviceService service, Guid characteristicId)
	{
		var ch = await GetCharacteristicAsync(service, characteristicId);

		var newValue = DateTimeOffset.UtcNow.ToString();
		var formattedValue = CharacteristicValue.FromString(newValue);

		try
		{
			var writeResult = await ch.WriteValueAsync(formattedValue);

			if (writeResult == GattCommunicationStatus.Success)
				Console.WriteLine($" * {characteristicId} => {newValue}");
			else
				Console.WriteLine($" * {characteristicId}: Write fail ({writeResult})");
		}
		catch (COMException ex)
		{
			Console.WriteLine($" * {characteristicId}: Write fail ({ex.ErrorCode:X})");
		}
	}

	private static async Task<DeviceInformation> DiscoverAsync(string customSelector, Func<DeviceInformation, bool> filter)
	{
		var resultTask =
			new TaskCompletionSource<DeviceInformation>(TaskCreationOptions.RunContinuationsAsynchronously);
		string[] requestedProperties =
		{
			"System.Devices.DevObjectType",
			"System.Devices.Aep.DeviceAddress",
			"System.Devices.Aep.IsConnected",
			"System.Devices.Aep.IsPaired",
			"System.Devices.Aep.Bluetooth.Le.IsConnectable",
			"System.Devices.Aep.Bluetooth.IssueInquiry"
		};

		var deviceWatcher = DeviceInformation.CreateWatcher(
			customSelector,
			requestedProperties,
			DeviceInformationKind.AssociationEndpoint
		);
		try
		{
			// Register event handlers before starting the watcher.
			deviceWatcher.Added += (sender, info) =>
			{
				Console.WriteLine($"Observed {info.Id}: {info.Name ?? "(no name)"}");
				if (filter(info))
				{
					resultTask.SetResult(info);
				}
			};

			deviceWatcher.Updated += (sender, info) =>
			{
				// updated must be not null or search won't be performed
			};


			deviceWatcher.Start();
			Console.WriteLine("Begin scan...");

			var found = await resultTask.Task;
			Console.WriteLine($"Found {found.Id}");

			return found;
		}
		finally
		{
			deviceWatcher.Stop();
		}
	}
}