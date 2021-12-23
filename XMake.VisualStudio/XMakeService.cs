using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XMake.VisualStudio
{
    [ComVisible(true)]
    public class XMakeService : IVsSolutionEvents7, IVsSolutionEvents
    {
        private XMakePluginPackage _package;

        private List<string> _allTargets = new List<string>() { "default" };
        private string[] _allModes = new string[]
            {
                "debug",
                "release"
            };
        private string[] _allPlats = new string[]
            {
                "windows",
            };
        private string[] _allArchs = new string[]
            {
                "x86",
                "x64"
            };

        private string _projDir;
        private string _target;
        private string _mode;
        private string _plat;
        private string _arch;

        public string ProjDir 
        { 
            get => _projDir;
        }

        public string Target 
        { 
            get => _target; 
            set => _target = value;
        }
        public string[] AllModes { get => _allModes; }

        public string[] AllPlats { get => _allPlats; }

        public string[] AllArchs { get => _allArchs; }

        public IReadOnlyList<string> AllTargets { get => _allTargets; }

        public string Mode { get => _mode; set => _mode = value; }

        public string Plat { get => _plat; set => _plat = value; }

        public string Arch { get => _arch; set => _arch = value; }


        public void QuickStart()
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await RunCommandAsync("f -y");
                await RefreshTargetAsync();
            });
        }

        public void Build()
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                if (string.IsNullOrEmpty(_target))
                    return;

                string command = "build -v -y";
                if (_target != "default")
                    command += " " + _target;
                else
                    command += " -a";
                await RunCommandAsync(command);
                await RefreshTargetAsync();
            });
        }

        public void Run()
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                if (string.IsNullOrEmpty(_target))
                    return;

                string command = "r";
                if (_target != "default")
                    command += " " + _target;
                await RunCommandAsync(command);
            });
        }

        public void Clean()
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                string command = "c";
                if (_target != "default")
                    command += " " + _target;

                await RunCommandAsync(command);
                await RefreshAllAsync();
            });
        }

        public void CleanConfig()
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await RunCommandAsync("f -c -y");
                await RefreshAllAsync();
            });
        }

        public void UpdateConfig()
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await RunCommandAsync(string.Format("f -p -y {0} -a {1} -m {2}", _plat, _arch, _mode));
            });
        }

        public async Task InitializeAsync(XMakePluginPackage package, CancellationToken cancellationToken)
        {
            _package = package;
        }

        public async Task OnAfterPackageLoadedAsync(CancellationToken cancellationToken)
        {
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync();

            IVsSolution solution = await _package.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            if (solution != null)
            {
                solution.GetSolutionInfo(out string directory, out string solutionFile, out string optsFile);
                if (directory == null)
                {
                    solution.AdviseSolutionEvents(this, out uint cookie);
                }
                else if (Directory.Exists(solutionFile) && Directory.Exists(directory))
                {
                    await InitProjectAsync(directory);
                }
            }
        }

        private async Task InitProjectAsync(string folderPath)
        {
            string[] files = Directory.GetFiles(folderPath, "xmake.lua", SearchOption.AllDirectories).Where(a => !a.Contains(".xmake")).ToArray();
            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    FileInfo fileInfo = new FileInfo(file);
                    _projDir = fileInfo.Directory.FullName;

                    await RefreshAllAsync();
                    break;
                }
            }
        }

        private void LoadConfig()
        {
            if (string.IsNullOrEmpty(_projDir))
                return;

            string[] cache = null;
            using (var proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.WorkingDirectory = _projDir;
                proc.StartInfo.FileName = "xmake";
                proc.StartInfo.Arguments = "l -c \"import(\\\"core.project.config\\\"); config.load(); print(\\\"$(plat) $(arch) $(mode)\\\")\"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
                proc.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
                if (!proc.Start())
                    return;

                string result = proc.StandardOutput.ReadToEnd();

                if (!string.IsNullOrEmpty(result))
                {
                    Regex regex = new Regex("\u001B\\[[;\\d]*m");
                    result = regex.Replace(result, "");
                    cache = result.Trim().Split(' ');
                }

                string platform = cache != null && cache.Length > 0 && !string.IsNullOrEmpty(cache[0]) ? cache[0] : null;
                if (!string.IsNullOrEmpty(platform))
                    _plat = platform;
                else
                    _plat = _allPlats[0];

                string arch = cache != null && cache.Length > 1 && !string.IsNullOrEmpty(cache[1]) ? cache[1] : null;
                if (!string.IsNullOrEmpty(arch))
                    _arch = arch;
                else
                    _arch = _allArchs[0];

                string mode = cache != null && cache.Length > 2 && !string.IsNullOrEmpty(cache[2]) ? cache[2] : null;
                if (!string.IsNullOrEmpty(mode))
                    _mode = mode;
                else
                    _mode = _allModes[0];
            }
        }

        private void LoadTarget()
        {
            if (string.IsNullOrEmpty(_projDir))
                return;

            _allTargets.Clear();
            _allTargets.Add("default");
            using (var proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.WorkingDirectory = _projDir;
                proc.StartInfo.FileName = "xmake";
                proc.StartInfo.Arguments = "l -c \"import(\\\"core.project.config\\\"); import(\\\"core.project.project\\\"); config.load(); for name, _ in pairs((project.targets())) do print(name) end\"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
                proc.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
                if (!proc.Start())
                    return;

                string result = proc.StandardOutput.ReadToEnd();
                if (!string.IsNullOrEmpty(result))
                {
                    Regex regex = new Regex("\u001B\\[[;\\d]*m");
                    result = regex.Replace(result, "");
                    string[] targets = result.Trim().Split('\n');
                    bool find = false;
                    foreach (var item in targets)
                    {
                        if (string.IsNullOrEmpty(item))
                            continue;

                        string t = item.Trim();
                        _allTargets.Add(t);

                        if (!find && _target != null && _target == t)
                            find = true;
                    }

                    if (!find)
                    {
                        _target = _allTargets.Count > 1 ? _allTargets[1] : _allTargets[0];
                    }
                }
            }

        }

        private async Task PrintAsync(string msg)
        {
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync();

            DTE dte = await _package.GetServiceAsync(typeof(DTE)) as DTE;
            Window outputWindow = dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
            outputWindow.Activate();

            IVsOutputWindow vso = await _package.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            Guid paneGuid = new Guid("f015d5ab-25d8-4ed3-8a3c-38fce53a0baf");
            vso.CreatePane(ref paneGuid, "XMake", 1, 1);

            IVsOutputWindowPane vsOutputWindowPane;
            vso.GetPane(ref paneGuid, out vsOutputWindowPane);
            vsOutputWindowPane.Activate();

            if (vsOutputWindowPane != null)
                vsOutputWindowPane.OutputString(msg + "\r\n");
        }

        private async Task RunCommandAsync(string command)
        {
            if (string.IsNullOrEmpty(_projDir))
                return;

            await PrintAsync(command);

            var proc = new System.Diagnostics.Process();
            proc.StartInfo.WorkingDirectory = _projDir;
            proc.StartInfo.FileName = "xmake";
            proc.StartInfo.Arguments = command;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.StandardOutputEncoding = Encoding.GetEncoding("GBK");
            proc.StartInfo.StandardErrorEncoding = Encoding.GetEncoding("GBK");
            proc.StartInfo.RedirectStandardError = true;
            proc.OutputDataReceived += Proc_OutputDataReceived;
            proc.ErrorDataReceived += Proc_ErrorDataReceived;
            proc.EnableRaisingEvents = true;
            proc.Exited += Proc_ExitReceived;

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
        }

        private async Task RefreshTargetAsync()
        {
            await _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                LoadTarget();

                XMakeToolWindow window = await _package.FindToolWindowAsync(typeof(XMakeToolWindow),
                    0,
                    true,
                    _package.DisposalToken) as XMakeToolWindow;
                if (window != null)
                {
                    ((IVsWindowFrame)window.Frame).Show();
                    window.RefreshTarget();
                }
            });
        }

        private async Task RefreshConfigAsync()
        {
            await _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                LoadConfig();

                XMakeToolWindow window = await _package.FindToolWindowAsync(typeof(XMakeToolWindow),
                    0,
                    true,
                    _package.DisposalToken) as XMakeToolWindow;
                if (window != null)
                {
                    ((IVsWindowFrame)window.Frame).Show();
                    window.RefreshConfig();
                }
            });
        }

        private async Task RefreshAllAsync()
        {
            await _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                LoadConfig();
                LoadTarget();

                XMakeToolWindow window = await _package.FindToolWindowAsync(typeof(XMakeToolWindow),
                    0,
                    true,
                    _package.DisposalToken) as XMakeToolWindow;
                if (window != null)
                {
                    ((IVsWindowFrame)window.Frame).Show();
                    window.RefreshEnable();
                    window.RefreshConfig();
                    window.RefreshTarget();
                }
            });
        }

        private void Proc_ExitReceived(object sender, EventArgs e)
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await PrintAsync("xmake finish");
            });
        }

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Regex regex = new Regex("\u001B\\[[;\\d]*m");
                string result = regex.Replace(e.Data, "");
                _package.JoinableTaskFactory.RunAsync(async () =>
                {
                    await PrintAsync(result);
                });
            }
        }

        private void Proc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
        }

        public void OnAfterOpenFolder(string folderPath)
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await InitProjectAsync(folderPath);
            });
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
    }
}
