using EnvDTE;
using EnvDTE80;
using System;

namespace CORE_VS_PLUGIN.Utils
{
    internal static class ConsoleWriter
    {
        internal static void Write(DTE dte, string message, string titleSuffix = "")
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            var outputWindowName = $"{nameof(CORE_VS_PLUGIN)}";

            if (!string.IsNullOrEmpty(titleSuffix)) { outputWindowName = $"{titleSuffix} -> {titleSuffix}"; }

            var pane = GetOutputPane(dte, outputWindowName);

            pane.OutputString(Environment.NewLine);
            pane.OutputString(message);
            pane.OutputString(Environment.NewLine);

            pane.Activate();
        }

        static OutputWindowPane GetOutputPane(DTE dte, string pane)
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
    }
}