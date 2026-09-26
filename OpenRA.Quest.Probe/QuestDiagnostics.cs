#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.IO;
using System.Threading;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Records startup milestones and managed failures in private app storage.
	/// Android logcat remains necessary for native crashes and ANR reports.
	/// </summary>
	static class QuestDiagnostics
	{
		const string Tag = "OpenRA.Quest.Probe";
		const long MaximumLogBytes = 1024 * 1024;
		static readonly object Sync = new();
		static string? logPath;
		static int handlersInstalled;

		public static void Initialize(string appFiles)
		{
			lock (Sync)
			{
				logPath = Path.Combine(appFiles, "quest-diagnostics.log");
				try
				{
					if (File.Exists(logPath) && new FileInfo(logPath).Length > MaximumLogBytes)
						File.Move(logPath, logPath + ".previous", true);
				}
				catch (Exception e)
				{
					Android.Util.Log.Warn(Tag, $"Diagnose-Rotation fehlgeschlagen: {e.Message}");
				}
			}

			if (Interlocked.Exchange(ref handlersInstalled, 1) == 0)
			{
				AppDomain.CurrentDomain.UnhandledException += (_, args) =>
					Error("Unbehandelte verwaltete Ausnahme", args.ExceptionObject as Exception);
				TaskScheduler.UnobservedTaskException += (_, args) =>
					Error("Unbeobachteter Task-Fehler", args.Exception);
			}

			Write("Activity erstellt.");
		}

		public static void Write(string message) => Record("INFO", message);

		public static void Error(string message, Exception? exception) =>
			Record("ERROR", exception == null ? message : $"{message}: {exception}");

		static void Record(string level, string message)
		{
			var line = $"{DateTimeOffset.Now:O} [{level}] [PID {Android.OS.Process.MyPid()}] " +
				$"[Thread {Environment.CurrentManagedThreadId}] {message}";
			try
			{
				lock (Sync)
				{
					if (logPath != null)
						File.AppendAllText(logPath, line + Environment.NewLine);
				}
			}
			catch (Exception e)
			{
				Android.Util.Log.Warn(Tag, $"Diagnose-Datei konnte nicht geschrieben werden: {e.Message}");
			}

			if (level == "ERROR")
				Android.Util.Log.Error(Tag, line);
			else
				Android.Util.Log.Info(Tag, line);
		}
	}
}
