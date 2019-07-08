using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

using Chromium.WebBrowser.Event;

namespace PhpWin {

    using Chromium;
    using Chromium.Event;
    using Chromium.WebBrowser;

    public class Program {

        [STAThread]
        public static void Main() {

            ChromiumWebBrowser.OnBeforeCfxInitialize += ChromiumWebBrowser_OnBeforeCfxInitialize;
            ChromiumWebBrowser.OnBeforeCommandLineProcessing += ChromiumWebBrowser_OnBeforeCommandLineProcessing;
            ChromiumWebBrowser.Initialize();

            Application.EnableVisualStyles();
            var winform = new BrowserForm();
            winform.Show();
            Application.Run(winform);

            CfxRuntime.Shutdown();
        }

        static void ChromiumWebBrowser_OnBeforeCommandLineProcessing(CfxOnBeforeCommandLineProcessingEventArgs e) {
            Console.WriteLine(e.CommandLine.CommandLineString);
        }

        static void ChromiumWebBrowser_OnBeforeCfxInitialize(OnBeforeCfxInitializeEventArgs e) {
            e.Settings.LocalesDirPath = System.IO.Path.GetFullPath(@"cef64\locales");
            e.Settings.ResourcesDirPath = System.IO.Path.GetFullPath(@"cef64");
        }
    }
}
