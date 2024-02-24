using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace BleTools.Infrastructure.Backported;

// BASED ON: https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Console/src/SimpleConsoleFormatter.cs
internal sealed class PlainConsoleFormatter : ConsoleFormatter, IDisposable
{
	private static bool IsAndroidOrAppleMobile => OperatingSystem.IsAndroid() ||
		OperatingSystem.IsTvOS() ||
		OperatingSystem.IsIOS(); // returns true on MacCatalyst

	private const string LogLevelPadding = ": ";

	private readonly IDisposable? _optionsReloadToken;

	public PlainConsoleFormatter(IOptionsMonitor<PlainConsoleFormatterOptions> options)
		: base(ConsoleFormatterNames.Simple)
	{
		ReloadLoggerOptions(options.CurrentValue);
		_optionsReloadToken = options.OnChange(ReloadLoggerOptions);
	}

	[MemberNotNull(nameof(FormatterOptions))]
	private void ReloadLoggerOptions(PlainConsoleFormatterOptions options)
	{
		FormatterOptions = options;
	}

	public void Dispose()
	{
		_optionsReloadToken?.Dispose();
	}

	internal PlainConsoleFormatterOptions FormatterOptions { get; set; } = default!;

	public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
	{
		var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
		if (logEntry.Exception == null && message == null)
		{
			return;
		}

		var logLevel = logEntry.LogLevel;
		var logLevelColors = GetLogLevelConsoleColors(logLevel);
		var logLevelString = GetLogLevelString(logLevel);

		string? timestamp = null;
		var timestampFormat = FormatterOptions.TimestampFormat;
		// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
		if (timestampFormat != null)
		{
			var dateTimeOffset = GetCurrentDateTime();
			timestamp = dateTimeOffset.ToString(timestampFormat);
		}
		if (timestamp != null)
		{
			textWriter.Write(timestamp);
		}
		// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
		if (logLevelString != null)
		{
			textWriter.WriteColoredMessage(logLevelString, logLevelColors.Background, logLevelColors.Foreground);
		}
		CreateDefaultLogMessage(textWriter, logEntry, message, scopeProvider);
	}

	private void CreateDefaultLogMessage<TState>(TextWriter textWriter, in LogEntry<TState> logEntry, string? message, IExternalScopeProvider? scopeProvider)
	{
		var exception = logEntry.Exception;

		// Example:
		// info: ConsoleApp.Program[10]
		//       Request received

		textWriter.Write(LogLevelPadding);
		WriteMessage(textWriter, message);

		// Example:
		// System.InvalidOperationException
		//    at Namespace.Class.Function() in File:line X
		if (exception != null)
		{
			// exception message
			WriteMessage(textWriter, exception.ToString());
		}
		textWriter.Write(Environment.NewLine);
	}

	private static void WriteMessage(TextWriter textWriter, string? message)
	{
		if (!string.IsNullOrEmpty(message))
		{
			textWriter.Write(' ');
			WriteReplacing(textWriter, Environment.NewLine, " ", message);
		}

		static void WriteReplacing(TextWriter writer, string oldValue, string newValue, string message)
		{
			var newMessage = message.Replace(oldValue, newValue);
			writer.Write(newMessage);
		}
	}

	private DateTimeOffset GetCurrentDateTime()
	{
		return FormatterOptions.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
	}

	private static string GetLogLevelString(LogLevel logLevel)
	{
		return logLevel switch
		{
			LogLevel.Trace => "trce",
			LogLevel.Debug => "dbug",
			LogLevel.Information => "info",
			LogLevel.Warning => "warn",
			LogLevel.Error => "fail",
			LogLevel.Critical => "crit",
			_ => throw new ArgumentOutOfRangeException(nameof(logLevel))
		};
	}

	private ConsoleColors GetLogLevelConsoleColors(LogLevel logLevel)
	{
		// We shouldn't be outputting color codes for Android/Apple mobile platforms,
		// they have no shell (adb shell is not meant for running apps) and all the output gets redirected to some log file.
		var disableColors = FormatterOptions.ColorBehavior == LoggerColorBehavior.Disabled ||
			(FormatterOptions.ColorBehavior == LoggerColorBehavior.Default && (!ConsoleUtils.EmitAnsiColorCodes || IsAndroidOrAppleMobile));
		if (disableColors)
		{
			return new(null, null);
		}

		// We must explicitly set the background color if we are setting the foreground color,
		// since just setting one can look bad on the users console.
		return logLevel switch
		{
			LogLevel.Trace => new(ConsoleColor.Gray, ConsoleColor.Black),
			LogLevel.Debug => new(ConsoleColor.Gray, ConsoleColor.Black),
			LogLevel.Information => new(ConsoleColor.DarkGreen, ConsoleColor.Black),
			LogLevel.Warning => new(ConsoleColor.Yellow, ConsoleColor.Black),
			LogLevel.Error => new(ConsoleColor.Black, ConsoleColor.DarkRed),
			LogLevel.Critical => new(ConsoleColor.White, ConsoleColor.DarkRed),
			_ => new(null, null)
		};
	}

	private readonly struct ConsoleColors
	{
		public ConsoleColors(ConsoleColor? foreground, ConsoleColor? background)
		{
			Foreground = foreground;
			Background = background;
		}

		public ConsoleColor? Foreground { get; }

		public ConsoleColor? Background { get; }
	}
}