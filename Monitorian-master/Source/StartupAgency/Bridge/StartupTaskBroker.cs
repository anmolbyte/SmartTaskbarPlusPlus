using System;

namespace StartupAgency.Bridge;

/// <summary>
/// Startup task broker (Stubbed)
/// </summary>
public static class StartupTaskBroker
{
	public static bool CanEnable(string taskId) => false;
	public static bool IsEnabled(string taskId) => false;
	public static bool Enable(string taskId) => false;
	public static void Disable(string taskId) { }
}