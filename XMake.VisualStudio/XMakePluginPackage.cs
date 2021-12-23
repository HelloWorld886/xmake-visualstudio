using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace XMake.VisualStudio
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(XMakePluginPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideToolWindow(typeof(XMake.VisualStudio.XMakeToolWindow))]
    [ComVisible(true)]
    public sealed class XMakePluginPackage : AsyncPackage
    {
        /// <summary>
        /// XMakeCommandPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "f015d5ab-25d8-4ed3-8a3c-38fce53a0baf";

        /// <summary>
        /// Initializes a new instance of the <see cref="XMakePluginPackage"/> class.
        /// </summary>
        public XMakePluginPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            AddService(typeof(XMakeService), CreateXMakeAsync);
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await XMakeCommand.InitializeAsync(this);
        }

        private async Task<object> CreateXMakeAsync(IAsyncServiceContainer container, CancellationToken cancellationToken, Type serviceType)
        {
            var svc = new XMakeService();
            await svc.InitializeAsync(this, cancellationToken);
            return svc;
        }

        protected override async Task OnAfterPackageLoadedAsync(CancellationToken cancellationToken)
        {
            XMakeService service = await GetServiceAsync(typeof(XMakeService)) as XMakeService;
            if (service != null)
                await service.OnAfterPackageLoadedAsync(cancellationToken);

            await base.OnAfterPackageLoadedAsync(cancellationToken);

        }

        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType)
        {
            return toolWindowType.Equals(Guid.Parse(XMakeToolWindow.WindowGuidString)) ? this : null;
        }

        protected override string GetToolWindowTitle(Type toolWindowType, int id)
        {
            return toolWindowType == typeof(XMakeToolWindow) ? XMakeToolWindow.Title : base.GetToolWindowTitle(toolWindowType, id);
        }

        protected override async Task<object> InitializeToolWindowAsync(Type toolWindowType, int id, CancellationToken cancellationToken)
        {
            XMakeService service = await GetServiceAsync(typeof(XMakeService)) as XMakeService;
            return service;
        }

        #endregion
    }
}
