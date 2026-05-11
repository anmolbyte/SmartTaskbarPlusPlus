using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Windows.Input;

namespace Monitorian.Core.Models;

[DataContract]
public class HotkeyItem
{
	[DataMember]
	public uint Modifiers { get; set; }

	[DataMember]
	public uint Key { get; set; }

	public HotkeyItem() { }

	public HotkeyItem(uint modifiers, uint key)
	{
		Modifiers = modifiers;
		Key = key;
	}

	public override string ToString()
	{
		if (Key == 0) return string.Empty;

		var sb = new StringBuilder();
		if ((Modifiers & 0x0002) != 0) sb.Append("Ctrl + "); // MOD_CONTROL
		if ((Modifiers & 0x0001) != 0) sb.Append("Alt + ");  // MOD_ALT
		if ((Modifiers & 0x0004) != 0) sb.Append("Shift + "); // MOD_SHIFT
		if ((Modifiers & 0x0008) != 0) sb.Append("Win + ");   // MOD_WIN

		sb.Append(((Key)Key).ToString());
		return sb.ToString();
	}

	public bool IsEmpty => Key == 0;
}
