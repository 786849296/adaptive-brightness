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
using Windows.Media.Devices;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using WinRT;
using Windows.Devices.Bluetooth.Advertisement;
using System.Diagnostics;
using Windows.Storage.Streams;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace adaptive_brightness
{
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        //public static Image image { get; set; }
        //https://learn.microsoft.com/zh-cn/windows-hardware/design/device-experiences/images/adaptive-brightness-ambient-light-response-curve.png
        public readonly int[,] brightnessBucket = new int[7, 3] { { 0, 100, 36 }, { 50, 300, 47 }, { 200, 400, 60 }, { 250, 500, 72 }, { 300, 1700, 85 }, { 1100, 3000, 92 }, { 2000, 5000, 100 } };
        public int curBucket = 0;
        private AdjustScreenByWmi brightnessSetter = new();

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

            var curBrightness = brightnessSetter.GetBrightness();
            for (int i = 0; i < brightnessBucket.GetLength(0); i++)
                if (brightnessBucket[i, 2] >= curBrightness)
                {
                    curBucket = i;
                    break;
                }

            MediaCapture camera = new MediaCapture();
            await camera.InitializeAsync();
            camera.Failed += cameraInitFailed;
            var settings = new AdvancedPhotoCaptureSettings { Mode = AdvancedPhotoMode.Auto };
            camera.VideoDeviceController.AdvancedPhotoControl.Configure(settings);
            var photoProperties = camera.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.Photo);
            VideoEncodingProperties photoPropertie = new();
            foreach (VideoEncodingProperties p in photoProperties)
                if (p.Subtype == "NV12" && p.Width > photoPropertie.Width)
                    photoPropertie = p;
            camera.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.Photo, photoPropertie);
            AdvancedPhotoCapture advancedPhoto = await camera.PrepareAdvancedPhotoCaptureAsync(ImageEncodingProperties.CreateUncompressed(MediaPixelFormat.Nv12));

            //var queueController = DispatcherQueueController.CreateOnDedicatedThread();
            //var queue = queueController.DispatcherQueue;
            var timer = m_window.DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(10);
            timer.IsRepeating = true;
            timer.Tick += async (s, e) =>
            {
                //var lowLagCapture = await camera.PrepareLowLagPhotoCaptureAsync(ImageEncodingProperties.CreateUncompressed(MediaPixelFormat.Bgra8));
                if ((m_window as MainWindow).is_luxSensor)
                    return;
                AdvancedCapturedPhoto photo = await advancedPhoto.CaptureAsync();
                var bitmap = photo.Frame.SoftwareBitmap;
                double lumaAvg = 0;
                using (BitmapBuffer buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read))
                    using (var reference = buffer.CreateReference())
                        unsafe
                        {
                            byte* dataInBytes;
                            uint capacity;
                            reference.As<IMemoryBufferByteAccess>().GetBuffer(out dataInBytes, out capacity);

                            BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                            for (int i = 0; i < bufferLayout.Height; i++)
                                for (int j = 0; j < bufferLayout.Width; j++)
                                    lumaAvg += dataInBytes[bufferLayout.StartIndex + i * bufferLayout.Stride + j] / (photoPropertie.Width * photoPropertie.Height * 1.0);
                        }

                //bitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Gray8, BitmapAlphaMode.Premultiplied);
                //自定义系数，假设最大亮度5000lux
                double coefficient = 5000.0 / 255.0;
                double lux = lumaAvg * coefficient;
                await advancedPhoto.FinishAsync();

                brightnessAdaptive((float)lux, 0, (m_window as MainWindow).is_curveBucket);
            };
            timer.Start();

            BluetoothLEAdvertisementWatcher watcher = new BluetoothLEAdvertisementWatcher();
            watcher.AllowExtendedAdvertisements = true;
            watcher.ScanningMode = BluetoothLEScanningMode.Passive;
            watcher.AdvertisementFilter.Advertisement.LocalName = "sensor_light";

            watcher.Received += (watcher, args) =>
            {
                //type 0x21: ServiceData128BitUuids
                //不同手机是否会有差异？未知
                bool is_luxSensor = false, is_curveBucket = false;
                m_window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () => 
                {
                    is_luxSensor = (m_window as MainWindow).is_luxSensor;
                    is_curveBucket = (m_window as MainWindow).is_curveBucket;
                });
                if (!is_luxSensor)
                    return;
                var data = args.Advertisement.GetSectionsByType(0x21).First().Data;
                DataReader dataReader = DataReader.FromBuffer(data);
                dataReader.ByteOrder = ByteOrder.LittleEndian;
                Guid uuid = dataReader.ReadGuid();
                float lux = dataReader.ReadSingle();
                int brightness = dataReader.ReadInt32();
                brightnessAdaptive(lux, brightness, is_curveBucket);
            };
            watcher.Start();
            Debug.WriteLine(watcher.Status);
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

        private static void cameraInitFailedToast(string e)
        {
            new ToastContentBuilder()
                .AddArgument("action", "viewConversation")
                .AddArgument("conversationId", 9813)
                .AddText("Camera init failed")
                .AddText(e)
                .Show();
        }

        public void brightnessAdaptive(float lux, int brightnessPhone, bool is_curveBucket)
        {
            if (brightnessPhone == 0)
                brightnessPhone = brightnessSetter.GetBrightness();
            if (is_curveBucket)
            {
                while (lux > brightnessBucket[curBucket, 1])
                    curBucket++;
                while (lux < brightnessBucket[curBucket, 0])
                    curBucket--;
                brightnessPhone = brightnessBucket[curBucket, 2];
            }
            else
                for (int i = 0; i < brightnessBucket.GetLength(0); i++)
                    if (brightnessBucket[i, 2] >= brightnessPhone)
                    {
                        curBucket = i;
                        break;
                    }
            brightnessSetter.StartupBrightness(brightnessPhone);
        }

        private Window m_window;
    }
}
