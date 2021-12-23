using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;

namespace XMake.VisualStudio
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid(WindowGuidString)]
    public class XMakeToolWindow : ToolWindowPane
    {
        public const string WindowGuidString = "2d408b97-42a2-4227-8e28-4a28bd1b2da2"; // Replace with new GUID in your own code
        public const string Title = "XMake Tool Window";

        private XMakeService _service;
        private XMakeToolWindowControl _control;


        /// <summary>
        /// Initializes a new instance of the <see cref="XMakeToolWindow"/> class.
        /// </summary>
        public XMakeToolWindow(XMakeService service) : base(null)
        {
            this.Caption = "XMakeToolWindow";
            _service = service;

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            _control = new XMakeToolWindowControl();
            Content = _control;

            _control.platChanged += OnPlatChanged;
            _control.archChanged += OnArchChanged;
            _control.modeChanged += OnModeChanged;
            _control.targetChanged += OnTargetChanged;
            _control.build += Build;
            _control.quickStart = QuickStart;
            _control.run = Run;
            _control.clean = Clean;
            _control.cleanConfig = CleanConfig;
            _control.updateIntellisense = UpdateIntellisense;
            for (int i = 0; i < _service.AllModes.Length; i++)
            {
                _control.ModeComboBox.Items.Add(_service.AllModes[i]);
            }

            for (int i = 0; i < _service.AllArchs.Length; i++)
            {
                _control.ArchComboBox.Items.Add(_service.AllArchs[i]);
            }

            for (int i = 0; i < _service.AllPlats.Length; i++)
            {
                _control.PlatformComboBox.Items.Add(_service.AllPlats[i]);
            }

            RefreshEnable();
            RefreshConfig();
            RefreshTarget();
        }

        public void RefreshEnable()
        {
            _control.IsEnabled = !string.IsNullOrEmpty(_service.ProjDir);
        }

        public void RefreshConfig()
        {
            _control.PlatformComboBox.SelectedItem = _service.Plat;
            _control.ArchComboBox.SelectedItem = _service.Arch;
            _control.ModeComboBox.SelectedItem = _service.Mode;
        }

        public void RefreshTarget()
        {
            _control.TargetComboBox.Items.Clear();

            for (int i = 0; i < _service.AllTargets.Count; i++)
            {
                _control.TargetComboBox.Items.Add(_service.AllTargets[i]);
            }

            _control.TargetComboBox.SelectedItem = _service.Target;
        }

        private void UpdateIntellisense()
        {
            throw new NotImplementedException();
        }

        private void CleanConfig()
        {
            _service.CleanConfig();
        }

        private void Clean()
        {
            _service.Clean();
        }

        private void Run()
        {
            _service.Run();
        }

        private void QuickStart()
        {
            _service.QuickStart();
        }

        private void Build()
        {
            _service.Build();
        }

        private void OnTargetChanged(string obj)
        {
            bool change = _service.Plat != obj;
            _service.Target = obj;
            if (change)
                _service.UpdateConfig();
        }

        private void OnModeChanged(string obj)
        {
            bool change = _service.Plat != obj;
            _service.Mode = obj;
            if (change)
                _service.UpdateConfig();
        }

        private void OnArchChanged(string obj)
        {
            bool change = _service.Plat != obj;
            _service.Arch = obj;
            if (change)
                _service.UpdateConfig();
        }

        private void OnPlatChanged(string obj)
        {
            bool change = _service.Plat != obj;
            _service.Plat = obj;
            if (change)
                _service.UpdateConfig();
        }
    }
}
