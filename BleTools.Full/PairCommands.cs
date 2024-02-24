using Cocona;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;

using Windows.Devices.Enumeration;

namespace BleTools.Full;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal partial class PairCommands
{
	private readonly BluetoothService _bluetoothService;
	private readonly ILogger<PairCommands> _logger;

	public PairCommands(
		BluetoothService bluetoothService,
		ILogger<PairCommands> logger)
	{
		_bluetoothService = bluetoothService;
		_logger = logger;
	}

	[Command("pair", Description = "Starts pairing for specified device (usually requires confirmation on the target device)")]
	public async Task PairAsync(
		[Argument] string bluetoothAddress,
		[Option("force", new[] { 'f' }, Description = "Force pairing")] bool force = false)
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
					await UnpairCoreAsync(device.DeviceInformation.Pairing, device.Name);
					device.Dispose();
					device = await _bluetoothService.GetBluetoothDeviceAsync(bluetoothAddress);
				}
				else
				{
					LogAlreadyPaired(device.Name);
					return;
				}
			}

			//// Trigger pairing

			var deviceName = device.Name;
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

	[Command("unpair", Description = "Breaks pairing for specified device")]
	public async Task UnpairAsync([Argument] string bluetoothAddress)
	{
		//// Find device

		using var device = await _bluetoothService.GetBluetoothDeviceAsync(bluetoothAddress);

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
		LogAcceptPairing(args.DeviceInformation.Name, args.PairingKind);
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

}