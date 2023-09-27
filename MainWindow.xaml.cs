using H.NotifyIcon.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace adaptive_brightness
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public bool is_luxSensor { get { return luxGetter_sensor.IsChecked; } }
        public bool is_curveBucket { get { return curve_bucket.IsChecked; } }

        public MainWindow()
        {
            this.InitializeComponent();

            //App.image = this.imageControl;
            var exitCommand = new StandardUICommand();
            exitCommand.ExecuteRequested += ExitCommand_ExecuteRequested;
            exit.Command = exitCommand;
        }

        private void ExitCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            Application.Current.Exit();
        }

    }
}
