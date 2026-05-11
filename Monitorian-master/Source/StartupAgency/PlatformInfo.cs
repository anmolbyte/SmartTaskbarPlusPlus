using System;

namespace StartupAgency;

/// <summary>
/// Platform information
/// </summary>
public static class PlatformInfo
{
	/// <summary>
	/// Whether this assembly is packaged in AppX package
	/// </summary>
	public static bool IsPackaged => false;
}