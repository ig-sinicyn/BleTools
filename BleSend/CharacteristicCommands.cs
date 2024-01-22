using Cocona;

using Microsoft.Extensions.Logging;

using Windows.Devices.Bluetooth;
using BleSend.Infrastructure;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using JetBrains.Annotations;

namespace BleSend;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal partial class CharacteristicCommands
{
	private readonly ILogger<CharacteristicCommands> _logger;

	public CharacteristicCommands(ILogger<CharacteristicCommands> logger)
	{
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

		var device = await GetDeviceAsync(bluetoothAddress);

		//// Check pairing

		AssertPairing(device, requirePairing);

		//// Obtain metadata

		using var service = GetGattService(device, serviceId);
		service.Session.MaintainConnection = true;
		var characteristic = GetGattCharacteristic(service, characteristicId, requireAuthorization: requirePairing);

		//// Read value

		var readMode = uncached ? BluetoothCacheMode.Uncached : BluetoothCacheMode.Cached;
		var readResult = await characteristic.ReadValueAsync(readMode);
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
		[Option("require-pairing", new[] { 'p' }, Description = "Require the device to be paired")] bool requirePairing = false)
	{
		//// Find device

		var device = await GetDeviceAsync(bluetoothAddress);

		//// Check pairing

		AssertPairing(device, requirePairing);

		//// Obtain metadata

		using var service = GetGattService(device, serviceId);
		service.Session.MaintainConnection = true;
		var characteristic = GetGattCharacteristic(service, characteristicId, requireAuthorization: requirePairing);

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

	private GattDeviceService GetGattService(BluetoothLEDevice device, Guid serviceId)
	{
		LogGetService(device.Name, serviceId);
		var service = device.GetGattService(serviceId);
		if (service == null)
		{
			LogServiceNotFound(device.Name, serviceId);
			throw new CommandExitedException(WellKnownResultCodes.ServiceNotFound);
		}

		return service;
	}

	private GattCharacteristic GetGattCharacteristic(
		GattDeviceService service,
		Guid characteristicId,
		bool requireAuthorization)
	{
		LogGetCharacteristic(service.Uuid, characteristicId);
		var characteristic = service.GetCharacteristics(characteristicId).SingleOrDefault();
		if (characteristic == null)
		{
			LogCharacteristicNotFound(service.Uuid, characteristicId);
			throw new CommandExitedException(WellKnownResultCodes.CharacteristicNotFound);
		}

		if (requireAuthorization)
		{
			characteristic.ProtectionLevel = GattProtectionLevel.EncryptionAndAuthenticationRequired;
		}

		return characteristic;
	}

	private static string GetGattErrorDescriptor(byte? protocolError)
	{
		var errorDescriptor = protocolError == null
			? "unknown"
			: WellKnownGattProtocolErrors.GetErrorDescriptor(protocolError.Value) ?? $"0x{protocolError.Value:X2}";
		return errorDescriptor;
	}

	[LoggerMessage(1, LogLevel.Debug, "Begin search for {deviceAddress}")]
	private partial void LogBeginSearch(string deviceAddress);

	[LoggerMessage(2, LogLevel.Error, "No device with address = {deviceAddress}")]
	private partial void LogNotFound(string deviceAddress);

	[LoggerMessage(3, LogLevel.Debug, "Found {deviceId}")]
	private partial void LogCompleteSearch(string deviceId);

	[LoggerMessage(4, LogLevel.Error, "Device {deviceName} is not paired. Please call pair command first")]
	private partial void LogNotPaired(string deviceName);

	[LoggerMessage(5, LogLevel.Debug, "Get service {serviceId} metadata for {deviceName}")]
	private partial void LogGetService(string deviceName, Guid serviceId);

	[LoggerMessage(6, LogLevel.Error, "Service {serviceId} not found for {deviceName}")]
	private partial void LogServiceNotFound(string deviceName, Guid serviceId);

	[LoggerMessage(7, LogLevel.Debug, "Get characteristic {characteristicId} metadata for service {serviceId}")]
	private partial void LogGetCharacteristic(Guid serviceId, Guid characteristicId);

	[LoggerMessage(8, LogLevel.Error, "Characteristic {characteristicId} not found for service {serviceId}")]
	private partial void LogCharacteristicNotFound(Guid serviceId, Guid characteristicId);

	[LoggerMessage(9, LogLevel.Information, "{characteristicId} value is '{value}'")]
	private partial void LogCharacteristicRead(Guid characteristicId, string value);

	[LoggerMessage(10, LogLevel.Error, "Failed to read {characteristicId}: {status} ({protocolError})")]
	private partial void LogCharacteristicReadFailed(Guid characteristicId, GattCommunicationStatus status, string protocolError);

	[LoggerMessage(11, LogLevel.Information, "{characteristicId} set to '{value}'")]
	private partial void LogCharacteristicWritten(Guid characteristicId, string value);

	[LoggerMessage(12, LogLevel.Information, "{characteristicId} set to '{value}'. Response: '{response}'")]
	private partial void LogCharacteristicWrittenWithResponse(Guid characteristicId, string value, string response);

	[LoggerMessage(13, LogLevel.Error, "Failed to write {characteristicId}: {status} ({protocolError})")]
	private partial void LogCharacteristicWriteFailed(Guid characteristicId, GattCommunicationStatus status, string protocolError);

}