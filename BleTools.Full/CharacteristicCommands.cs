using BleTools.Full.Infrastructure;

using Cocona;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;

using System.Runtime.InteropServices;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BleTools.Full;

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

		using var service = await GetGattServiceAsync(device, serviceId);
		service.Session.MaintainConnection = true;
		var characteristic = await GetGattCharacteristicAsync(service, characteristicId, requireAuthorization: requirePairing);

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

		using var device = await _bluetoothService.GetBluetoothDeviceAsync(bluetoothAddress);

		//// Check pairing

		AssertPairing(device, requirePairing);

		//// Obtain metadata

		using var service = await GetGattServiceAsync(device, serviceId);
		service.Session.MaintainConnection = true;
		var characteristic = await GetGattCharacteristicAsync(service, characteristicId, requireAuthorization: requirePairing);

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

	private async Task<GattDeviceService> GetGattServiceAsync(BluetoothLEDevice device, Guid serviceId)
	{
		LogGetService(device.Name, serviceId);
		GattDeviceService? service = null;
		try
		{
			service = device.GetGattService(serviceId);
		}
		catch (COMException)
		{
		}
		if (service == null)
		{
			LogBeginServiceDiscovery(device.Name, serviceId);
			service = await _bluetoothService.GetServiceAsync(device, serviceId);
		}

		return service;
	}

	private async Task<GattCharacteristic> GetGattCharacteristicAsync(
		GattDeviceService service,
		Guid characteristicId,
		bool requireAuthorization)
	{
		LogGetCharacteristic(service.Uuid, characteristicId);
		var characteristic = (await service.GetCharacteristicsForUuidAsync(characteristicId)).Characteristics.SingleOrDefault();
		if (characteristic == null)
		{
			LogBeginCharacteristicDiscovery(service.Uuid, characteristicId);
			characteristic = await _bluetoothService.GetCharacteristicAsync(service, characteristicId);
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

	[LoggerMessage(0, LogLevel.Error, "Device {deviceName} is not paired. Please call pair command first")]
	private partial void LogNotPaired(string deviceName);

	[LoggerMessage(1, LogLevel.Debug, "Get service {serviceId} metadata for {deviceName}")]
	private partial void LogGetService(string deviceName, Guid serviceId);

	[LoggerMessage(2, LogLevel.Warning, "Service {serviceId} not found for {deviceName}. Begin service discovery.")]
	private partial void LogBeginServiceDiscovery(string deviceName, Guid serviceId);

	[LoggerMessage(3, LogLevel.Debug, "Get characteristic {characteristicId} metadata for service {serviceId}")]
	private partial void LogGetCharacteristic(Guid serviceId, Guid characteristicId);

	[LoggerMessage(4, LogLevel.Information, "Characteristic {characteristicId} not found for service {serviceId}. Begin characteristic discovery.")]
	private partial void LogBeginCharacteristicDiscovery(Guid serviceId, Guid characteristicId);

	[LoggerMessage(5, LogLevel.Information, "{characteristicId} value is '{value}'")]
	private partial void LogCharacteristicRead(Guid characteristicId, string value);

	[LoggerMessage(6, LogLevel.Error, "Failed to read {characteristicId}: {status} ({protocolError})")]
	private partial void LogCharacteristicReadFailed(Guid characteristicId, GattCommunicationStatus status, string protocolError);

	[LoggerMessage(7, LogLevel.Information, "{characteristicId} set to '{value}'")]
	private partial void LogCharacteristicWritten(Guid characteristicId, string value);

	[LoggerMessage(8, LogLevel.Information, "{characteristicId} set to '{value}'. Response: '{response}'")]
	private partial void LogCharacteristicWrittenWithResponse(Guid characteristicId, string value, string response);

	[LoggerMessage(9, LogLevel.Error, "Failed to write {characteristicId}: {status} ({protocolError})")]
	private partial void LogCharacteristicWriteFailed(Guid characteristicId, GattCommunicationStatus status, string protocolError);

}