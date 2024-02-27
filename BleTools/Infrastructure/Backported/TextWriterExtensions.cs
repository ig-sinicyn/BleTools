namespace BleTools.Infrastructure.Backported;

// BASED ON: https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Console/src/TextWriterExtensions.cs
internal static class TextWriterExtensions
{
	public static void WriteColoredMessage(this TextWriter textWriter, string message, ConsoleColor? background, ConsoleColor? foreground)
	{
		// Order: backgroundColor, foregroundColor, Message, reset foregroundColor, reset backgroundColor
		if (background.HasValue)
		{
			textWriter.Write((string?)AnsiParser.GetBackgroundColorEscapeCode(background.Value));
		}
		if (foreground.HasValue)
		{
			textWriter.Write((string?)AnsiParser.GetForegroundColorEscapeCode(foreground.Value));
		}
		textWriter.Write(message);
		if (foreground.HasValue)
		{
			textWriter.Write((string?)AnsiParser.DefaultForegroundColor); // reset to default foreground color
		}
		if (background.HasValue)
		{
			textWriter.Write((string?)AnsiParser.DefaultBackgroundColor); // reset to the background color
		}
	}
}

