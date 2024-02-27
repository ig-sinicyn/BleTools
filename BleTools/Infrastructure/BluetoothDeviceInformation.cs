using BleTools.Models;

using System.Text;

using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BleTools.Infrastructure;

internal static class BluetoothDeviceInformation
{

	public static readonly IReadOnlyCollection<string> DeviceWatcherProperties =
	[
		"System.Devices.DevObjectType",
		"System.Devices.Aep.DeviceAddress",
		"System.Devices.Aep.IsConnected",
		"System.Devices.Aep.IsPaired",
		"System.Devices.Aep.Bluetooth.Le.IsConnectable",
		"System.Devices.Aep.Bluetooth.IssueInquiry"
	];

	public static readonly IReadOnlyCollection<string> ScanDeviceWatcherProperties =
	[
		"System.Devices.DevObjectType",
		"System.Devices.Aep.DeviceAddress",
		"System.Devices.Aep.IsConnected",
		"System.Devices.Aep.IsPaired",
		"System.Devices.Aep.Bluetooth.Le.IsConnectable",
		"System.Devices.Aep.Bluetooth.IssueInquiry",
		"System.Devices.Aep.ModelId",
		"System.Devices.Aep.ModelName",
		"System.Devices.Aep.Manufacturer",
		"System.Devices.Aep.SignalStrength"
	];

	public static string ToAqsFilter(this BluetoothDeviceFilter deviceFilter) =>
		deviceFilter switch
		{
			BluetoothDeviceFilter.BluetoothLe =>
				"System.Devices.DevObjectType:=5 AND System.Devices.Aep.ProtocolId:=\"{BB7BB05E-5972-42B5-94FC-76EAA7084D49}\"",
			BluetoothDeviceFilter.BluetoothClassic =>
				"System.Devices.DevObjectType:=5 AND System.Devices.Aep.ProtocolId:=\"{E0CBF06C-CD8B-4647-BB8A-263B43F0F974}\"",
			BluetoothDeviceFilter.All =>
				"System.Devices.DevObjectType:=5 AND (System.Devices.Aep.ProtocolId:=\"{BB7BB05E-5972-42B5-94FC-76EAA7084D49}\" OR System.Devices.Aep.ProtocolId:=\"{E0CBF06C-CD8B-4647-BB8A-263B43F0F974}\")",
			_ => throw new ArgumentOutOfRangeException(nameof(deviceFilter), deviceFilter, null)
		};

	public static string GetDisplayName(this BluetoothLEDevice device)
	{
		var deviceAddress = BluetoothAddress.Format(device.BluetoothAddress);

		return string.IsNullOrEmpty(device.Name)
			? deviceAddress
			: $"{deviceAddress} ({device.Name})";
	}

	public static string GetDisplayName(this DeviceInformation device)
	{
		const string deviceAddressProperty = "System.Devices.Aep.DeviceAddress";

		var deviceAddress = ((string?)device.Properties.GetValueOrDefault(deviceAddressProperty))?.ToUpperInvariant();

		return device.Name switch
		{
			var name when string.IsNullOrEmpty(name) => deviceAddress ?? device.Id,
			var name when deviceAddress == null => name,
			var name => $"{deviceAddress} ({name})"
		};
	}

	public static BluetoothPairingStatus GetPairingStatus(this DeviceInformation device) =>
		device.Pairing switch
		{
			{ IsPaired: true } => BluetoothPairingStatus.Paired,
			{ CanPair: true } => BluetoothPairingStatus.Unpaired,
			_ => BluetoothPairingStatus.CannotBePaired
		};

	public static string GetScanDeviceDescription(this DeviceInformation device)
	{
		const string signalStrengthProperty = "System.Devices.Aep.SignalStrength";
		const string modelIdProperty = "System.Devices.Aep.ModelId";
		const string modelNameProperty = "System.Devices.Aep.ModelName";
		const string manufacturerProperty = "System.Devices.Aep.Manufacturer";

		var result = new StringBuilder();

		var properties = device.Properties;

		var rssi = (int?)properties.GetValueOrDefault(signalStrengthProperty);
		if (rssi != null)
			result.Append($"RSSI: {rssi}, ");

		var pairingStatus = device.GetPairingStatus();
		result.Append($"Status: {pairingStatus}");

		var modelId = (string?)properties.GetValueOrDefault(modelIdProperty);
		if (modelId != null)
			result.Append($", Model id: {modelId}");

		var modelName = (string?)properties.GetValueOrDefault(modelNameProperty);
		if (modelId != null)
			result.Append($", Model name: {modelName}");

		var manufacturer = (string?)properties.GetValueOrDefault(manufacturerProperty);
		if (manufacturer != null)
			result.Append($", Manufacturer: {manufacturer}");

		return result.ToString();
	}
}