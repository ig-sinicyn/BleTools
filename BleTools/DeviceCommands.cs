using BleTools.Infrastructure;
using BleTools.Models;

using Cocona;
using Cocona.Application;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;

using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BleTools;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal partial class DeviceCommands
{
	private readonly BluetoothService _bluetoothService;
	private readonly ICoconaAppContextAccessor _contextAccessor;
	private readonly ILogger<DeviceCommands> _logger;

	public DeviceCommands(
		BluetoothService bluetoothService,
		ICoconaAppContextAccessor contextAccessor,
		ILogger<DeviceCommands> logger)
	{
		_bluetoothService = bluetoothService;
		_contextAccessor = contextAccessor;
		_logger = logger;
	}

	[Command("scan", Description = "Scans for available Bluetooth devices")]
	public async Task ScanAsync(
		[Option(
			"filter",
			new[] { 'f' },
			Description = "Device filter")]
		BluetoothDeviceFilter deviceFilter = BluetoothDeviceFilter.BluetoothLe)
	{
		var cancellation = _contextAccessor.Current!.CancellationToken;

		var observed = new HashSet<string>();
		await foreach (var device in _bluetoothService.ScanBluetoothDevicesAsync(deviceFilter, cancellation))
		{
			var deviceId = BluetoothDeviceId.FromId(device.Id);
			var deviceKind = deviceId.IsLowEnergyDevice ? "Bluetooth LE" : "Bluetooth Classic";

			if (observed.Add(device.Id))
			{
				LogDiscovered(device.GetDisplayName(), deviceKind);

				var summary = device.GetScanDeviceDescription();
				LogDiscoveredSummary(summary);
			}
			else
			{
				LogRediscovered(device.GetDisplayName(), deviceKind);
			}
		}
	}

	[Command("pair", Description = "Starts pairing for specified device (usually requires confirmation on the target device)")]
	public async Task PairAsync(
		[Argument(Description = "MAC address of the Bluetooth LE device")] string bluetoothAddress,
		[Option("force", new[] { 'f' }, Description = "Force pairing (unpair if already paired)")] bool force = false)
	{
		//// Find device

		var device = await _bluetoothService.GetBluetoothDeviceAsync(bluetoothAddress);

		try
		{
			//// Check pairing status

			if (device.DeviceInformation.Pairing.IsPaired)
			{
				if (force)
				{
					await UnpairCoreAsync(device);
					device.Dispose();
					device = await _bluetoothService.GetBluetoothDeviceAsync(bluetoothAddress);
				}
				else
				{
					LogAlreadyPaired(device.GetDisplayName());
					return;
				}
			}

			//// Trigger pairing

			var deviceName = device.GetDisplayName();
			LogBeginPairing(deviceName);

			var ceremoniesSelected = DevicePairingKinds.ProvidePin
				| DevicePairingKinds.ConfirmOnly
				| DevicePairingKinds.ConfirmPinMatch
				| DevicePairingKinds.DisplayPin;
			var protectionLevel = DevicePairingProtectionLevel.EncryptionAndAuthentication;
			var customPairing = device.DeviceInformation.Pairing.Custom;
			customPairing.PairingRequested += PairingRequestedHandler;
			try
			{
				var pairResult = await customPairing.PairAsync(ceremoniesSelected, protectionLevel);
				if (pairResult.Status != DevicePairingResultStatus.Paired)
				{
					LogPairingFailed(deviceName, pairResult.Status, pairResult.ProtectionLevelUsed);
					throw new CommandExitedException(WellKnownResultCodes.DevicePairingFailed);
				}

				LogPaired(deviceName, pairResult.ProtectionLevelUsed);
			}
			finally
			{
				customPairing.PairingRequested -= PairingRequestedHandler;
			}
		}
		finally
		{
			device.Dispose();
		}
	}

	[Command("unpair", Description = "Revokes pairing for specified device")]
	public async Task UnpairAsync([Argument(Description = "MAC address of the Bluetooth LE device")] string bluetoothAddress)
	{
		//// Find device

		using var device = await _bluetoothService.GetBluetoothDeviceAsync(bluetoothAddress);

		//// Trigger unpairing

		var deviceName = device.GetDisplayName();
		var pairing = device.DeviceInformation.Pairing;
		if (pairing.IsPaired == false)
		{
			LogAlreadyUnpaired(deviceName);
			return;
		}

		await UnpairCoreAsync(device);
	}

	private async Task UnpairCoreAsync(BluetoothLEDevice device)
	{
		var deviceName = device.GetDisplayName();
		LogBeginUnpairing(deviceName);

		var unpairResult = await device.DeviceInformation.Pairing.UnpairAsync();
		if (unpairResult.Status != DeviceUnpairingResultStatus.Unpaired)
		{
			LogUnpairingFailed(deviceName, unpairResult.Status);
			throw new CommandExitedException(WellKnownResultCodes.DeviceUnpairingFailed);
		}

		LogUnpaired(deviceName);
	}

	private void PairingRequestedHandler(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
	{
		args.Accept(args.Pin);
		LogAcceptPairing(args.DeviceInformation.GetDisplayName(), args.PairingKind);
	}

	[LoggerMessage(0, LogLevel.Information, "Device {deviceName} already paired.")]
	private partial void LogAlreadyPaired(string deviceName);

	[LoggerMessage(1, LogLevel.Debug, "Begin pairing for {deviceName}.")]
	private partial void LogBeginPairing(string deviceName);

	[LoggerMessage(2, LogLevel.Information, "Please confirm pairing on {deviceName}. Pairing accepted on this device ({pairingKind}).")]
	private partial void LogAcceptPairing(string deviceName, DevicePairingKinds pairingKind);

	[LoggerMessage(3, LogLevel.Error, "Device {deviceName} pairing failed ({pairingStatus}). Requested protection level: {protectionLevel}.")]
	private partial void LogPairingFailed(string deviceName, DevicePairingResultStatus pairingStatus, DevicePairingProtectionLevel protectionLevel);

	[LoggerMessage(4, LogLevel.Information, "Device {deviceName} pairing complete. Protection level: {protectionLevel}.")]
	private partial void LogPaired(string deviceName, DevicePairingProtectionLevel protectionLevel);

	[LoggerMessage(5, LogLevel.Information, "Device {deviceName} not paired.")]
	private partial void LogAlreadyUnpaired(string deviceName);

	[LoggerMessage(6, LogLevel.Debug, "Begin unpairing {deviceName}.")]
	private partial void LogBeginUnpairing(string deviceName);

	[LoggerMessage(7, LogLevel.Error, "Device {deviceName} unpairing failed ({pairingStatus}).")]
	private partial void LogUnpairingFailed(string deviceName, DeviceUnpairingResultStatus pairingStatus);

	[LoggerMessage(8, LogLevel.Information, "Device {deviceName} unpairing complete.")]
	private partial void LogUnpaired(string deviceName);

	[LoggerMessage(9, LogLevel.Information, "* {deviceKind} device {deviceName} discovered:")]
	private partial void LogDiscovered(string deviceName, string deviceKind);

	[LoggerMessage(10, LogLevel.Information, "   - {deviceSummary};")]
	private partial void LogDiscoveredSummary(string deviceSummary);

	[LoggerMessage(11, LogLevel.Information, "* {deviceKind} device {deviceName} discovered (already seen);")]
	private partial void LogRediscovered(string deviceName, string deviceKind);

}