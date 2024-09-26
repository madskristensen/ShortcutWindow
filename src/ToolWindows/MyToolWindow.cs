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
        public override string GetTitle(int toolWindowId)
        {
            return Vsix.Name;
        }

        public override Type PaneType => typeof(Pane);

        public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            DTE2 dte = await VS.GetRequiredServiceAsync<DTE, DTE2>();
            return new ShortcutToolWindow(dte);
        }

        [Guid("0b20f013-c12d-40e1-9be7-4141a5b9942b")]
        internal class Pane : ToolkitToolWindowPane
        {
            public Pane()
            {
                BitmapImageMoniker = KnownMonikers.Keyboard;
                Caption = Vsix.Name;
            }
        }
    }
}