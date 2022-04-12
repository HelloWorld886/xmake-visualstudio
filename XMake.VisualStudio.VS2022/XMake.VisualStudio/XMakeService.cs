using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace XMake.VisualStudio
{
    internal struct Intellisense
    {
        public string name;
        public List<string> inheritEnvironments;
        public List<string> includePath;
        public List<string> defines;
        public string intelliSenseMode;
    }

    internal struct Launch
    {
        public string type;
        public string name;
        public string project;
        public string cwd;
        public string program;
        public string MIMode;
        public string miDebuggerPath;
        public bool externalConsole;
    }

    [ComVisible(true)]
    public class XMakeService : IVsSolutionEvents7, IVsSolutionEvents
    {
        private static string _updateIntellisense = @"""import('core.project.config')
import('core.project.project')
import('core.project.rule')
config.load()
for k,t in pairs(project:targets()) do
	local rules = t:get('rules')
	if rules and type(rules) == 'string' then
		local on_config = rules:script('config')
		if on_config then
			utils.trycall(on_config, nil, t)
		end
    elseif rules and type(rules) == 'table' then
		for k, r in pairs(rules) do
			local rule = rule.rule(r)
			local on_config = rule:script('config')
			if on_config then
				utils.trycall(on_config, nil, t)
			end
        end
    end
    local name = t:name() or ''
    local define = ''
	local defines = t:get('defines') or ''
	if type(defines) == 'string' then
		define = defines
    elseif type(defines) == 'table' then
		for k, p in pairs(defines) do
			define = define == '' and p or (define.. ';' .. p)
        end
    end
    local arch = t:get('arch') or 'x86'
	local includes = ''
	for k, p in pairs(t:pkgs()) do
		local dir = p:get('sysincludedirs')
		if dir then
			includes = includes == '' and dir or (includes .. ';' .. dir)
		end
	end
    local includedirs = t:get('includedirs')
	if type(includedirs) == 'string' then
		includes = includes == '' and includedirs or (includes .. ';' .. includedirs)
    elseif type(includedirs) == 'table' then
		for k, p in pairs(includedirs) do
			includes = includes == '' and p or (includes .. ';' .. p)
        end
    end
    local sysincludedirs = t:get('sysincludedirs')
	if type(sysincludedirs) == 'string' then
		includes = includes == '' and sysincludedirs or (includes .. ';' .. sysincludedirs)
    elseif type(sysincludedirs) == 'table' then
		for k, p in pairs(sysincludedirs) do
			includes = includes == '' and p or (includes .. ';' .. p)
        end
    end
    local toolchains = t:toolchains()
    if toolchains then
		for k, toolchain in pairs(toolchains) do
			local runenvs = toolchain:runenvs()
            if runenvs then
                local include = runenvs.INCLUDE
                if include then
                    includes = includes == '' and include or (includes .. ';' .. include)
                end
            end
        end
    end
    print(string.format('name=%s|define=%s|arch=%s|includes=%s', name, define, arch, includes))
end""";

        private static string _updateLaunch = @"""
import('core.project.config')
import('core.project.project')
config.load()
for k,t in pairs(project:targets()) do
	if t:is_binary() then
        print(t:targetfile())
    end
end
""";

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
        private XMakeOptionPage _page;
        private bool _configChanged = false;
        private bool _isExecutable = false;

        public string ProjDir
        {
            get => _projDir;
        }

        public string Target
        {
            get => _target;
            set
            {
                if (!string.IsNullOrEmpty(_target) && _target != value)
                    _configChanged = true;
                _target = value;
            }
        }

        public string[] AllModes { get => _allModes; }

        public string[] AllPlats { get => _allPlats; }

        public string[] AllArchs { get => _allArchs; }

        public IReadOnlyList<string> AllTargets { get => _allTargets; }

        public string Mode
        { 
            get => _mode; 
            set
            {
                if (!string.IsNullOrEmpty(_mode) && _mode != value)
                    _configChanged = true;
                _mode = value;
            }
        }

        public string Plat
        {
            get => _plat; 
            set
            {
                if (!string.IsNullOrEmpty(_plat) && _plat != value)
                    _configChanged = true;
                _plat = value;
            }
        }

        public string Arch
        { 
            get => _arch;
            set
            {
                if (!string.IsNullOrEmpty(_arch) && _arch != value)
                    _configChanged = true;
                _arch = value;
            }
        }

        public string CppProperties { 
            get
            {
                return Path.Combine(_projDir, "CppProperties.json");
            } 
        }

        public string LaunchVS
        {
            get
            {
                return Path.Combine(_projDir, "Launch.vs.json");
            }
        }

        public bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(_projDir))
                    return false;
                return _isExecutable;
            }
        }

        public string ExecutablePath
        {
            get
            {
                if (string.IsNullOrEmpty(_page.CustomExecutablePath))
                    return "xmake";
                return _page.CustomExecutablePath;
            }
        }

        public void QuickStart()
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                if(await RunCommandAsync("f -y") != -1)
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

                if (_configChanged)
                {
                    await UpdateConfigAsync();
                    _configChanged = false;
                }

                if(await RunCommandAsync(command) != -1)
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

                if (await RunCommandAsync(command) != -1)
                    await RefreshAllAsync();
            });
        }

        public void CleanConfig()
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                if(await RunCommandAsync("f -c -y") != -1)
                    await RefreshAllAsync();
            });
        }

        public void UpdateIntellisense()
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                if(await RunCommandAsync("f -y") != -1)
                    await RefreshTargetAsync();

                string result = await RunScriptAsync(_updateIntellisense);
                result = result.Trim();
                Dictionary<string, Intellisense[]> intellisenseDict = new Dictionary<string, Intellisense[]>();
                Intellisense intellisense = new Intellisense()
                {
                    name = "",
                    inheritEnvironments = new List<string>(1) { "msvc_" + _arch },
                    includePath = new List<string>(2) {"${env.INCLUDE}", "${workspaceRoot}\\**"},
                    defines = new List<string>(),
                    intelliSenseMode = "windows-msvc-" + _arch
                };
                DirectoryInfo directoryInfo = new DirectoryInfo(_projDir);
                intellisense.name = directoryInfo.Name;
                intellisenseDict.Add("configurations", new Intellisense[] { intellisense});
                using (StringReader reader = new StringReader(result))
                {
                    while (reader.Peek() > -1)
                    {
                        string line = await reader.ReadLineAsync();
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line))
                            continue;
                        string[] fields = line.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < fields.Length; i++)
                        {
                            string field = fields[i].Trim();
                            string[] kv = field.Split(new char[] { '=' });
                            if (kv.Length != 2)
                                continue;
                            switch (kv[0])
                            {
                                case "includes":
                                    string includes = kv[1].Trim();
                                    if (!string.IsNullOrEmpty(includes))
                                    {
                                        string[] includePaths = includes.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                                        intellisense.includePath.AddRange(includePaths);
                                    }
                                    break;
                                case "define":
                                    string define = kv[1].Trim();
                                    if (!string.IsNullOrEmpty(define))
                                    {
                                        string[] defines = define.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                                        intellisense.defines.AddRange(defines);
                                    }
                                    break;
                            }
                        }
                    }
                }

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(intellisenseDict);
                try
                {
                    File.WriteAllText(CppProperties, json, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    await PrintAsync(ex.Message);
                }
            });

        }

        public void UpdateLaunch()
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                if(await RunCommandAsync("f -y") != -1)
                    await RefreshTargetAsync();

                string result = await RunScriptAsync(_updateLaunch);
                result = result.Trim();
                Dictionary<string, object> lanuchDict = new Dictionary<string, object>();
                lanuchDict.Add("version", "0.2.1");
                lanuchDict.Add("defaults", new Dictionary<string, string>());
                List<Launch> launches = new List<Launch>(0);
                lanuchDict.Add("configurations", launches);
                using (StringReader reader = new StringReader(result))
                {
                    while (reader.Peek() > -1)
                    {
                        string line = await reader.ReadLineAsync();
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line))
                            continue;

                        string targetFile = Path.Combine(_projDir, line);
                        Launch launch = new Launch()
                        {
                            name = Path.GetFileName(targetFile),
                            type = "cppvsdbg",
                            project = targetFile,
                            cwd = Path.GetDirectoryName(targetFile),
                            program = targetFile,
                            MIMode = "gdb",
                            miDebuggerPath = "",
                            externalConsole = true,
                        };
                        launches.Add(launch);
                    }
                }

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(lanuchDict);
                try
                {
                    File.WriteAllText(LaunchVS, json, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    await PrintAsync(ex.Message);
                }
            });
        }

        public Task<int> UpdateConfigAsync()
        {
            return RunCommandAsync(string.Format("f -p {0} -a {1} -m {2} -y", _plat, _arch, _mode));
        }

        public async Task InitializeAsync(XMakePluginPackage package, CancellationToken cancellationToken)
        {
            _package = package;
        }

        public async Task OnAfterPackageLoadedAsync(CancellationToken cancellationToken, XMakeOptionPage page)
        {
            _page = page;
            _page.OptionChange += OnOptionChanged;
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync();

            if(!CheckIsExecutable())
            {
                _isExecutable = false;
                await PrintAsync("Access https://xmake.io to download and install xmake first!");
                return;
            }

            _isExecutable = true;
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

        private bool CheckIsExecutable()
        {
            using (var proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.WorkingDirectory = _projDir;
                proc.StartInfo.FileName = ExecutablePath;
                proc.StartInfo.Arguments = "--version";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
                proc.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
                try
                {
                    if (proc.Start())
                    {
                        Regex regex = new Regex("\u001B\\[[;\\d]*m");
                        string result = regex.Replace(proc.StandardOutput.ReadToEnd(), "");
                        if (!string.IsNullOrEmpty(result))
                            return true;
                    }
                }
                catch(Exception e)
                {
                    return false;
                }

            }

            return false;
        }

        private void OnOptionChanged(string option, object value)
        {
            if(option == "CustomExecutablePath")
            {
                if (!CheckIsExecutable())
                {
                    _isExecutable = false;
                    _package.JoinableTaskFactory.RunAsync(async ()=>{
                        await PrintAsync("Access https://xmake.io to download and install xmake first!");
                    });
                }
                else
                {
                    _isExecutable = true;
                }

                _package.JoinableTaskFactory.Run(RefreshEnableAsync);
            }
        }

        private async Task LoadConfigAsync()
        {
            if (string.IsNullOrEmpty(_projDir))
                return;


            string result = await RunScriptAsync("\"import(\\\"core.project.config\\\"); config.load(); print(\\\"$(plat) $(arch) $(mode)\\\")\"");
            string[] cache = null;
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

        private async Task LoadTargetAsync()
        {
            if (string.IsNullOrEmpty(_projDir))
                return;

            _allTargets.Clear();
            _allTargets.Add("default");
            string result = await RunScriptAsync("\"import(\\\"core.project.config\\\"); import(\\\"core.project.project\\\"); config.load(); for name, _ in pairs((project.targets())) do print(name) end\"");
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

        private Task<int> RunCommandAsync(string command)
        {
            var tcs = new TaskCompletionSource<int>();

            if (string.IsNullOrEmpty(_projDir))
            {
                tcs.SetResult(-1);
                return tcs.Task;
            }

            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await PrintAsync(command);
            });

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
            proc.OutputDataReceived += (s, e)=>
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
            };
            proc.EnableRaisingEvents = true;
            proc.Exited += (s, e)=>
            {
                tcs.SetResult(proc.ExitCode);
                proc.Dispose();
            };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            return tcs.Task;
        }

        private async Task<string> RunScriptAsync(string script)
        {
            if (string.IsNullOrEmpty(_projDir))
                return "";

            string result = "";
            using (var proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.WorkingDirectory = _projDir;
                proc.StartInfo.FileName = "xmake";
                proc.StartInfo.Arguments = "lua -c " + script;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
                proc.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
                try
                {
                    if (proc.Start())
                        result = await proc.StandardOutput.ReadToEndAsync();
                }
                catch(Exception e)
                {
                    return string.Empty;
                }

            }

            return result;
        }

        private async Task RefreshTargetAsync()
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await LoadTargetAsync();
            XMakeToolWindow window = await _package.FindToolWindowAsync(typeof(XMakeToolWindow),
                0,
                true,
                _package.DisposalToken) as XMakeToolWindow;
            if (window != null)
            {
                ((IVsWindowFrame)window.Frame).Show();
                window.RefreshTarget();
            }
        }

        private async Task RefreshConfigAsync()
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await LoadConfigAsync();

            XMakeToolWindow window = await _package.FindToolWindowAsync(typeof(XMakeToolWindow),
                0,
                true,
                _package.DisposalToken) as XMakeToolWindow;
            if (window != null)
            {
                ((IVsWindowFrame)window.Frame).Show();
                window.RefreshConfig();
            }
        }

        private async Task RefreshEnableAsync()
        {
            XMakeToolWindow window = await _package.FindToolWindowAsync(typeof(XMakeToolWindow),
                0,
                true,
                _package.DisposalToken) as XMakeToolWindow;
            if (window != null)
            {
                ((IVsWindowFrame)window.Frame).Show();
                window.RefreshEnable();
            }
        }

        private async Task RefreshAllAsync()
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await LoadConfigAsync();
            await LoadTargetAsync();
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
