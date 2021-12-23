using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
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

        internal Action<string> modeChanged;
        internal Action<string> platChanged;
        internal Action<string> archChanged;
        internal Action<string> targetChanged;
        internal Action quickStart;
        internal Action build;
        internal Action run;
        internal Action clean;
        internal Action cleanConfig;
        internal Action updateIntellisense;

        /// <summary>
        /// Initializes a new instance of the <see cref="XMakeToolWindowControl"/> class.
        /// </summary>
        public XMakeToolWindowControl()
        {
            this.InitializeComponent();
        }

        private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModeComboBox.SelectedItem == null)
                return;

            modeChanged.Invoke(ModeComboBox.SelectedItem.ToString());
        }

        private void PlatformComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlatformComboBox.SelectedItem == null)
                return;

            platChanged.Invoke(PlatformComboBox.SelectedItem.ToString());
        }

        private void ArchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ArchComboBox.SelectedItem == null)
                return;

            archChanged.Invoke(ArchComboBox.SelectedItem.ToString());
        }

        private void TargetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetComboBox.SelectedItem == null)
                return;

            targetChanged.Invoke(TargetComboBox.SelectedItem.ToString());
        }

        private void Build_Click(object sender, RoutedEventArgs e)
        {
            build.Invoke();
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            run.Invoke();
        }

        private void Clean_Click(object sender, RoutedEventArgs e)
        {
            clean.Invoke();
        }

        private void CleanConfig_Click(object sender, RoutedEventArgs e)
        {
            cleanConfig.Invoke();
        }

        private void Intellisense_Click(object sender, RoutedEventArgs e)
        {
            updateIntellisense.Invoke();
        }

        private void QuickStart_Click(object sender, RoutedEventArgs e)
        {
            quickStart.Invoke();
        }


    }
}