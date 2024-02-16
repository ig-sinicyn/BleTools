using BleSend.Infrastructure;

using Cocona;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

using DeviceInformation = Windows.Devices.Enumeration.DeviceInformation;

namespace BleSend;

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

	[LoggerMessage(0, LogLevel.Debug, "Found device {deviceId} with address = {deviceAddress}.")]
	private partial void LogDeviceFound(string deviceId, string deviceAddress);

	[LoggerMessage(0, LogLevel.Debug, "Found device {deviceId} with address = {deviceAddress}.")]
	private partial void LogDeviceNotFound(string deviceId, string deviceAddress);

	[LoggerMessage(1, LogLevel.Information, "Device {deviceAddress} not found, begin discovery.")]
	private partial void LogBeginDiscovery(string deviceAddress);

	[LoggerMessage(2, LogLevel.Error, "No device with address = {deviceAddress}")]
	private partial void LogNotFound(string deviceAddress);
}