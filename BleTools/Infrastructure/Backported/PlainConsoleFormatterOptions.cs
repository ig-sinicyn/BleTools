using Microsoft.Extensions.Logging.Console;

namespace BleTools.Infrastructure.Backported;

public sealed class PlainConsoleFormatterOptions : ConsoleFormatterOptions
{
	public const string FormatterName = "plain-console";

	/// <summary>
	/// Determines when to use color when logging messages.
	/// </summary>
	public LoggerColorBehavior ColorBehavior { get; set; }
}