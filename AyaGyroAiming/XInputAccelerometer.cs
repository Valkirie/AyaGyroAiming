using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Sensors;

namespace AyaGyroAiming
{
    public class XInputAccelerometerReadingChangedEventArgs : EventArgs
    {
        public float AccelerationX { get; set; }
        public float AccelerationY { get; set; }
        public float AccelerationZ { get; set; }
    }

    public class XInputAccelerometer
    {
        public Accelerometer sensor;

        public event XInputAccelerometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputAccelerometerReadingChangedEventHandler(Object sender, XInputAccelerometerReadingChangedEventArgs e);

        public XInputAccelerometer(uint _rate)
        {
            sensor = Accelerometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = _rate < sensor.MinimumReportInterval ? sensor.MinimumReportInterval : _rate;
                Console.WriteLine($"Accelerometer initialised.");
                Console.WriteLine($"Accelerometer report interval set to {sensor.ReportInterval}ms");
                Console.WriteLine();

                sensor.ReadingChanged += AcceleroReadingChanged;
            }
        }

        void AcceleroReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            AccelerometerReading reading = args.Reading;

            // raise event
            XInputAccelerometerReadingChangedEventArgs newargs = new XInputAccelerometerReadingChangedEventArgs()
            {
                AccelerationX = (float)reading.AccelerationX,
                AccelerationY = (float)reading.AccelerationY,
                AccelerationZ = (float)reading.AccelerationZ
            };
            ReadingChanged?.Invoke(this, newargs);
        }
    }
}
