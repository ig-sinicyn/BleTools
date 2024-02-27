using Cocona.Command;

namespace BleTools.Infrastructure;

public static class WellKnownGlobalOptions
{
	public static readonly CommandOptionDescriptor DebugFlag = new CommandOptionDescriptor(
		typeof(bool),
		"debug",
		Array.Empty<char>(),
		"Enables debug output",
		new CoconaDefaultValue(false),
		null,
		CommandOptionFlags.None,
		Array.Empty<Attribute>());
}