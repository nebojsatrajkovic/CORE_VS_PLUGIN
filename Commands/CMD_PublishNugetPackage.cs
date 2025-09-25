using CORE_VS_PLUGIN.Utils;
using EnvDTE;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Task = System.Threading.Tasks.Task;

namespace CORE_VS_PLUGIN.Commands
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class CMD_PublishNugetPackage
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 4133;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("52E118AE-65E5-47D1-88AA-00BCDE03C256");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="CMD_PublishNugetPackage"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        CMD_PublishNugetPackage(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static CMD_PublishNugetPackage Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in CMD_PublishNugetPackage's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new CMD_PublishNugetPackage(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = (DTE)ServiceProvider.GetServiceAsync(typeof(DTE)).Result;

            try
            {
                var selectedItem = dte.SelectedItems.Item(1);

                EnvDTE.ProjectItem projectItem = selectedItem.ProjectItem;
                EnvDTE.Project project = selectedItem.Project;

                var dependencies = GetAllDependencies(project.FileName);

                var currentProject = ProjectCollection.GlobalProjectCollection.LoadProject(project.FileName);

                #region increase current project version

                var version = currentProject.GetPropertyValue("Version");

                if (string.IsNullOrWhiteSpace(version)) version = "1.0.0";

                var parts = version.Split('.').ToList();

                while (parts.Count < 3) parts.Add("0");

                var lastPart = int.Parse(parts.ElementAt(parts.Count - 1));
                parts.RemoveAt(parts.Count - 1);
                parts.Add((lastPart + 1).ToString("D3"));
                version = string.Join(".", parts);

                currentProject.SetProperty("Version", version);

                currentProject.Save();

                #endregion increase current project version

                #region update project dependencies and their version

                string csproj = File.ReadAllText(project.FileName);

                foreach (var dependency in dependencies)
                {
                    var dependencyProject = ProjectCollection.GlobalProjectCollection.LoadProject(dependency);

                    var dependencyProjectPackageId = dependencyProject.GetPropertyValue("PackageId");
                    var dependencyProjectVersion = dependencyProject.GetPropertyValue("Version");

                    string pattern = $@"(<PackageReference\s+Include=""{Regex.Escape(dependencyProjectPackageId)}""\s+Version="")(.*?)("")";

                    csproj = Regex.Replace(csproj, pattern, match =>
                    {
                        return match.Groups[1].Value + dependencyProjectVersion + match.Groups[3].Value;
                    });
                }

                File.WriteAllText(project.FileName, csproj);

                #endregion update project dependencies and their version

                #region execute git commands

                var fileInfo = new FileInfo(project.FileName);

                var gitCommands = new List<string>
                {
                    $"cd {fileInfo.DirectoryName}",
                    "git add .",
                    $"git commit -m \"{version}\"",
                    "git push"
                };

                var commandoutput = CORE_VS_PLUGIN_HELPER.CommandOutput(gitCommands);

                ConsoleWriter.Write(dte, $"{project.Name} - {commandoutput}", nameof(CMD_PublishNugetPackage));

                #endregion execute git commands

                package.ShowMessageBox(nameof(CORE_VS_PLUGIN), "Successfully created nuget package!", OLEMSGICON.OLEMSGICON_INFO);
            }
            catch (Exception ex)
            {
                ConsoleWriter.Write(dte, ex.Message, nameof(CMD_PublishNugetPackage));
                ConsoleWriter.Write(dte, ex.StackTrace, nameof(CMD_PublishNugetPackage));

                if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
                {
                    ConsoleWriter.Write(dte, ex.InnerException.Message, nameof(CMD_PublishNugetPackage));
                }

                package.ShowMessageBox(nameof(CORE_VS_PLUGIN), "Failed create nuget package!", OLEMSGICON.OLEMSGICON_CRITICAL);
            }
            finally
            {
                ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            }
        }

        static List<string> GetAllDependencies(string projectPath)
        {
            var result = new List<string>();

            projectPath = Path.GetFullPath(projectPath);

            if (!File.Exists(projectPath)) return result;

            var project = new Microsoft.Build.Evaluation.Project(projectPath); // load into shared collection

            foreach (var item in project.GetItems("ProjectReference"))
            {
                string refPath = Path.GetFullPath(Path.Combine(project.DirectoryPath, item.EvaluatedInclude));

                project = new Microsoft.Build.Evaluation.Project(refPath); // load into shared collection

                result.Add(refPath);
            }

            return result;
        }
    }
}