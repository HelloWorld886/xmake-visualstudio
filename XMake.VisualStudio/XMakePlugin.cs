//using System.Diagnostics;
//using System.Collections.Generic;
//using System;
//using System.Text;
//using System.Text.RegularExpressions;

//namespace XMake.VisualStudio
//{
//    internal struct Intellisense
//    {
//        public string name;
//        public string[] inheritEnvironments;
//        public string[] includePath;
//        public string defines;
//        public string intelliSenseMode;
//    }

//    internal struct Launch
//    {
//        public string type;
//        public string name;
//        public string project;
//        public string cwd;
//        public string program;
//        public string MIMode;
//        public string miDebuggerPath;
//        public bool externalConsole;
//    }

//    internal static class XMakePlugin
//    {
//        private static string _updateIntellisense = @"import('core.project.config')
//import('core.project.project')
//config.load()
//for k,t in pairs(project:targets()) do
//	print('name:' .. t:name())
//	local define = t:get('defines') or ''
//	print('defines:' .. define)
//	local arch = t:get('arch') or 'x86'
//	print('arch:' .. arch)
//	local includes = ''
//	for k, p in pairs(t:pkgs()) do
//		local dir = p:get('sysincludedirs')
//		if dir then
//			includes = includes == '' and dir or ';' .. dir
//		end
//	end
//	print('includes:' .. includes)
//end
//        ";
//        private static Dictionary<string, string> _options;
//        private static string[] _modes;
//        private static string[] _platforms;
//        private static string[] _archs;
//        private static List<string> _targets = new List<string>();
//        private static string _projectDir;
//        private static string _target = "default";
//        private static CommandType _commandType = CommandType.None;

//        public static Action BeginOutput;
//        public static Action<string> Output;
//        public static Action<CommandType> CommandFinished;

//        public enum CommandType
//        {
//            None,
//            QuickStart,
//            Run,
//            Build,
//            Clean,
//            CleanConfig,
//            UpdateCMake
//        }

//        static XMakePlugin()
//        {
//            _options = new Dictionary<string, string>();
//            _modes = new string[]
//            {
//                "debug",
//                "release"
//            };
//            _platforms = new string[]
//            {
//                "windows",
//            };
//            _archs = new string[]
//            {
//                "x86",
//                "x64"
//            };
//            _targets.Add("default");
//        }

//        public static string[] Modes
//        {
//            get
//            {
//                return _modes;
//            }

//        }

//        public static string[] Platforms
//        {
//            get
//            {
//                return _platforms;
//            }
//        }

//        public static string[] Archs
//        {
//            get 
//            { 
//                return _archs; 
//            }
//        }

//        public static IReadOnlyList<string> Targets
//        {
//            get 
//            { 
//                return _targets; 
//            }
//        }

//        public static string ProjectDir
//        {
//            get
//            {
//                return _projectDir;
//            }
//            set
//            {
//                _projectDir = value;
//            }
//        }

//        public static string Target
//        {
//            set 
//            { 
//                _target = value; 
//            }
//            get
//            {
//                return _target;
//            }
//        }

//        public static void LoadConfig()
//        {
//            if (string.IsNullOrEmpty(_projectDir))
//                return;

//            string[] cache = null;
//            string result = null;
//            using (var proc = new Process())
//            {
//                proc.StartInfo.WorkingDirectory = _projectDir;
//                proc.StartInfo.FileName = "xmake";
//                proc.StartInfo.Arguments = "l -c \"import(\\\"core.project.config\\\"); config.load(); print(\\\"$(plat) $(arch) $(mode)\\\")\"";
//                proc.StartInfo.UseShellExecute = false;
//                proc.StartInfo.RedirectStandardOutput = true;
//                proc.StartInfo.CreateNoWindow = true;
//                proc.StartInfo.RedirectStandardError = true;
//                proc.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
//                proc.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
//                proc.Start();
//                result = proc.StandardOutput.ReadToEnd();
//            }

//            if (!string.IsNullOrEmpty(result))
//            {
//                Regex regex = new Regex("\u001B\\[[;\\d]*m");
//                result = regex.Replace(result, "");
//                cache = result.Trim().Split(' ');
//            }

//            string platform = cache != null && cache.Length > 0 && !string.IsNullOrEmpty(cache[0]) ? cache[0] : null;
//            if (!string.IsNullOrEmpty(platform))
//                _options["platform"] = platform;
//            else
//                _options["platform"] = _platforms[0];

//            string arch = cache != null && cache.Length > 1 && !string.IsNullOrEmpty(cache[1]) ? cache[1] : null;
//            if (!string.IsNullOrEmpty(arch))
//                _options["arch"] = arch;
//            else
//                _options["arch"] = _archs[0];

//            string mode = cache != null && cache.Length > 2 && !string.IsNullOrEmpty(cache[2]) ? cache[2] : null;
//            if (!string.IsNullOrEmpty(mode))
//                _options["mode"] = mode;
//            else
//                _options["mode"] = _modes[0];

//        }

