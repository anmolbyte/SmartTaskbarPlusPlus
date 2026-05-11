using System;

namespace Monitorian.Core.Models.Sensor;

public static class LightSensor
{
	public class WinRTLightSensor
	{
		public static bool AmbientLightSensorExists() => false;
		public static bool TryGetAmbientLight(out float illuminance)
		{
			illuminance = default;
			return false;
		}
		public static TimeSpan ReportInterval { get; set; }
		public static event EventHandler<float> AmbientLightChanged { add { } remove { } }
	}

	public static bool AmbientLightSensorExists => false;
}