using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Imaging;

namespace ShortcutWindow
{
    public class MyToolWindow : BaseToolWindow<MyToolWindow>
    {
        public override string GetTitle(int toolWindowId) => Vsix.Name;

        public override Type PaneType => typeof(Pane);

        public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            DTE2 dte = await VS.GetRequiredServiceAsync<DTE, DTE2>();
            General settings = await General.GetLiveInstanceAsync();
            CommandBridge service = await Package.GetServiceAsync<CommandBridge, CommandBridge>();

            return new ShortcutToolWindow(dte, settings, service);
        }

        [Guid("f2194a0a-71df-42ad-a530-a4d3a0379ce8")]
        internal class Pane : ToolkitToolWindowPane
        {
            public Pane()
            {
                BitmapImageMoniker = KnownMonikers.Keyboard;
            }
        }
    }
}