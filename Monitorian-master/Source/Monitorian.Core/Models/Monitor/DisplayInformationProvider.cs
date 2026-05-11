using System;
using System.Runtime.Serialization;
using Monitorian.Core.Helper;

namespace Monitorian.Core.Models.Monitor;

internal static class DisplayInformationProvider
{
	[DataContract]
	public class DisplayItem
	{
		[DataMember(Order = 0)]
		public bool IsHighDynamicRangeSupported { get; private set; }

		[DataMember(Order = 1)]
		public string AdvancedColorKind { get; private set; }

		[DataMember(Order = 2)]
		public string SdrWhiteLevel { get; private set; }

		[DataMember(Order = 3)]
		public string MinLuminance { get; private set; }

		[DataMember(Order = 4)]
		public string MaxLuminance { get; private set; }

		public DisplayItem(IntPtr monitorHandle) { }
	}

	public static event EventHandler<(string deviceInstanceId, float sdrWhiteLevel)> AdvancedColorInfoChanged { add { } remove { } }

	public static void RegisterMonitor(string deviceInstanceId, IntPtr monitorHandle) { }
	public static void UnregisterMonitor(string deviceInstanceId) { }
	public static void ClearMonitors() { }
	public static (AccessResult result, float sdrWhiteLevel) GetSdrWhiteLevel(string deviceInstanceId) => (AccessResult.NotSupported, -1);
	public static (bool isHdr, float sdrWhiteLevel) IsHdrAndGetSdrWhiteLevel(IntPtr monitorHandle) => (false, -1);
	public static void EnsureDispatcherQueue() { }
}