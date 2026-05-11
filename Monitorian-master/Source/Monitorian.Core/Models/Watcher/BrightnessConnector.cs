using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Monitorian.Core.Models.Watcher;

public class BrightnessConnector : IDisposable
{
	public float Interval { get; set; } = 0.5F;
	public static IReadOnlyCollection<string> Options => new string[] { "/connect" };
	public virtual bool IsEnabled => false;

	public BrightnessConnector() { }

	public virtual Task InitiateAsync(Action<int> onBrightnessChanged, Action<string> onError, Func<bool> onContinue)
	{
		return Task.CompletedTask;
	}

	public virtual Task<bool> OpenAsync() => Task.FromResult(false);
	public virtual Task<bool> ConnectAsync(bool isMultiple) => Task.FromResult(false);

	public void Dispose() { }
}