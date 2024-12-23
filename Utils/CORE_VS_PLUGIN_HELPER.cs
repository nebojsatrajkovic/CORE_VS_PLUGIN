using EnvDTE;
using EnvDTE80;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CORE_VS_PLUGIN.Utils
{
    public static class CORE_VS_PLUGIN_HELPER
    {
        public static List<FileInfo> _GetProjectsFileInfos(DTE dte)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            var returnValue = new List<FileInfo>();

            var slnPath = dte.Solution.FullName;

            var slnFileInfo = new FileInfo(slnPath);
            var csprojPath = new FileInfo(slnPath).Directory.GetFiles().First(x => x.Name.ToLower().EndsWith("csproj"));
            var startupProjectName = csprojPath.Name;

            var Content = File.ReadAllText(slnPath);
            var projReg = new Regex("Project\\(\"\\{[\\w-]*\\}\"\\) = \"([\\w _]*.*)\", \"(.*\\.(cs|vcx|vb)proj)\"", RegexOptions.Compiled);
            var matches = projReg.Matches(Content).Cast<Match>();
            var folderNames = matches.Select(x => x.Groups[1].Value).ToList();
            var projects = matches.Select(x => x.Groups[2].Value).ToList();
            for (int i = 0; i < projects.Count; ++i)
            {
                if (!Path.IsPathRooted(projects[i]))
                    projects[i] = Path.Combine(Path.GetDirectoryName(slnPath),
                        projects[i]);
                projects[i] = Path.GetFullPath(projects[i]);

                var project = projects[i];

                var projectFileInfo = new FileInfo(project);

                returnValue.Add(projectFileInfo);
            }

            return returnValue;
        }

        public static string _GetStartupProjectName(IServiceProvider ServiceProvider)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            var dte = (DTE)ServiceProvider.GetService(typeof(DTE));
            var slnPath = dte?.Solution.FullName ?? string.Empty;

            var slnFileInfo = new FileInfo(slnPath);
            var csprojPath = new FileInfo(slnPath).Directory.GetFiles().First(x => x.Name.ToLower().EndsWith("csproj"));
            var startupProjectName = csprojPath.Name;

            return startupProjectName;
        }

        private static OutputWindowPane GetOutputPane(DTE dte, string pane)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            var application = (DTE2)dte;

            try
            {
                return application.ToolWindows.OutputWindow.OutputWindowPanes.Item(pane);
            }
            catch (Exception)
            {
                application.ToolWindows.OutputWindow.OutputWindowPanes.Add(pane);
            }

            return application.ToolWindows.OutputWindow.OutputWindowPanes.Item(pane);
        }

        public static void WriteToConsole(DTE dte, string message)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            var outputWindowName = nameof(CORE_VS_PLUGIN);

            var pane = GetOutputPane(dte, outputWindowName);

            pane.OutputString(message);
            pane.Activate();
        }

        public static void GIT_WriteToConsole(DTE dte, string message)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            var outputWindowName = $"{nameof(CORE_VS_PLUGIN)} -> GIT";

            var pane = GetOutputPane(dte, outputWindowName);

            pane.OutputString(Environment.NewLine);
            pane.OutputString(message);
            pane.OutputString(Environment.NewLine);

            pane.Activate();
        }

        public static string CommandOutput(string command, string workingDirectory = null)
        {
            try
            {
                var procStartInfo = new ProcessStartInfo("cmd", "/c " + command);

                procStartInfo.RedirectStandardError = procStartInfo.RedirectStandardInput = procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                procStartInfo.CreateNoWindow = true;
                if (null != workingDirectory)
                {
                    procStartInfo.WorkingDirectory = workingDirectory;
                }

                var proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                proc.Start();

                var sb = new StringBuilder();
                proc.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    sb.AppendLine(e.Data);
                };
                proc.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    sb.AppendLine(e.Data);
                };

                sb.AppendLine();
                sb.AppendLine();

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
                return sb.ToString();
            }
            catch (Exception objException)
            {
                return $"Error in command: {command}, {objException.Message}";
            }
        }
    }
}