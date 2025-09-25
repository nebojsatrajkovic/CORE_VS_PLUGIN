using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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
        internal static void ShowMessageBox(this AsyncPackage package, string title, string message, OLEMSGICON messageType)
        {
            VsShellUtilities.ShowMessageBox(
                package,
                message,
                title,
                messageType,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        public static List<FileInfo> GetProjectsFileInfos(DTE dte)
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

        internal static string CommandOutput(string command, string workingDirectory = null)
        {
            return CommandOutput(new List<string> { command }, workingDirectory);
        }

        internal static string CommandOutput(List<string> commands, string workingDirectory = null)
        {
            string command = string.Empty;

            try
            {
                command = string.Join(" && ", commands);

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