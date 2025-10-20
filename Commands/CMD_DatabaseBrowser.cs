using CORE_VS_PLUGIN.GENERATOR.ToolWindows;
using CORE_VS_PLUGIN.Utils;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace CORE_VS_PLUGIN.Commands
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class CMD_DatabaseBrowser
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 4131;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("58A865A1-A6C6-474A-8816-019CD515F7D2");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="CMD_DatabaseBrowser"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        CMD_DatabaseBrowser(AsyncPackage package, OleMenuCommandService commandService)
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
        public static CMD_DatabaseBrowser Instance
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
            // Switch to the main thread - the call to AddCommand in CMD_DatabaseBrowser's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new CMD_DatabaseBrowser(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var svc = ServiceProvider.GetServiceAsync(typeof(DTE)).Result;
            var dte = svc as DTE;

            try
            {
                var (path, selectedNamespace) = CORE_VS_PLUGIN_HELPER.GetSelectedFolderPathAndNamespaceForDatabaseBrowser();

                var databaseBrowser = new DATABASE_BROWSER(dte, package, path, selectedNamespace);
                databaseBrowser.Show();
            }
            catch (Exception ex)
            {
                ConsoleWriter.Write(dte, ex.Message, nameof(CMD_DatabaseBrowser));
                ConsoleWriter.Write(dte, ex.StackTrace, nameof(CMD_DatabaseBrowser));

                if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
                {
                    ConsoleWriter.Write(dte, ex.InnerException.Message, nameof(CMD_DatabaseBrowser));
                }
            }
        }
    }
}