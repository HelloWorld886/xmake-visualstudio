﻿using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace XMake.VisualStudio
{
    internal static class XMakePlugin
    {
        private static Dictionary<string, string> _options;
        private static string[] _modes;
        private static string[] _platforms;
        private static string[] _archs;
        private static string _projectDir;
        private static bool _optionsChanged = false;

        public static Action BeginOutput;
        public static Action<string> Output;

        static XMakePlugin()
        {
            _options = new Dictionary<string, string>();
            _modes = new string[]
            {
                "debug",
                "release"
            };
            _platforms = new string[]
            {
                "windows",
            };
            _archs = new string[]
            {
                "x86",
                "x64"
            };
        }

        public static string[] Modes
        {
            get
            {
                return _modes;
            }

        }

        public static string[] Platforms
        {
            get
            {
                return _platforms;
            }
        }

        public static string[] Archs
        {
            get 
            { 
                return _archs; 
            }
        }

        public static string ProjectDir
        {
            get
            {
                return _projectDir;
            }
            set
            {
                _projectDir = value;
            }
        }

        public static void LoadConfig()
        {
            if (string.IsNullOrEmpty(_projectDir))
                return;

            string[] cache = null;
            string result = null;
            using (var proc = new Process())
            {
                proc.StartInfo.WorkingDirectory = _projectDir;
                proc.StartInfo.FileName = "xmake";
                proc.StartInfo.Arguments = "l -c \"import(\\\"core.project.config\\\"); config.load(); print(\\\"$(plat) $(arch) $(mode)\\\")\"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
                proc.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
                proc.Start();
                result = proc.StandardOutput.ReadToEnd();
            }

            if (!string.IsNullOrEmpty(result))
                cache = result.Trim().Split(' ');

            string platform = cache != null && !string.IsNullOrEmpty(cache[0]) ? cache[0] : null;
            if (!string.IsNullOrEmpty(platform))
                _options["platform"] = platform;
            else
                _options["platform"] = _platforms[0];

            string arch = cache != null && !string.IsNullOrEmpty(cache[1]) ? cache[1] : null;
            if (!string.IsNullOrEmpty(arch))
                _options["arch"] = arch;
            else
                _options["arch"] = _archs[0];

            string mode = cache != null && !string.IsNullOrEmpty(cache[2]) ? cache[2] : null;
            if (!string.IsNullOrEmpty(mode))
                _options["mode"] = mode;
            else
                _options["mode"] = _modes[0];

        }

        public static string GetOption(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";

            string result;
            if (_options.TryGetValue(name, out result))
                return result;

            return "";
        }

        public static void SetOption(string name, string value)
        {
            if (string.IsNullOrEmpty(name) ||
                string.IsNullOrEmpty(value))
                return;

            if (!_options.ContainsKey(name))
                return;

            if (_options[name] != value)
            {
                _optionsChanged = true;
                _options[name] = value;
                UpdateOptions();
            }
        }

        private static void UpdateOptions()
        {
            RunCommand(string.Format("f -p {0} -a {1} -m {2}", GetOption("platform"), GetOption("arch"), GetOption("mode")));
            _optionsChanged = false;
        }

        private static void RunCommand(string command)
        {
            if (string.IsNullOrEmpty(_projectDir))
                return;

            BeginOutput.Invoke();
            Output.Invoke("xmake " + command);

            var proc = new Process();
            proc.StartInfo.WorkingDirectory = _projectDir;
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
            proc.Exited += Proc_Exited;

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
        }

        private static void Proc_Exited(object sender, EventArgs e)
        {
            Output.Invoke("xmake finished");
        }

        private static void Proc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
        }

        private static void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if(!string.IsNullOrEmpty(e.Data))
            {
                Regex regex = new Regex("\u001B\\[[;\\d]*m");
                string result = regex.Replace(e.Data, "");
                Output.Invoke(result);
            }
        }

        public static void QuickStart()
        {
            RunCommand("f -y");
        }

        public static void Build()
        {
            RunCommand("build -v -a -y");
        }


        public static void Run()
        {
            RunCommand("r -a");
        }

        public static void Clean()
        {
            RunCommand("f c -y");
        }

        public static void CleanConfig()
        {
            RunCommand("f -c -y");
        }

        public static void UpdateCMake()
        {
            RunCommand("project -k cmake");
        }
    }
}