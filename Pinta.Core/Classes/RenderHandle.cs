using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pinta.Core;

internal sealed class RenderHandle : IDisposable
{
	public double Progress { get; internal set; }

	public Task<CompletionInfo> RenderTask { get; }

	private readonly CancellationTokenSource cancellation;
	private readonly uint timer_id;

	internal RenderHandle (
		Task<CompletionInfo> task,
		CancellationTokenSource cts,
		uint timerId)
	{
		RenderTask = task;
		cancellation = cts;
		timer_id = timerId;
	}

	public void Cancel ()
	{
		cancellation.Cancel ();
	}

	private void Dispose (bool disposing)
	{
		cancellation.Dispose ();

		if (timer_id > 0)
			GLib.Source.Remove (timer_id);
	}

	void IDisposable.Dispose ()
	{
		Dispose (disposing: true);
	}

	~RenderHandle ()
	{
		Dispose (disposing: false);
	}
}

internal readonly record struct CompletionInfo (
	bool WasCanceled,
	IReadOnlyList<Exception> Errors);
