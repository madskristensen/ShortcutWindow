namespace ShortcutWindow
{
    [Command(PackageIds.MyCommand)]
    internal sealed class MyToolWindowCommand : BaseCommand<MyToolWindowCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await MyToolWindow.ShowAsync();
        }
    }
}
