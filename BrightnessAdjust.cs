using Microsoft.Management.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace adaptive_brightness
{
    //https://github.com/luojunyuan/dotNetPlayground/blob/master/BrightnessAdjust/AdjustScreenBuilder.cs
    class AdjustScreenByWmi
    {
        // Store array of valid level values
        private readonly byte[] _brightnessLevels;
        //// Define scope (namespace)
        //readonly ManagementScope _scope = new ManagementScope("root\\WMI");
        //// Define querys
        //readonly SelectQuery _query = new SelectQuery("WmiMonitorBrightness");
        //readonly SelectQuery _queryMethods = new SelectQuery("WmiMonitorBrightnessMethods");

        readonly CimSession _session = CimSession.Create(null);
        readonly string _query = "select * from WmiMonitorBrightness";
        readonly string _queryMethods = "select * from WmiMonitorBrightnessMethods";
        public bool IsSupported { get; set; }

        public AdjustScreenByWmi()
        {
            //get the level array for this system
            _brightnessLevels = GetBrightnessLevels();
            if (_brightnessLevels.Length == 0)
            {
                //"WmiMonitorBrightness" is not supported by the system
                IsSupported = false;
            }
            else
            {
                IsSupported = true;
            }
        }

        /// <summary>
        /// Returns the current brightness setting
        /// </summary>
        /// <returns></returns>
        private int GetBrightness()
        {
            //using ManagementObjectSearcher searcher = new(_scope, _query);
            //using ManagementObjectCollection objCollection = searcher.Get();

            IEnumerable<CimInstance> objCollection = _session.QueryInstances(@"root/WMI", "WQL", _query);

            byte curBrightness = 0;
            foreach (var obj in objCollection)
            {
                curBrightness = (byte)obj.CimInstanceProperties["CurrentBrightness"].Value;
                break;
            }
            return curBrightness;
        }

        /// <summary>
        /// Convert the brightness percentage to a byte and set the brightness using SetBrightness()
        /// </summary>
        /// <param name="iPercent"></param>
        public void StartupBrightness(int iPercent)
        {
            // XXX: ...
            if (iPercent < 0)
            {
                iPercent = 0;
            }
            else if (iPercent > 100)
            {
                iPercent = 100;
            }

            // iPercent is in the range of brightnessLevels
            if (iPercent <= _brightnessLevels[^1])
            {
                // Default level 100
                byte level = 100;
                foreach (byte item in _brightnessLevels)
                {
                    // 找到 brightnessLevels 数组中与传入的 iPercent 接近的一项
                    if (item >= iPercent)
                    {
                        level = item;
                        break;
                    }
                }
                SetBrightness(level);
            }
        }

        /// <summary>
        /// Set the brightness level to the targetBrightness
        /// </summary>
        /// <param name="targetBrightness"></param>
        private void SetBrightness(byte targetBrightness)
        {
            //using ManagementObjectSearcher searcher = new ManagementObjectSearcher(_scope, _queryMethods);
            //using ManagementObjectCollection objectCollection = searcher.Get();
            IEnumerable<CimInstance> objCollection = _session.QueryInstances(@"root/WMI", "WQL", _queryMethods);

            foreach (var obj in objCollection)
            {
                CimMethodParametersCollection parameters = new CimMethodParametersCollection();
                parameters.Add(CimMethodParameter.Create("Brightness", targetBrightness, CimFlags.In));
                parameters.Add(CimMethodParameter.Create("Timeout", uint.MaxValue, CimFlags.In));
                _session.InvokeMethod(obj, "WmiSetBrightness", parameters);
                break;
            }
        }

        private byte[] GetBrightnessLevels()
        {
            // Output current brightness
            //using ManagementObjectSearcher mos = new(_scope, _query);

            IEnumerable<CimInstance> objCollection = _session.QueryInstances(@"root/WMI", "WQL", _query);
            byte[] bLevels = Array.Empty<byte>();
            try
            {
                //using ManagementObjectCollection moc = mos.Get();
                foreach (var obj in objCollection)
                {
                    bLevels = (byte[])obj.CimInstanceProperties["Level"].Value;
                    // Only work on the first object
                    break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            return bLevels;
        }
    }
}
