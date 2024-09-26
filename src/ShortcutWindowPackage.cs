global using System;
global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using Task = System.Threading.Tasks.Task;
using System.Runtime.InteropServices;
using System.Threading;

namespace ShortcutWindow
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideToolWindow(typeof(MyToolWindow.Pane), DockedHeight = 400, DocumentLikeTool = false, Orientation = ToolWindowOrientation.Bottom, Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.ShortcutWindowString)]
    public sealed class ShortcutWindowPackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();
            this.RegisterToolWindows();
        }
    }
}