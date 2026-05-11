using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Monitorian.Core.Models;

namespace Monitorian.Core.Views.Controls;

public class HotkeyTextBox : TextBox
{
	public static readonly DependencyProperty HotkeyProperty =
		DependencyProperty.Register(nameof(Hotkey), typeof(HotkeyItem), typeof(HotkeyTextBox),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

	public HotkeyItem Hotkey
	{
		get => (HotkeyItem)GetValue(HotkeyProperty);
		set => SetValue(HotkeyProperty, value);
	}

	static HotkeyTextBox()
	{
		DefaultStyleKeyProperty.OverrideMetadata(typeof(HotkeyTextBox), new FrameworkPropertyMetadata(typeof(HotkeyTextBox)));
	}

	public HotkeyTextBox()
	{
		IsReadOnly = true;
		IsReadOnlyCaretVisible = false;
		Cursor = Cursors.Hand;
		TextAlignment = TextAlignment.Center;
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		e.Handled = true;

		var key = e.Key == Key.System ? e.SystemKey : e.Key;

		// Ignore modifier-only presses
		if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
				   Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
		{
			return;
		}

		if (key == Key.Back || key == Key.Delete)
		{
			Hotkey = new HotkeyItem();
			Text = string.Empty;
			return;
		}

		uint modifiers = 0;
		if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= 0x0002;
		if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= 0x0001;
		if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= 0x0004;
		if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= 0x0008;

		var hotkey = new HotkeyItem(modifiers, (uint)KeyInterop.VirtualKeyFromKey(key));
		Hotkey = hotkey;
		Text = hotkey.ToString();
	}

	protected override void OnTextChanged(TextChangedEventArgs e)
	{
		if (Hotkey != null && string.IsNullOrEmpty(Text))
		{
			Text = Hotkey.ToString();
		}
		base.OnTextChanged(e);
	}
}
