using System.Reflection;

using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BleSend.Infrastructure;

public static class WellKnownGattProtocolErrors
{
	private static string?[] GetDescriptors()
	{
		var result = new string?[255];

		var props = typeof(GattProtocolError)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(x => x.PropertyType == typeof(byte));
		foreach (var propertyInfo in props)
		{
			var index = (byte)propertyInfo.GetValue(null)!;
			result[index] = propertyInfo.Name;
		}

		return result;
	}

	private static readonly string?[] _descriptors = GetDescriptors();

	public static string? GetErrorDescriptor(byte protocolError) => _descriptors[protocolError];
}