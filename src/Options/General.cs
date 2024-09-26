using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ShortcutWindow
{
    internal partial class OptionsProvider
    {
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General> { }
    }

    public class General : BaseOptionModel<General>, IRatingConfig
    {
        [Category("General")]
        [DisplayName("Font size (shortcut)")]
        [Description("The font size of the keyboard shortcut when shown in the tool window. Default: 40")]
        [DefaultValue(40)]
        public int FontSizeShortcut { get; set; } = 40;

        [Category("General")]
        [DisplayName("Font size (command)")]
        [Description("The font size of the command name under the shortcut when shown in the tool window. Default: 25")]
        [DefaultValue(25)]
        public int FontSizeCommand { get; set; } = 25;

        [Browsable(false)]
        public int RatingRequests { get; set; }
    }
}
