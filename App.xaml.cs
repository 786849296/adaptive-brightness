using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Diagnostics;
using Microsoft.Management.Infrastructure;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace adaptive_brightness
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        MediaCapture camera;
        //public static Image image { get; set; }
        public double brightness = 0.5;

        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            //m_window.Activate();

            camera = new MediaCapture();
            await camera.InitializeAsync();
            camera.Failed += cameraInitFailed;

            var lowLagCapture = await camera.PrepareLowLagPhotoCaptureAsync(ImageEncodingProperties.CreateUncompressed(MediaPixelFormat.Bgra8));
            var photo = await lowLagCapture.CaptureAsync();
            var softwareBitmap = photo.Frame.SoftwareBitmap;
            await lowLagCapture.FinishAsync();

            var brightnessSetter = new AdjustScreenByWmi();
            brightnessSetter.StartupBrightness(50);

            //if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || softwareBitmap.BitmapAlphaMode == BitmapAlphaMode.Straight)
            //    softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            //var source = new SoftwareBitmapSource();
            //await source.SetBitmapAsync(softwareBitmap);
            //image.Source = source;
        }

        private void cameraInitFailed(MediaCapture sender, MediaCaptureFailedEventArgs e)
        {
            cameraInitFailedToast(e.Message);
            throw new NotImplementedException();
        }

        private void cameraInitFailedToast(string e)
        {
            new ToastContentBuilder()
                .AddArgument("action", "viewConversation")
                .AddArgument("conversationId", 9813)
                .AddText("Camera init failed")
                .AddText(e)
                .Show();
        }

        private Window m_window;
    }
}
