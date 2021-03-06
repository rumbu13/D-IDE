﻿using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using D_IDE.Core;
using AvalonDock;

namespace D_IDE.Controls.Panels
{
	/// <summary>
	/// Interaktionslogik für ListPanel.xaml
	/// </summary>
	public partial class ErrorListPanel : DockableContent
	{
		public ErrorListPanel()
		{
			Name = "ErrorListPanel";
			DataContext = this;
			InitializeComponent();
		}

		public void RefreshErrorList()
		{
			Dispatcher.Invoke(new EventHandler(delegate(object o,EventArgs e){
			var selIndex = MainList.SelectedIndex;

			MainList.ItemsSource = IDEManager.ErrorManagement.Errors;

			if (MainList.Items.Count > selIndex)
				MainList.SelectedIndex = selIndex;
			}),null,null);
		}

		private void MainList_MouseDown(object sender, MouseButtonEventArgs e)
		{
			var item=e.OriginalSource as FrameworkElement;
			var err=item.DataContext as GenericError;

			if (err == null || string.IsNullOrEmpty( err.FileName) || !File.Exists(err.FileName))
				return;

			IDEManager.Instance.OpenFile(err.FileName, err.Line, err.Column);
		}
	}
}
