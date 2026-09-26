using System.Text;
using Porta.Pty;

namespace SDeleteGUI.Services;

/// <summary>
/// Запускает консольный процесс через псевдотерминал (ConPTY) с помощью библиотеки Porta.Pty.
/// Это единственный надёжный способ получить вывод устаревших консольных CRT-приложений
/// (таких как sdelete) в реальном времени: ConPTY заставляет дочерний процесс думать,
/// что он подключён к настоящему терминалу (isatty() возвращает true), из-за чего CRT
/// использует построчную буферизацию — точно так же, как при ручном запуске в PowerShell.
/// </summary>
internal static class PseudoConsoleProcessRunner
{
	/// <summary>
	/// Запускает <paramref name="exePath"/> с аргументами <paramref name="arguments"/> в PTY,
	/// вызывая <paramref name="onOutput"/> для каждого куска UTF-8 текста, который приходит.
	/// Возвращает код возврата процесса.
	/// </summary>
	public static async Task<int> RunAsync(
		string exePath,
		string[] arguments,
		Action<string> onOutput,
		CancellationToken cancellationToken = default)
	{
		var options = new PtyOptions
		{
			Name = "SDeletePTY",
			Cols = 120,
			Rows = 32,
			Cwd = Environment.CurrentDirectory,
			App = exePath,
			CommandLine = arguments,
			Environment = Environment.GetEnvironmentVariables()
				.Cast<System.Collections.DictionaryEntry>()
				.Where(e => e.Value is not null)
				.ToDictionary(e => (string)e.Key, e => (string)e.Value!)
		};

		using IPtyConnection terminal = await PtyProvider
			.SpawnAsync(options, cancellationToken)
			.ConfigureAwait(false);

		// Отдельный CTS, который отменит чтение, когда процесс завершится.
		using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		var exitTcs = new TaskCompletionSource<int>(
			TaskCreationOptions.RunContinuationsAsynchronously);

		terminal.ProcessExited += (_, e) =>
		{
			exitTcs.TrySetResult(e.ExitCode);
			// Подсказываем циклу чтения: больше данных не будет.
			readCts.Cancel();
		};

		var readTask = Task.Run(async () =>
		{
			var buffer = new byte[1024];
			try
			{
				while (!readCts.IsCancellationRequested)
				{
					int read = await terminal.ReaderStream
						.ReadAsync(buffer, readCts.Token)
						.ConfigureAwait(false);

					if (read == 0) break;

					var text = Encoding.UTF8.GetString(buffer, 0, read);
					onOutput(text);
				}
			}
			catch (OperationCanceledException)
			{
				// Нормальная ситуация: процесс завершился, чтение прервано.
			}
		});

		int exitCode = await exitTcs.Task
			.WaitAsync(cancellationToken)
			.ConfigureAwait(false);

		// Ждём чтение с небольшим таймаутом, чтобы не зависнуть,
		// если ConPTY не закрыл пайп сразу.
		try
		{
			await readTask.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None)
						.ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			// Не страшно: скорее всего, все данные уже прочитаны.
		}

		return exitCode;
	}
}
