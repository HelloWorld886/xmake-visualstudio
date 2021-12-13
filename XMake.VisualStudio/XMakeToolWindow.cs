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
    [Guid("2d408b97-42a2-4227-8e28-4a28bd1b2da2")]
    public class XMakeToolWindow : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XMakeToolWindow"/> class.
        /// </summary>
        public XMakeToolWindow() : base(null)
        {
            this.Caption = "XMakeToolWindow";


            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new XMakeToolWindowControl((XMakePluginPackage)Package);
        }
    }
}
