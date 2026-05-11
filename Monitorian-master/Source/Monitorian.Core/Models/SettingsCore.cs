using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading.Tasks;

using Monitorian.Core.Collections;
using Monitorian.Core.Common;
using Monitorian.Core.Helper;
using Monitorian.Core.Views;

namespace Monitorian.Core.Models;

/// <summary>
/// Settings
/// </summary>
[DataContract]
public class SettingsCore : BindableBase
{
	#region Settings (persistent)

	/// <summary>
	/// Whether to use large elements
	/// </summary>
	[DataMember]
	public bool UsesLargeElements
	{
		get => _usesLargeElements;
		set => SetProperty(ref _usesLargeElements, value);
	}
	private bool _usesLargeElements = true; // Default

	/// <summary>
	/// Whether to use accent color for brightness
	/// </summary>
	[DataMember]
	public bool UsesAccentColor
	{
		get => _usesAccentColor;
		set => SetProperty(ref _usesAccentColor, value);
	}
	private bool _usesAccentColor;

	/// <summary>
	/// Whether to show adjusted brightness
	/// </summary>
	[DataMember]
	public bool ShowsAdjusted
	{
		get => _showsAdjusted;
		set => SetProperty(ref _showsAdjusted, value);
	}
	private bool _showsAdjusted = true; // default

	/// <summary>
	/// Whether to sort by monitor arrangement
	/// </summary>
	[DataMember]
	public bool SortsArrangement
	{
		get => _sortsArrangement;
		set => SetProperty(ref _sortsArrangement, value);
	}
	private bool _sortsArrangement = true; // default

	/// <summary>
	/// Whether to defer change until stopped
	/// </summary>
	[DataMember]
	public bool DefersChange
	{
		get => _defersChange;
		set => SetProperty(ref _defersChange, value);
	}
	private bool _defersChange;

	/// <summary>
	/// Whether to adjust SDR content brightness
	/// </summary>
	[DataMember]
	public bool AdjustsSdrContent
	{
		get => _adjustsSdrContent;
		set => SetProperty(ref _adjustsSdrContent, value);
	}
	private bool _adjustsSdrContent;

	/// <summary>
	/// Whether to invert scroll direction
	/// </summary>
	/// <remarks>
	/// This value is a set of flags.
	/// </remarks>
	public ScrollInput InvertsScrollDirection
	{
		get => _invertsScrollDirection ??= (ScrollInput)0b_1110; // default
		set => SetProperty(ref _invertsScrollDirection, value);
	}
	[DataMember(Name = nameof(InvertsScrollDirection))]
	private ScrollInput? _invertsScrollDirection;

	/// <summary>
	/// Whether to enable moving in unison
	/// </summary>
	[DataMember]
	public bool EnablesUnison
	{
		get => _enablesUnison;
		set => SetProperty(ref _enablesUnison, value);
	}
	private bool _enablesUnison;

	/// <summary>
	/// Whether to enable changing adjustable range
	/// </summary>
	[DataMember]
	public bool EnablesRange
	{
		get => _enablesRange;
		set => SetProperty(ref _enablesRange, value);
	}
	private bool _enablesRange;

	/// <summary>
	/// Whether to enable changing contrast
	/// </summary>
	[DataMember]
	public bool EnablesContrast
	{
		get => _enablesContrast;
		set => SetProperty(ref _enablesContrast, value);
	}
	private bool _enablesContrast;

	/// <summary>
	/// Whether to enable showing monitor identity
	/// </summary>
	[DataMember]
	public bool EnablesIdentity
	{
		get => _enablesIdentity;
		set => SetProperty(ref _enablesIdentity, value);
	}
	private bool _enablesIdentity = true;

	/// <summary>
	/// Monitor customizations by user
	/// </summary>
	[DataMember]
	public ObservableKeyedList<string, MonitorCustomizationItem> MonitorCustomizations
	{
		get => _monitorCustomizations ??= new ObservableKeyedList<string, MonitorCustomizationItem>();
		protected set => _monitorCustomizations = value;
	}
	private ObservableKeyedList<string, MonitorCustomizationItem> _monitorCustomizations;

	/// <summary>
	/// Device Instance ID of selected monitor
	/// </summary>
	[DataMember]
	public string SelectedDeviceInstanceId
	{
		get => _selectedDeviceInstanceId;
		set => SetProperty(ref _selectedDeviceInstanceId, value);
	}
	private string _selectedDeviceInstanceId;

	/// <summary>
	/// Whether to record operations to log
	/// </summary>
	[DataMember]
	public bool RecordsOperationLog
	{
		get => _recordsOperationLog;
		set => SetProperty(ref _recordsOperationLog, value);
	}
	private bool _recordsOperationLog;

	/// <summary>
	/// Whether to restore brightness on reconnection
	/// </summary>
	[DataMember]
	public bool RestoresBrightnessOnReconnection
	{
		get => _restoresBrightnessOnReconnection;
		set => SetProperty(ref _restoresBrightnessOnReconnection, value);
	}
	private bool _restoresBrightnessOnReconnection = true;

