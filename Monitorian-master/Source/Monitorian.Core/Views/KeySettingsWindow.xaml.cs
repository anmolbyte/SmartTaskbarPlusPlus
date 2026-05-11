using System;
using System.Windows;
using Monitorian.Core.ViewModels;

namespace Monitorian.Core.Views;

public partial class KeySettingsWindow : Window
{
	private readonly AppControllerCore _controller;

	public KeySettingsWindow(AppControllerCore controller)
	{
		InitializeComponent();
		this._controller = controller;
		this.DataContext = controller;
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		this.Close();
	}
}
