using BleTools.Infrastructure;

using Cocona;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BleTools;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal partial class CharacteristicCommands
{
	private readonly BluetoothService _bluetoothService;
	private readonly ILogger<CharacteristicCommands> _logger;

	public CharacteristicCommands(
		BluetoothService bluetoothService,
		ILogger<CharacteristicCommands> logger)
	{
		_bluetoothService = bluetoothService;
		_logger = logger;
	}

	[Command("read", Description = "Reads characteristic for specified device")]
	public async Task ReadAsync(
		[Argument] string bluetoothAddress,
		[Option("service", new[] { 's' }, Description = "GATT service UUID")] Guid serviceId,
		[Option("characteristic", new[] { 'c' }, Description = "GATT service characteristic UUID")] Guid characteristicId,
		[Option("require-pairing", new[] { 'p' }, Description = "Require the device to be paired")] bool requirePairing = false,
		[Option("uncached", new[] { 'u' }, Description = "Ignore cache")] bool uncached = false)
	{
		//// Find device

		using var device = await _bluetoothService.GetBluetoothDeviceAsync(bluetoothAddress);

		//// Check pairing

		AssertPairing(device, requirePairing);

		//// Obtain metadata

		var cacheMode = uncached ? BluetoothCacheMode.Uncached : BluetoothCacheMode.Cached;
		using var service = await _bluetoothService.GetServiceAsync(device, serviceId, cacheMode);
		service.Session.MaintainConnection = true;
		var characteristic = await _bluetoothService.GetCharacteristicAsync(service, characteristicId, cacheMode);
		if (requirePairing)
			characteristic.ProtectionLevel = GattProtectionLevel.EncryptionAndAuthenticationRequired;

		//// Read value

		var readResult = await characteristic.ReadValueAsync(cacheMode);
		if (readResult.Status != GattCommunicationStatus.Success)
		{
			LogCharacteristicReadFailed(characteristicId, readResult.Status, GetGattErrorDescriptor(readResult.ProtocolError));
			throw new CommandExitedException(WellKnownResultCodes.CharacteristicReadFailed);
		}

		var value = readResult.Value.AsString();
		LogCharacteristicRead(characteristicId, value);
	}

	[Command("write", Description = "Reads characteristic for specified device")]
	public async Task WriteAsync(
		[Argument] string bluetoothAddress,
		[Argument] string value,
		[Option("service", new[] { 's' }, Description = "GATT service UUID")] Guid serviceId,
		[Option("characteristic", new[] { 'c' }, Description = "GATT service characteristic UUID")] Guid characteristicId,
		[Option("require-pairing", new[] { 'p' }, Description = "Require the device to be paired")] bool requirePairing = false,
		[Option("uncached", new[] { 'u' }, Description = "Ignore cache")] bool uncached = false)
	{
		//// Find device

		using var device = await _bluetoothService.GetBluetoothDeviceAsync(bluetoothAddress);

		//// Check pairing

		AssertPairing(device, requirePairing);

		//// Obtain metadata

		var cacheMode = uncached ? BluetoothCacheMode.Uncached : BluetoothCacheMode.Cached;
		using var service = await _bluetoothService.GetServiceAsync(device, serviceId, cacheMode);
		service.Session.MaintainConnection = true;
		var characteristic = await _bluetoothService.GetCharacteristicAsync(service, characteristicId, cacheMode);
		if (requirePairing)
			characteristic.ProtectionLevel = GattProtectionLevel.EncryptionAndAuthenticationRequired;

		//// Write value

		var buffer = CharacteristicValue.FromString(value);
		var writeResult = await characteristic.WriteValueAsync(buffer);
		if (writeResult != GattCommunicationStatus.Success)
		{
			LogCharacteristicWriteFailed(characteristicId, writeResult, GetGattErrorDescriptor(null));
			throw new CommandExitedException(WellKnownResultCodes.CharacteristicWriteFailed);
		}

		LogCharacteristicWritten(characteristicId, value);
	}

	private void AssertPairing(BluetoothLEDevice device, bool required)
	{
		if (required)
		{
			var pairing = device.DeviceInformation.Pairing;
			if (pairing.IsPaired == false)
			{
				LogNotPaired(device.Name);

				throw new CommandExitedException(WellKnownResultCodes.DeviceNotPaired);
			}
		}
	}

	private static string GetGattErrorDescriptor(byte? protocolError)
	{
		var errorDescriptor = protocolError == null
			? "unknown"
			: WellKnownGattProtocolErrors.GetErrorDescriptor(protocolError.Value) ?? $"0x{protocolError.Value:X2}";
		return errorDescriptor;
	}



	[Command("list", Description = "Lists services and characteristic for specified device")]
	public async Task ListAsync(
		[Argument] string bluetoothAddress,
		[Option("require-pairing", new[] { 'p' }, Description = "Require the device to be paired")] bool requirePairing = false,
		[Option("uncached", new[] { 'u' }, Description = "Ignore cache")] bool uncached = false)
	{
		//// Find device

		using var device = await _bluetoothService.GetBluetoothDeviceAsync(bluetoothAddress);

		//// Check pairing

		AssertPairing(device, requirePairing);

		//// List metadata

		var cacheMode = uncached ? BluetoothCacheMode.Uncached : BluetoothCacheMode.Cached;

		var listResult = await device.GetGattServicesAsync(cacheMode);
		if (listResult.Status != GattCommunicationStatus.Success)
		{
			LogListServicesFailed(device.Name, listResult.Status, GetGattErrorDescriptor(listResult.ProtocolError));
			throw new CommandExitedException(WellKnownResultCodes.ListServicesFailed);
		}

		LogServicesFound(device.Name, listResult.Services.Count);
		foreach (var service in listResult.Services)
		{
			try
			{
				LogServiceItem(service.Uuid);

				await ListServiceCharacteristicsAsync(service, cacheMode);
			}
			finally
			{
				service.Dispose();
			}
		}

	}

	private async Task ListServiceCharacteristicsAsync(GattDeviceService service, BluetoothCacheMode cacheMode)
	{
		var listResult = await service.GetCharacteristicsAsync(cacheMode);
		if (listResult.Status != GattCommunicationStatus.Success)
		{
			LogListCharacteristicsFailed(service.Uuid, listResult.Status, GetGattErrorDescriptor(listResult.ProtocolError));
			return;
		}

		foreach (var characteristic in listResult.Characteristics)
		{
			LogCharacteristicItem(characteristic.Uuid, characteristic.CharacteristicProperties);
		}
	}

	[LoggerMessage(0, LogLevel.Error, "Device {deviceName} is not paired. Please call pair command first.")]
	private partial void LogNotPaired(string deviceName);

	[LoggerMessage(1, LogLevel.Information, "{characteristicId} value is '{value}'.")]
	private partial void LogCharacteristicRead(Guid characteristicId, string value);

	[LoggerMessage(2, LogLevel.Error, "Failed to read {characteristicId}: {status} ({protocolError}).")]
	private partial void LogCharacteristicReadFailed(Guid characteristicId, GattCommunicationStatus status, string protocolError);

	[LoggerMessage(3, LogLevel.Information, "{characteristicId} set to '{value}'.")]
	private partial void LogCharacteristicWritten(Guid characteristicId, string value);

	[LoggerMessage(4, LogLevel.Error, "Failed to write {characteristicId}: {status} ({protocolError}).")]
	private partial void LogCharacteristicWriteFailed(Guid characteristicId, GattCommunicationStatus status, string protocolError);

	[LoggerMessage(5, LogLevel.Error, "Failed to list services for {deviceName}: {status} ({protocolError}).")]
	private partial void LogListServicesFailed(string deviceName, GattCommunicationStatus status, string protocolError);

	[LoggerMessage(6, LogLevel.Information, "{serviceCount} service(s) found for {deviceName}.")]
	private partial void LogServicesFound(string deviceName, int serviceCount);

	[LoggerMessage(7, LogLevel.Information, "* {serviceId}:")]
	private partial void LogServiceItem(Guid serviceId);

	[LoggerMessage(8, LogLevel.Error, "Failed to list characteristics for service {serviceId}: {status} ({protocolError}).")]
	private partial void LogListCharacteristicsFailed(Guid serviceId, GattCommunicationStatus status, string protocolError);

	[LoggerMessage(9, LogLevel.Information, "   - {characteristicId}: {properties};")]
	private partial void LogCharacteristicItem(Guid characteristicId, GattCharacteristicProperties properties);
}