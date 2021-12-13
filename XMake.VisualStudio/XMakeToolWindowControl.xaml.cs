﻿using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace XMake.VisualStudio
{
    /// <summary>
    /// Interaction logic for XMakeToolWindowControl.
    /// </summary>
    public partial class XMakeToolWindowControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XMakeToolWindowControl"/> class.
        /// </summary>
        public XMakeToolWindowControl(XMakePluginPackage package)
        {
            this.InitializeComponent();
            SetEnable(!string.IsNullOrEmpty(XMakePlugin.ProjectDir));


            foreach (var item in XMakePlugin.Platforms)
            {
                PlatformComboBox.Items.Add(item);
            }

            foreach (var item in XMakePlugin.Archs)
            {
                ArchComboBox.Items.Add(item);
            }

            foreach (var item in XMakePlugin.Modes)
            {
                ModeComboBox.Items.Add(item);
            }

            Reset();
        }

        public void SetEnable(bool isEnable)
        {
            RootNode.IsEnabled = isEnable;
        }

        public void Reset()
        {
            PlatformComboBox.SelectedItem = XMakePlugin.GetOption("platform");
            ArchComboBox.SelectedItem = XMakePlugin.GetOption("arch");
            ModeComboBox.SelectedItem = XMakePlugin.GetOption("mode");
        }

        private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XMakePlugin.SetOption("mode", ModeComboBox.SelectedItem.ToString());
        }

        private void PlatformComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XMakePlugin.SetOption("platform", PlatformComboBox.SelectedItem.ToString());
        }

        private void ArchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XMakePlugin.SetOption("arch", ArchComboBox.SelectedItem.ToString());
        }

        private void Build_Click(object sender, RoutedEventArgs e)
        {
            XMakePlugin.Build();
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            XMakePlugin.Run();
        }

        private void Clean_Click(object sender, RoutedEventArgs e)
        {
            XMakePlugin.Clean();
        }

        private void CleanConfig_Click(object sender, RoutedEventArgs e)
        {
            XMakePlugin.CleanConfig();
        }

        private void CMake_Click(object sender, RoutedEventArgs e)
        {
            XMakePlugin.UpdateCMake();
        }

        private void QuickStart_Click(object sender, RoutedEventArgs e)
        {
            XMakePlugin.QuickStart();
        }
    }
}