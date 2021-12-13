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
    public sealed class XMakePluginPackage : AsyncPackage, IVsSolutionEvents7, IVsSolutionEvents
    {
        /// <summary>
        /// XMakeCommandPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "f015d5ab-25d8-4ed3-8a3c-38fce53a0baf";

        private IVsOutputWindowPane _vsOutputWindow;

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
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await XMakeCommand.InitializeAsync(this);
        }

        public void BeginPrint()
        {
            DTE dte = (DTE)GetService(typeof(DTE));
            Window outputWindow = dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
            outputWindow.Activate();

            IVsOutputWindow vso = GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            Assumes.Present(vso);
            Guid paneGuid = new Guid("f015d5ab-25d8-4ed3-8a3c-38fce53a0baf");
            vso.CreatePane(ref paneGuid, "XMake", 1, 1);

            vso.GetPane(ref paneGuid, out _vsOutputWindow);
            _vsOutputWindow.Activate();
        }

        public void Print(string message)
        {
            if(_vsOutputWindow != null)
                _vsOutputWindow.OutputString(message + "\r\n");
        }

        private void Init(string folderPath)
        {
            string[] files = Directory.GetFiles(folderPath, "xmake.lua", SearchOption.AllDirectories).Where(a => !a.Contains(".xmake")).ToArray();
            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    FileInfo fileInfo = new FileInfo(file);
                    XMakePlugin.ProjectDir = fileInfo.Directory.FullName;
                    XMakePlugin.LoadConfig();
                    XMakePlugin.BeginOutput += BeginPrint;
                    XMakePlugin.Output += Print;

                    XMakeToolWindow window = FindToolWindow(typeof(XMakeToolWindow), 0, true) as XMakeToolWindow;
                    IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
                    if (windowFrame.IsVisible() == 0)
                    {
                        XMakeToolWindowControl control = (XMakeToolWindowControl)window.Content;
                        control.SetEnable(true);
                        control.Reset();
                    }
                    break;
                }
            }
        }

        protected override Task OnAfterPackageLoadedAsync(CancellationToken cancellationToken)
        {
            IVsSolution solution = GetService(typeof(SVsSolution)) as IVsSolution;
            solution.GetSolutionInfo(out string directory, out string solutionFile, out string optsFile);
            if(directory == null)
            {
                solution.AdviseSolutionEvents(this, out uint cookie);
            }
            else if(Directory.Exists(solutionFile) && Directory.Exists(directory))
            {
                Init(directory);
            }


            return base.OnAfterPackageLoadedAsync(cancellationToken);
        }

        public void OnAfterOpenFolder(string folderPath)
        {
            Init(folderPath);
        }

        public void OnBeforeCloseFolder(string folderPath)
        {
        }

        public void OnQueryCloseFolder(string folderPath, ref int pfCancel)
        {
        }

        public void OnAfterCloseFolder(string folderPath)
        {
        }

        public void OnAfterLoadAllDeferredProjects()
        {
        }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            return 0;
        }

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
        {
            return 0;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            return 0;
        }

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            return 0;
        }

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
        {
            return 0;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            return 0;
        }

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            return 0;
        }

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
        {
            return 0;
        }

        public int OnBeforeCloseSolution(object pUnkReserved)
        {
            return 0;
        }

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            return 0;
        }

        #endregion
    }
}
