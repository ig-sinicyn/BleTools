using BleTools.Infrastructure;
using BleTools.Models;

using Cocona;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

using DeviceInformation = Windows.Devices.Enumeration.DeviceInformation;

namespace BleTools;

public partial class BluetoothService
{
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
		//// Fast path

		var nativeAddress = BluetoothAddress.Parse(bluetoothAddress);
		var device = await BluetoothLEDevice.FromBluetoothAddressAsync(nativeAddress);

		if (device != null)
		{
			LogDeviceFound(device.GetDisplayName());
			return device;
		}

		//// Slow path

		LogBeginDeviceDiscovery(bluetoothAddress);
		try
		{
			var discoveryResult = await DiscoveryDeviceAsync(nativeAddress);
			device = await BluetoothLEDevice.FromIdAsync(discoveryResult.Id);
			if (device == null)
			{
				throw new CommandExitedException(WellKnownResultCodes.DeviceNotFound);
			}
		}
		catch (CommandExitedException)
		{
			LogDeviceNotFound(bluetoothAddress);
			throw;
		}

		LogDeviceFound(device.GetDisplayName());
		return device;
	}

	private async Task<DeviceInformation> DiscoveryDeviceAsync(ulong nativeAddress)
	{
		var discoveryTaskSource =
			new TaskCompletionSource<DeviceInformation>(TaskCreationOptions.RunContinuationsAsynchronously);

		var deviceWatcher = DeviceInformation.CreateWatcher(
			BluetoothLEDevice.GetDeviceSelectorFromBluetoothAddress(nativeAddress),
			BluetoothDeviceInformation.DeviceWatcherProperties,
			DeviceInformationKind.AssociationEndpoint);
		try
		{
			// Register event handlers before starting the watcher.
			// Updated callback must be not null or search won't be performed
			deviceWatcher.Added += (sender, info) => discoveryTaskSource.SetResult(info);
			deviceWatcher.Updated += (sender, info) => { };

			deviceWatcher.Start();

			return await discoveryTaskSource.Task.WaitAsync(_options.DeviceDiscoveryTimeout);
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

	public async IAsyncEnumerable<DeviceInformation> ScanBluetoothDevicesAsync(
		BluetoothDeviceFilter deviceFilter,
		[EnumeratorCancellation] CancellationToken cancellation)
	{
		LogBeginDeviceScan(deviceFilter);

		var channel = Channel.CreateUnbounded<DeviceInformation>();

		var deviceWatcher = DeviceInformation.CreateWatcher(
			deviceFilter.ToAqsFilter(),
			BluetoothDeviceInformation.ScanDeviceWatcherProperties,
			DeviceInformationKind.AssociationEndpoint);
		try
		{
			//// Populate channel part

			// Register event handlers before starting the watcher.
			// Updated callback must be not null or search won't be performed
			deviceWatcher.Added += (sender, info) => channel.Writer.TryWrite(info);
			deviceWatcher.Updated += (sender, info) => { };

			deviceWatcher.Start();

			//// Read channel part

			while (!cancellation.IsCancellationRequested)
			{
				DeviceInformation device;
				try
				{
					device = await channel.Reader.ReadAsync(cancellation);
				}
				catch (OperationCanceledException)
				{
					continue;
				}

				yield return device;
			}
		}
		finally
		{
			channel.Writer.Complete();
			deviceWatcher.Stop();
			LogCompleteDeviceScan();
		}
	}

	public async Task<GattDeviceService> GetServiceAsync(BluetoothLEDevice device, Guid serviceId,
		BluetoothCacheMode cacheMode = BluetoothCacheMode.Cached)
	{
		//// Fast path

		GattDeviceService? service = null;
		if (cacheMode != BluetoothCacheMode.Uncached)
		{
			try
			{
				service = device.GetGattService(serviceId);
			}
			catch (COMException)
			{
			}

			if (service != null)
			{
				LogServiceConnected(serviceId);
				return service;
			}
		}

		//// Slow path

		LogBeginServiceDiscovery(serviceId);
		var sw = Stopwatch.StartNew();
		while (service == null && sw.Elapsed < _options.MetadataRetrieveTimeout)
		{
			var listResult = await device.GetGattServicesAsync(cacheMode);
			if (listResult.Status == GattCommunicationStatus.Success)
			{
				foreach (var candidateService in listResult.Services)
				{
					if (candidateService.Uuid == serviceId)
					{
						service = candidateService;
						LogServiceConnected(serviceId);
					}
					else
					{
						candidateService.Dispose();
					}
				}
			}

			if (service == null) await Task.Delay(_options.MetadataPollingInterval);
		}

		if (service == null)
		{
			LogServiceConnectionFailed(serviceId);
			throw new CommandExitedException(WellKnownResultCodes.ServiceNotFound);
		}

		return service;
	}

	public async Task<GattCharacteristic> GetCharacteristicAsync(GattDeviceService service, Guid characteristicId,
		BluetoothCacheMode cacheMode = BluetoothCacheMode.Cached)
	{
		//// Fast path

		GattCharacteristic? characteristic = null;
		if (cacheMode != BluetoothCacheMode.Uncached)
		{
			characteristic = service.GetCharacteristics(characteristicId).SingleOrDefault();
			if (characteristic != null)
			{
				LogCharacteristicFound(characteristicId);
				return characteristic;
			}
		}

		//// Slow path

		LogBeginCharacteristicDiscovery(characteristicId);
		var sw = Stopwatch.StartNew();
		while (characteristic == null && sw.Elapsed < _options.MetadataRetrieveTimeout)
		{
			var listResult = await service.GetCharacteristicsForUuidAsync(characteristicId, cacheMode);
			if (listResult.Status == GattCommunicationStatus.Success
				&& listResult.Characteristics.FirstOrDefault(x => x.Uuid == characteristicId) is { } found)
			{
				characteristic = found;
				LogCharacteristicFound(characteristicId);
			}

			if (characteristic == null) await Task.Delay(_options.MetadataPollingInterval);
		}

		if (characteristic == null)
		{
			LogCharacteristicNotFound(characteristicId);
			throw new CommandExitedException(WellKnownResultCodes.CharacteristicNotFound);
		}

		return characteristic;
	}

	[LoggerMessage(0, LogLevel.Debug, "Found device {deviceName}.")]
	private partial void LogDeviceFound(string deviceName);

	[LoggerMessage(1, LogLevel.Information, "Device {deviceAddress} not found, begin discovery.")]
	private partial void LogBeginDeviceDiscovery(string deviceAddress);

	[LoggerMessage(2, LogLevel.Error, "Cannot connect to device {deviceAddress}.")]
	private partial void LogDeviceNotFound(string deviceAddress);

	[LoggerMessage(3, LogLevel.Debug, "Connected to service {serviceId}.")]
	private partial void LogServiceConnected(Guid serviceId);

	[LoggerMessage(4, LogLevel.Information, "Service {serviceId} not found, begin discovery.")]
	private partial void LogBeginServiceDiscovery(Guid serviceId);

	[LoggerMessage(5, LogLevel.Error, "Failed to connect to service {serviceId}")]
	private partial void LogServiceConnectionFailed(Guid serviceId);

	[LoggerMessage(6, LogLevel.Debug, "Found characteristic {characteristicId}.")]
	private partial void LogCharacteristicFound(Guid characteristicId);

	[LoggerMessage(7, LogLevel.Information, "Characteristic {characteristicId} not found, begin discovery.")]
	private partial void LogBeginCharacteristicDiscovery(Guid characteristicId);

	[LoggerMessage(8, LogLevel.Error, "Found characteristic {characteristicId}.")]
	private partial void LogCharacteristicNotFound(Guid characteristicId);

	[LoggerMessage(9, LogLevel.Information,
		"Begin scanning for Bluetooth devices (filter set to {deviceFilter}). Press Ctrl-C to stop the scanning.")]
	private partial void LogBeginDeviceScan(BluetoothDeviceFilter deviceFilter);

	[LoggerMessage(10, LogLevel.Information, "End scanning for Bluetooth devices.")]
	private partial void LogCompleteDeviceScan();
}