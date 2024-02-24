using BleSend.Infrastructure;

using Cocona;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

using DeviceInformation = Windows.Devices.Enumeration.DeviceInformation;

namespace BleSend;

public partial class BluetoothService
{
	private const int MaxRetryCount = 5;

	private readonly BluetoothOptions _options;
	private readonly ILogger<BluetoothService> _logger;

	public BluetoothService(
		IOptions<BluetoothOptions> options,
		ILogger<BluetoothService> logger)
	{
		_options = options.Value;
		_logger = logger;
	}

	public async Task<BluetoothLEDevice> GetBluetoothDeviceAsync(string bluetoothAddress)
	{
		var nativeAddress = BluetoothAddress.Parse(bluetoothAddress);
		var device = await BluetoothLEDevice.FromBluetoothAddressAsync(nativeAddress);

		if (device != null)
		{
			LogDeviceFound(device.DeviceId, bluetoothAddress);
			return device;
		}

		LogBeginDiscovery(bluetoothAddress);
		var discoveryResult = await DiscoveryAsync(nativeAddress);
		device = await BluetoothLEDevice.FromIdAsync(discoveryResult.Id);
		if (device != null)
		{
			LogDeviceFound(device.DeviceId, bluetoothAddress);
			return device;
		}

		throw new CommandExitedException(WellKnownResultCodes.DeviceNotFound);
	}

	private async Task<DeviceInformation> DiscoveryAsync(ulong nativeAddress)
	{
		var resultTask = new TaskCompletionSource<DeviceInformation>(TaskCreationOptions.RunContinuationsAsynchronously);

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
			BluetoothLEDevice.GetDeviceSelectorFromBluetoothAddress(nativeAddress),
			requestedProperties,
			DeviceInformationKind.AssociationEndpoint
		);
		try
		{
			// Register event handlers before starting the watcher.
			deviceWatcher.Added += (sender, info) => { resultTask.SetResult(info); };
			deviceWatcher.Updated += (sender, info) =>
			{
				// updated must be not null or search won't be performed
			};


			deviceWatcher.Start();

			return await resultTask.Task.WaitAsync(_options.DiscoveryTimeout);
		}
		catch (OperationCanceledException ex)
		{
			throw new CommandExitedException(ex.Message, WellKnownResultCodes.DeviceNotFound);
		}
		catch (TimeoutException ex)
		{
			throw new CommandExitedException(ex.Message, WellKnownResultCodes.DeviceNotFound);
		}
		finally
		{
			deviceWatcher.Stop();
		}
	}

	public async Task<GattDeviceService> GetServiceAsync(BluetoothLEDevice device, Guid serviceId)
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
						LogServiceConnected(serviceId, tryCount);
					}
					else
					{
						candidateService.Dispose();
					}
				}
			}

			if (service == null)
			{
				if (tryCount > MaxRetryCount)
				{
					LogServiceConnectionFailed(serviceId);
					throw new CommandExitedException(WellKnownResultCodes.ServiceNotFound);
				}

				await Task.Delay(TimeSpan.FromMicroseconds(100));
			}

		}

		return service;
	}

	public async Task<GattCharacteristic> GetCharacteristicAsync(GattDeviceService service,
		Guid characteristicId)
	{
		GattCharacteristic? characteristic = null;
		int tryCount = 0;
		while (characteristic == null) //This is to make sure all characteristics are found.
		{
			tryCount++;
			var characteristics = await service.GetCharacteristicsForUuidAsync(characteristicId, BluetoothCacheMode.Uncached);
			if (characteristics.Status == GattCommunicationStatus.Success
				&& characteristics.Characteristics.FirstOrDefault(x => x.Uuid == characteristicId) is { } found)
			{
				characteristic = found;
				LogCharacteristicFound(characteristicId, tryCount);
			}
			else
			{
				if (tryCount > MaxRetryCount)
				{
					Console.WriteLine("Failed to connect to characteristic");
					throw new InvalidOperationException("Failed to connect to characteristic");
				}

				await Task.Delay(TimeSpan.FromMicroseconds(100));
			}
		}

		return characteristic;
	}

	[LoggerMessage(0, LogLevel.Debug, "Found device {deviceId} with address = {deviceAddress}.")]
	private partial void LogDeviceFound(string deviceId, string deviceAddress);

	[LoggerMessage(1, LogLevel.Information, "Device {deviceAddress} not found, begin discovery.")]
	private partial void LogBeginDiscovery(string deviceAddress);

	[LoggerMessage(2, LogLevel.Information, "Service {serviceId} connected in {attemptCount} tries")]
	private partial void LogServiceConnected(Guid serviceId, int attemptCount);

	[LoggerMessage(3, LogLevel.Error, "Failed to connect to service {serviceId}")]
	private partial void LogServiceConnectionFailed(Guid serviceId);

	[LoggerMessage(4, LogLevel.Information, "Characteristic {characteristicId} found in {attemptCount} tries")]
	private partial void LogCharacteristicFound(Guid characteristicId, int attemptCount);

	[LoggerMessage(5, LogLevel.Error, "Characteristic {characteristicId} not found.")]
	private partial void LogCharacteristicNotFound(Guid characteristicId);
}