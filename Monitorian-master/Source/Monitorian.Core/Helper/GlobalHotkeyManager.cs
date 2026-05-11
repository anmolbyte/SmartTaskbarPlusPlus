using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Monitorian.Core.Helper;

/// <summary>
/// Manages global hotkey registration and message dispatch using Win32 RegisterHotKey.
/// </summary>
public class GlobalHotkeyManager : IDisposable
{
	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

	public const uint MOD_ALT = 0x0001;
	public const uint MOD_CONTROL = 0x0002;
	public const uint MOD_SHIFT = 0x0004;
	public const uint MOD_WIN = 0x0008;
	public const uint MOD_NOREPEAT = 0x4000;

	private const int WM_HOTKEY = 0x0312;

	private readonly IntPtr _hWnd;
	private readonly HwndSource _source;
	private readonly Dictionary<int, Action> _hotkeys = new();
	private int _currentId = 0;

	public GlobalHotkeyManager(Window window)
	{
		var helper = new WindowInteropHelper(window);
		_hWnd = helper.EnsureHandle();
		_source = HwndSource.FromHwnd(_hWnd);
		_source?.AddHook(HwndHook);
	}

	public int Register(uint modifiers, uint virtualKey, Action action)
	{
		int id = ++_currentId;
		if (RegisterHotKey(_hWnd, id, modifiers, virtualKey))
		{
			_hotkeys[id] = action;
			return id;
		}
		return -1;
	}

	public void Unregister(int id)
	{
		if (_hotkeys.ContainsKey(id))
		{
			UnregisterHotKey(_hWnd, id);
			_hotkeys.Remove(id);
		}
	}

	public void UnregisterAll()
	{
		foreach (var id in _hotkeys.Keys)
		{
			UnregisterHotKey(_hWnd, id);
		}
		_hotkeys.Clear();
	}

	private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (msg == WM_HOTKEY)
		{
			int id = wParam.ToInt32();
			if (_hotkeys.TryGetValue(id, out var action))
			{
				action?.Invoke();
				handled = true;
			}
		}
		return IntPtr.Zero;
	}

	public void Dispose()
	{
		UnregisterAll();
		_source?.RemoveHook(HwndHook);
		_source?.Dispose();
	}
}
