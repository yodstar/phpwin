using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Text;

namespace PhpWin {

    using Chromium;
    using Chromium.Event;
    using Chromium.Remote;
    using Chromium.Remote.Event;
    using Chromium.WebBrowser;

    public partial class BrowserForm : Form
    {

        public BrowserForm() {
            InitializeComponent();

            this.WindowState = FormWindowState.Maximized;

            // Refresh
            this.SizeChanged += (_, e) => { WebBrowser.Refresh(); };

            // Cookies
            WebBrowser.RequestHandler.CanSetCookie += (_, e) => { e.SetReturnValue(true); };
            WebBrowser.RequestHandler.CanGetCookies += (_, e) => { e.SetReturnValue(true); };

            // ContextMenu
            WebBrowser.ContextMenuHandler.OnBeforeContextMenu += (_, e) => {
                e.Model.RemoveAt(e.Model.Count - 1);
                e.Model.InsertItemAt(2, 26501, "Reload");
                e.Model.AddItem(26502, "Copy");
                e.Model.AddItem(26503, "Home");
                e.Model.AddItem(26504, "Inspect");
            };
            WebBrowser.ContextMenuHandler.OnContextMenuCommand += (_, e) =>
            {
                switch (e.CommandId)
                {
                    case 26501:
                        WebBrowser.BrowserHost.Browser.ReloadIgnoreCache();
                        break;
                    case 26502:
                        Clipboard.SetDataObject(e.Params.SelectionText);
                        break;
                    case 26503:
                        LoadHomePage();
                        break;
                    case 26504:
                        ShowDevTools();
                        break;
                }
            };

            // ResourceHandler
            WebBrowser.RequestHandler.GetResourceHandler += (_, e) => { CgiResource.GetResourceHandler(_, e); };
            
            // DownloadHandler
            WebBrowser.DownloadHandler.OnBeforeDownload += (_, e) => { e.Callback.Continue(string.Empty, true); };

            LoadHomePage();
        }


        private void LoadHomePage()
        {
            WebBrowser.LoadUrl("http://phpwin/phpinfo.php");
        }


        private void ShowDevTools()
        {
            CfxWindowInfo windowInfo = new CfxWindowInfo();

            windowInfo.Style = WindowStyle.WS_OVERLAPPEDWINDOW | WindowStyle.WS_CLIPCHILDREN | WindowStyle.WS_CLIPSIBLINGS | WindowStyle.WS_VISIBLE;
            windowInfo.ParentWindow = IntPtr.Zero;
            windowInfo.WindowName = "Dev Tools";
            windowInfo.X = 100;
            windowInfo.Y = 300;
            windowInfo.Width = 1024;
            windowInfo.Height = 400;

            WebBrowser.BrowserHost.ShowDevTools(windowInfo, new CfxClient(), new CfxBrowserSettings(), null);

        }
    }
}
