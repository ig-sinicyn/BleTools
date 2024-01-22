using BleSend.Infrastructure;

using Cocona;

using Microsoft.Extensions.Logging;

using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using JetBrains.Annotations;

namespace BleSend;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal partial class PairCommands
{
	private readonly ILogger<PairCommands> _logger;

	public PairCommands(ILogger<PairCommands> logger)
	{
		_logger = logger;
	}

	[Command("pair", Description = "Starts pairing for specified device (usually requires confirmation on the target device)")]
	public async Task PairAsync(
		[Argument] string bluetoothAddress,
		[Option("force", new[] { 'f' }, Description = "Force pairing")] bool force = false)
	{
		//// Find device

		var device = await GetDeviceAsync(bluetoothAddress);

		//// Trigger pairing

		var deviceName = device.Name;
		var pairing = device.DeviceInformation.Pairing;
		if (pairing.IsPaired)
		{
			if (force)
			{
				await UnpairCoreAsync(pairing, deviceName);
				device = await GetDeviceAsync(bluetoothAddress);
				deviceName = device.Name;
				pairing = device.DeviceInformation.Pairing;

			}
			else
			{
				LogAlreadyPaired(deviceName);
				return;
			}
		}

		LogBeginPairing(deviceName);

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

		//// Handle pairing status

		if (pairResult.Status != DevicePairingResultStatus.Paired)
		{
			LogPairingFailed(deviceName, pairResult.Status, pairResult.ProtectionLevelUsed);
			throw new CommandExitedException(WellKnownResultCodes.DevicePairingFailed);
		}

		LogPaired(deviceName, pairResult.ProtectionLevelUsed);
	}

	[Command("unpair", Description = "Breaks pairing for specified device")]
	public async Task UnpairAsync([Argument] string bluetoothAddress)
	{
		//// Find device

		using var device = await GetDeviceAsync(bluetoothAddress);

		//// Trigger unpairing

		var deviceName = device.Name;
		var pairing = device.DeviceInformation.Pairing;
		if (pairing.IsPaired == false)
		{
			LogAlreadyUnpaired(deviceName);
			return;
		}

		await UnpairCoreAsync(pairing, deviceName);
	}

	private async Task UnpairCoreAsync(DeviceInformationPairing pairing, string deviceName)
	{
		LogBeginUnpairing(deviceName);
		var unpairResult = await pairing.UnpairAsync();

		//// Handle pairing status

		if (unpairResult.Status != DeviceUnpairingResultStatus.Unpaired)
		{
			LogUnpairingFailed(deviceName, unpairResult.Status);
			throw new CommandExitedException(WellKnownResultCodes.DeviceUnpairingFailed);
		}

		LogUnpaired(deviceName);
	}

	private async Task<BluetoothLEDevice> GetDeviceAsync(string bluetoothAddress)
	{
		var parsedAddress = BluetoothAddress.Parse(bluetoothAddress);

		LogBeginSearch(parsedAddress.ToString("X"));
		var device = await BluetoothLEDevice.FromBluetoothAddressAsync(parsedAddress);
		try
		{
			if (device == null)
			{
				LogNotFound(parsedAddress.ToString("X"));
				throw new CommandExitedException(WellKnownResultCodes.DeviceNotFound);
			}

			LogCompleteSearch(device.DeviceId);
			return device;
		}
		catch
		{
			device?.Dispose();
			throw;
		}
	}

	private void PairingRequestedHandler(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
	{
		args.Accept(args.Pin);
		LogAcceptPairing(args.DeviceInformation.Name, args.PairingKind);
	}

	[LoggerMessage(1, LogLevel.Debug, "Begin search for {deviceAddress}")]
	private partial void LogBeginSearch(string deviceAddress);

	[LoggerMessage(2, LogLevel.Error, "No device with address = {deviceAddress}")]
	private partial void LogNotFound(string deviceAddress);

	[LoggerMessage(3, LogLevel.Debug, "Found {deviceId}")]
	private partial void LogCompleteSearch(string deviceId);

	[LoggerMessage(4, LogLevel.Information, "Device {deviceName} already paired")]
	private partial void LogAlreadyPaired(string deviceName);

	[LoggerMessage(5, LogLevel.Debug, "Begin pairing {deviceName}")]
	private partial void LogBeginPairing(string deviceName);

	[LoggerMessage(6, LogLevel.Information, "Please confirm pairing on {deviceName}. Pairing accepted on this device ({pairingKind})")]
	private partial void LogAcceptPairing(string deviceName, DevicePairingKinds pairingKind);

	[LoggerMessage(7, LogLevel.Error, "Device {deviceName} pairing failed ({pairingStatus}). Result protection level: {protectionLevel}")]
	private partial void LogPairingFailed(string deviceName, DevicePairingResultStatus pairingStatus, DevicePairingProtectionLevel protectionLevel);

	[LoggerMessage(8, LogLevel.Information, "Device {deviceName} pairing complete. Protection level: {protectionLevel}")]
	private partial void LogPaired(string deviceName, DevicePairingProtectionLevel protectionLevel);

	[LoggerMessage(9, LogLevel.Information, "Device {deviceName} not paired")]
	private partial void LogAlreadyUnpaired(string deviceName);

	[LoggerMessage(10, LogLevel.Debug, "Begin unpairing {deviceName}")]
	private partial void LogBeginUnpairing(string deviceName);

	[LoggerMessage(11, LogLevel.Error, "Device {deviceName} pairing failed ({pairingStatus})")]
	private partial void LogUnpairingFailed(string deviceName, DeviceUnpairingResultStatus pairingStatus);

	[LoggerMessage(12, LogLevel.Information, "Device {deviceName} unpair complete")]
	private partial void LogUnpaired(string deviceName);

}