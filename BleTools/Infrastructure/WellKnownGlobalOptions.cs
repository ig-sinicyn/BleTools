using Cocona.Command;

namespace BleTools.Infrastructure;

public static class WellKnownGlobalOptions
{
	public static readonly CommandOptionDescriptor DebugFlag = new(
		typeof(bool),
		"debug",
		[],
		"Enables debug output",
		new CoconaDefaultValue(false),
		null,
		CommandOptionFlags.None,
		[]);
}