	/// <summary>
	/// Whether to enable global hotkeys
	/// </summary>
	[DataMember]
	public bool EnableHotkeys
	{
		get => _enableHotkeys;
		set => SetProperty(ref _enableHotkeys, value);
	}
	private bool _enableHotkeys = true;

	[DataMember] public HotkeyItem ShowHotkey { get; set; } = new();
	[DataMember] public HotkeyItem ShowAtCursorHotkey { get; set; } = new();

	[DataMember] public HotkeyItem BrightenHotkey { get; set; } = new();
	[DataMember] public HotkeyItem BrightenAllHotkey { get; set; } = new();
	[DataMember] public HotkeyItem DarkenHotkey { get; set; } = new();
	[DataMember] public HotkeyItem DarkenAllHotkey { get; set; } = new();
	[DataMember] public HotkeyItem MoveUpHotkey { get; set; } = new();
	[DataMember] public HotkeyItem MoveDownHotkey { get; set; } = new();
	[DataMember] public HotkeyItem ShowForMomentHotkey { get; set; } = new();

	[DataMember] public HotkeyItem LocalBrightenHotkey { get; set; } = new();
	[DataMember] public HotkeyItem LocalDarkenHotkey { get; set; } = new();
	[DataMember] public HotkeyItem LocalMoveUpHotkey { get; set; } = new();
	[DataMember] public HotkeyItem LocalMoveDownHotkey { get; set; } = new();

	[DataMember] public int ChangePerKeyStroke { get; set; } = 4;
	[DataMember] public int ChangePerWheelRoll { get; set; } = 3;

	[DataMember] public HotkeyItem ToBrightnessHotkey { get; set; } = new();
	[DataMember] public HotkeyItem ToContrastHotkey { get; set; } = new();

	[DataMember] public HotkeyItem OpenDisplaySettingsHotkey { get; set; } = new();
	[DataMember] public HotkeyItem TurnOffDisplayHotkey { get; set; } = new();
	[DataMember] public HotkeyItem ChangeColorTemperatureHotkey { get; set; } = new();

	#endregion

	protected Type[] KnownTypes { get; set; }

	private const string SettingsFileName = "settings.xml";

	protected string FileName
	{
		get => _fileName;
		set
		{
			if (!string.IsNullOrWhiteSpace(value))
				_fileName = value;
		}
	}
	private string _fileName = SettingsFileName;

	public SettingsCore()
	{ }

	private Throttle _save;

	protected internal virtual async Task InitiateAsync()
	{
		await Task.Run(() => Load(this));

		_save = new Throttle(
			TimeSpan.FromMilliseconds(100),
			() => Save(this));

		MonitorCustomizations.CollectionChanged += (_, _) => OnPropertyChanged(nameof(MonitorCustomizations));
		PropertyChanged += async (_, _) => await _save.PushAsync();
	}

	#region Load/Save

	private void Load<T>(T instance) where T : class
	{
		try
		{
			AppDataService.Load(instance, FileName, KnownTypes);
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Failed to load settings from AppData." + Environment.NewLine
				+ ex);
		}
	}

	private void Save<T>(T instance) where T : class
	{
		try
		{
			AppDataService.Save(instance, FileName, KnownTypes);
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Failed to save settings to AppData." + Environment.NewLine
				+ ex);
		}
	}

	#endregion
}

[DataContract]
public class MonitorCustomizationItem
{
	[DataMember]
	public string Name { get; private set; }

	[DataMember(Name = "Unison")]
	public bool IsUnison { get; private set; }

	[DataMember]
	public byte Lowest { get; private set; } = 0;

	[DataMember]
	public byte Highest { get; private set; } = 100;

	[DataMember]
	public HotkeyItem BrightenHotkey { get; set; } = new();

	[DataMember]
	public HotkeyItem DarkenHotkey { get; set; } = new();

	[DataMember]
	public HotkeyItem CycleInputHotkey { get; set; } = new();

	public MonitorCustomizationItem(string name, bool isUnison, byte lowest, byte highest)
	{
		this.Name = name;
		this.IsUnison = isUnison;
		this.Lowest = lowest;
		this.Highest = highest;
	}

	public MonitorCustomizationItem(string name, bool isUnison, byte lowest, byte highest, HotkeyItem brighten, HotkeyItem darken, HotkeyItem cycle)
		: this(name, isUnison, lowest, highest)
	{
		this.BrightenHotkey = brighten;
		this.DarkenHotkey = darken;
		this.CycleInputHotkey = cycle;
	}

	internal bool IsValid
	{
		get => (Lowest < Highest) && (Highest <= 100);
	}

	internal bool IsDefault
	{
		get => (Name is null)
			&& (IsUnison == default)
			&& (Lowest, Highest) is (0, 100);
	}
}