//        public static void LoadTargets()
//        {
//            _targets.Clear();
//            _targets.Add("default");
//            using (var proc = new Process())
//            {          
//                proc.StartInfo.WorkingDirectory = _projectDir;
//                proc.StartInfo.FileName = "xmake";
//                proc.StartInfo.Arguments = "l -c \"import(\\\"core.project.config\\\"); import(\\\"core.project.project\\\"); config.load(); for name, _ in pairs((project.targets())) do print(name) end\"";
//                proc.StartInfo.UseShellExecute = false;
//                proc.StartInfo.RedirectStandardOutput = true;
//                proc.StartInfo.CreateNoWindow = true;
//                proc.StartInfo.RedirectStandardError = true;
//                proc.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
//                proc.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
//                proc.Start();
//                string result = proc.StandardOutput.ReadToEnd();
//                if (!string.IsNullOrEmpty(result))
//                {
//                    Regex regex = new Regex("\u001B\\[[;\\d]*m");
//                    result = regex.Replace(result, "");
//                    string[] targets = result.Trim().Split('\n');
//                    bool find = false;
//                    foreach (var item in targets)
//                    {
//                        if (string.IsNullOrEmpty(item))
//                            continue;

//                        string t = item.Trim();
//                        _targets.Add(t);

//                        if (!find && _target != null && _target == t)
//                            find = true;
//                    }

//                    if (!find)
//                    {
//                        _target = _targets.Count > 1 ? _targets[1] : _targets[0];
//                    }
//                }
//            }
//        }

//        public static string GetOption(string name)
//        {
//            if (string.IsNullOrEmpty(name))
//                return "";

//            string result;
//            if (_options.TryGetValue(name, out result))
//                return result;

//            return "";
//        }

//        public static void SetOption(string name, string value)
//        {
//            if (string.IsNullOrEmpty(name) ||
//                string.IsNullOrEmpty(value))
//                return;

//            if (!_options.ContainsKey(name))
//                return;

//            if (_options[name] != value)
//            {
//                _optionsChanged = true;
//                _options[name] = value;
//                UpdateOptions();
//            }
//        }

//        private static void UpdateOptions()
//        {
//            RunCommand(string.Format("f -p {0} -a {1} -m {2}", GetOption("platform"), GetOption("arch"), GetOption("mode")));
//            _optionsChanged = false;
//        }

//        private static void RunCommand(string command)
//        {
//            if (string.IsNullOrEmpty(_projectDir))
//                return;

//            BeginOutput.Invoke();
//            Output.Invoke("xmake " + command);

//            var proc = new Process();
//            proc.StartInfo.WorkingDirectory = _projectDir;
//            proc.StartInfo.FileName = "xmake";
//            proc.StartInfo.Arguments = command;
//            proc.StartInfo.UseShellExecute = false;
//            proc.StartInfo.RedirectStandardOutput = true;
//            proc.StartInfo.CreateNoWindow = true;
//            proc.StartInfo.RedirectStandardError = true;
//            proc.StartInfo.StandardOutputEncoding = Encoding.GetEncoding("GBK");
//            proc.StartInfo.StandardErrorEncoding = Encoding.GetEncoding("GBK");
//            proc.StartInfo.RedirectStandardError = true;
//            proc.OutputDataReceived += Proc_OutputDataReceived;
//            proc.ErrorDataReceived += Proc_ErrorDataReceived;
//            proc.EnableRaisingEvents = true;
//            proc.Exited += Proc_ExitReceived;

//            proc.Start();
//            proc.BeginOutputReadLine();
//            proc.BeginErrorReadLine();
//        }

//        private static void Proc_ExitReceived(object sender, EventArgs e)
//        {
//            Output.Invoke("xmake finished");
//            CommandFinished.Invoke(_commandType);
//            _commandType = CommandType.None;
//        }

//        private static void Proc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
//        {
//        }

//        private static void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
//        {
//            if(!string.IsNullOrEmpty(e.Data))
//            {
//                Regex regex = new Regex("\u001B\\[[;\\d]*m");
//                string result = regex.Replace(e.Data, "");
//                Output.Invoke(result);
//            }
//        }

//        public static void QuickStart()
//        {
//            _commandType = CommandType.QuickStart;
//            RunCommand("f -y");
//        }

//        public static void Build()
//        {
//            if (string.IsNullOrEmpty(_target))
//                return;

//            string command = "build -v -y";
//            if (_target != "default")
//                command += " " + _target;
//            else
//                command += " -a";
//            _commandType = CommandType.Build;
//            RunCommand(command);
//        }

//        public static void Run()
//        {
//            if (string.IsNullOrEmpty(_target))
//                return;

//            string command = "r";
//            if ( _target != "default")
//               command += " " + _target;
//            _commandType = CommandType.Run;
//            RunCommand(command);
//        }

//        public static void Clean()
//        {
//            if (string.IsNullOrEmpty(_target))
//                return;

//            string command = "c";
//            if (_target != "default")
//                command += " " + _target;
//            _commandType = CommandType.Clean;
//            RunCommand(command);
//        }

//        public static void CleanConfig()
//        {
//            _commandType = CommandType.CleanConfig;
//            RunCommand("f -c -y");
//        }

//        public static void UpdateCMake()
//        {
//            _commandType = CommandType.UpdateCMake;
//            RunCommand("project -k cmake");
//        }

//        public static void UpdateIntellisense()
//        {
//            using (var proc = new Process())
//            {
//                proc.StartInfo.WorkingDirectory = _projectDir;
//                proc.StartInfo.FileName = "xmake";
//                proc.StartInfo.Arguments = "l -c " + _updateIntellisense;
//                proc.StartInfo.UseShellExecute = false;
//                proc.StartInfo.RedirectStandardOutput = true;
//                proc.StartInfo.CreateNoWindow = true;
//                proc.StartInfo.RedirectStandardError = true;
//                proc.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
//                proc.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
//                proc.Start();
//                string result = proc.StandardOutput.ReadToEnd();
//                BeginOutput.Invoke();
//                Output.Invoke(result);
//            }
//        }
//    }
//}
