using System;

namespace StartupAgency.Worker;

/// <summary>
/// Startup task (AppX) worker (Stubbed)
/// </summary>
internal class BridgeWorker : IStartupWorker
{
	public BridgeWorker(string taskId)
	{
	}

	public bool? IsStartedOnSignIn() => false;

	public bool CanRegister() => false;

	public bool IsRegistered() => false;

	public bool Register() => false;

	public void Unregister()
	{
	}
}