using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace Monitorian.Core.Models.Monitor;

internal class DisplayMonitorProvider
{
	[DataContract]
	public class DisplayItem
	{
		[DataMember(Order = 0)]
		public string DeviceInstanceId { get; }

		[DataMember(Order = 1)]
		public string DisplayName { get; }

		[DataMember(Order = 2)]
		public ConnectionType Connection { get; }

		[DataMember(Order = 3)]
		public bool IsInternal { get; }

		[DataMember(Order = 4)]
		public Size NativeResolution { get; }

		[DataMember(Order = 5)]
		public Size PhysicalSize { get; }

		[DataMember(Order = 6)]
		public float PhysicalDiagonalLength { get; }

		public DisplayItem(string deviceInstanceId, string displayName)
		{
			this.DeviceInstanceId = deviceInstanceId;
			this.DisplayName = displayName;
		}
	}

	public static Task<DisplayItem[]> GetDisplayMonitorsAsync() => Task.FromResult(Array.Empty<DisplayItem>());
}