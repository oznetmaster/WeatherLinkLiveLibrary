// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WeatherLinkLive;

public static partial class WeatherLinkLiveAPI

	{
	public partial class WeatherLinkLive
		{
		private static readonly int[] RecoverySeconds = [10, 20, 30, 40, 50, 60, 120];
		private readonly CancellationTokenSource _recoveryCancellation = new ();
		private bool _disconnected;
		private int _recoveryGeneration;
		private Task? _recoveryTask;
		private CancellationTokenSource? _recoveryDelayCancellation;
		internal Func<TimeSpan, CancellationToken, Task> RecoveryDelayAsync { get; set; } = Task.Delay;
		internal Task RecoveryCompletion { get { lock (_stateLock) return _recoveryTask ?? Task.CompletedTask; } }

		/// <summary>
		/// Raised once when a previously successful device stops supplying valid readings.
		/// The failed caller still receives its exception; automatic recovery proceeds in the background.
		/// Handlers run on a worker/calling thread, outside client locks, and must return promptly.
		/// </summary>
		public event EventHandler? Disconnected;

		/// <summary>
		/// Raised once after valid readings have been fetched following a disconnection.
		/// Readings are available to the handler. Initial connection does not raise this event.
		/// Handlers run outside client locks; handler exceptions are logged and isolated.
		/// </summary>
		public event EventHandler? Reconnected;

		private static bool IsRecoveryFailure (Exception error) =>
			error is HttpRequestException or IOException or InvalidDataException or OperationCanceledException;

		private void RaiseConnectionEvent (EventHandler? handlers)
			{
			if (handlers == null) return;
			foreach (EventHandler handler in handlers.GetInvocationList ())
				{
				lock (_stateLock) { if (_disposed) return; }
				try { handler (this, EventArgs.Empty); }
				catch (Exception error) { _logFailed (_logger, error); }
				}
			}

		private void StartRecoveryIfNeeded ()
			{
			lock (_stateLock)
				{
				if (_disposed || !_disconnected || _recoveryTask != null) return;
				_recoveryTask = Task.Run (RecoverAsync);
				}
			}

		private async Task RecoverAsync ()
			{
			CancellationToken token = _recoveryCancellation.Token;
			int generation;
			lock (_stateLock) generation = _recoveryGeneration;
			int attempt = 0;
			bool finishedNormally = false;
			try
				{
				while (true)
					{
					lock (_stateLock)
						{
						if (_disposed || !_disconnected) { finishedNormally = true; return; }
						if (generation != _recoveryGeneration) { generation = _recoveryGeneration; attempt = 0; }
						}
					int seconds = attempt < RecoverySeconds.Length ? RecoverySeconds[attempt++] : 300;
					if (!await WaitForRecoveryDelayAsync (seconds, generation, token).ConfigureAwait (false)) continue;
					token.ThrowIfCancellationRequested ();
					lock (_stateLock)
						{
						if (_disposed || !_disconnected) { finishedNormally = true; return; }
						// A manual successful refresh followed by a new outage starts a new sequence.
						if (generation != _recoveryGeneration) continue;
						}
					try { await RefreshAsync (token).ConfigureAwait (false); }
					catch (Exception error) when (IsRecoveryFailure (error) && !token.IsCancellationRequested)
						{
						// Keep the original failure and snapshot; the next delay is measured from this attempt.
						}
					}
				}
			catch (OperationCanceledException) when (token.IsCancellationRequested) { }
			catch (ObjectDisposedException) when (token.IsCancellationRequested) { }
			catch (Exception error) { _logFailed (_logger, error); }
			finally
				{
				lock (_stateLock) _recoveryTask = null;
				// Cover a new outage arriving while the old loop was exiting.
				if (finishedNormally) StartRecoveryIfNeeded ();
				}
			}

		private async Task<bool> WaitForRecoveryDelayAsync (int seconds, int generation, CancellationToken token)
			{
			CancellationTokenSource delayCancellation;
			lock (_stateLock)
				{
				if (_disposed || !_disconnected || generation != _recoveryGeneration) return false;
				_recoveryDelayCancellation = delayCancellation = CancellationTokenSource.CreateLinkedTokenSource (token);
				}
			try
				{
				await RecoveryDelayAsync (TimeSpan.FromSeconds (seconds), delayCancellation.Token).ConfigureAwait (false);
				return !delayCancellation.IsCancellationRequested;
				}
			catch (OperationCanceledException) when (!token.IsCancellationRequested) { return false; }
			finally
				{
				lock (_stateLock) _recoveryDelayCancellation = null;
				delayCancellation.Dispose ();
				}
			}
		}
	}
