using SharpDX.XInput;
using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using Windows.Devices.Sensors;

namespace AyaGyroAiming
{
    public class XInputGirometerReadingChangedEventArgs : EventArgs
    {
        public float AngularStickX { get; set; }
        public float AngularStickY { get; set; }
        public float AngularStickZ { get; set; }

        public float AngularVelocityX { get; set; }
        public float AngularVelocityY { get; set; }
        public float AngularVelocityZ { get; set; }
    }

    public class XInputGirometer
    {
        public Gyrometer sensor;
        public uint poolsize;

        // Compute & Maths
        uint gyroPoolIdx;
        Vector3[] gyroPool;

        // const
        float GyroStickAlpha = 0.2f;
        float GyroStickMagnitude = 3.5f;
        float GyroStickThreshold = 0.1f;
        float GyroStickRatio = 1.7f;

        // Settings
        Settings settings;

        public event XInputGirometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputGirometerReadingChangedEventHandler(Object sender, XInputGirometerReadingChangedEventArgs e);

        public XInputGirometer(Settings _settings)
        {
            sensor = Gyrometer.GetDefault();
            if (sensor != null)
            {
                UpdateSettings(_settings);

                Console.WriteLine($"Gyrometer initialised.");
                Console.WriteLine($"Gyrometer report interval set to {sensor.ReportInterval}ms");
                Console.WriteLine($"Gyrometer sample pool size set to: {poolsize}");
                Console.WriteLine();

                sensor.ReadingChanged += GyroReadingChanged;
            }
        }

        public void UpdateSettings(Settings _settings)
        {
            settings = _settings;

            // resolution settings
            Rectangle resolution = Screen.PrimaryScreen.Bounds;
            GyroStickRatio = settings.MonitorRatio ? ((float)resolution.Width / (float)resolution.Height) : 1.0f;

            poolsize = settings.MaxSample;
            gyroPool = new Vector3[poolsize];

            sensor.ReportInterval = settings.PullRate < sensor.MinimumReportInterval ? sensor.MinimumReportInterval : settings.PullRate;
        }

        static Vector3 SmoothReading(Vector3 input, float GyroStickAlpha)
        {
            Vector3 lowpass = new Vector3();
            lowpass.X = input.X * GyroStickAlpha + lowpass.X * (1.0f - GyroStickAlpha);
            lowpass.Y = input.Y * GyroStickAlpha + lowpass.Y * (1.0f - GyroStickAlpha);
            lowpass.Z = input.Z * GyroStickAlpha + lowpass.Z * (1.0f - GyroStickAlpha);

            Vector3 medpass = new Vector3();
            medpass.X = GyroStickAlpha * medpass.X + (1.0f - GyroStickAlpha) * lowpass.X;
            medpass.Y = GyroStickAlpha * medpass.Y + (1.0f - GyroStickAlpha) * lowpass.Y;
            medpass.Z = GyroStickAlpha * medpass.Z + (1.0f - GyroStickAlpha) * lowpass.Z;

            Vector3 hipass = new Vector3();
            hipass.X = input.X - medpass.X;
            hipass.Y = input.Y - medpass.Y;
            hipass.Z = input.Z - medpass.Z;

            return hipass;
        }

        static Vector3 NormalizeReading(Vector3 input, float magnitude, float threshold, float alpha)
        {
            float vector = (magnitude > 0 ? magnitude : 1);
            var direction = input / vector;

            if (magnitude - threshold > 0)
            {
                float normalizedMagnitude = Math.Min((magnitude - threshold) / (short.MaxValue - threshold), 1);
                normalizedMagnitude = normalizedMagnitude < alpha ? alpha : normalizedMagnitude;

                return direction * normalizedMagnitude;
            }

            return new Vector3(0, 0, 0);
        }

        void GyroReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            GyrometerReading reading = args.Reading;

            Vector3 input = new Vector3((float)reading.AngularVelocityY, (float)reading.AngularVelocityX, (float)reading.AngularVelocityZ);
            input = SmoothReading(input, GyroStickAlpha);
            input = NormalizeReading(input, GyroStickMagnitude, GyroStickThreshold, GyroStickAlpha);

            gyroPool[gyroPoolIdx] = input;
            gyroPoolIdx++;
            gyroPoolIdx %= poolsize;

            // scale value
            Vector3 posAverage = new Vector3()
            {
                X = (float)(settings.InvertAxisX ? 1.0f : -1.0f) * (float)gyroPool.Select(a => a.X).Average(),
                Y = (float)(settings.InvertAxisY ? 1.0f : -1.0f) * (float)gyroPool.Select(a => a.Y).Average(),
                Z = (float)(settings.InvertAxisZ ? 1.0f : -1.0f) * (float)gyroPool.Select(a => a.Z).Average(),
            };

            posAverage *= settings.Range;
            posAverage.X *= GyroStickRatio; // take screen ratio in consideration 1.7f (16:9)

            posAverage.X = (float)(Math.Sign(posAverage.X) * Math.Pow(Math.Abs(posAverage.X) / Gamepad.RightThumbDeadZone, settings.Aggressivity) * Gamepad.RightThumbDeadZone);
            posAverage.Y = (float)(Math.Sign(posAverage.Y) * Math.Pow(Math.Abs(posAverage.Y) / Gamepad.RightThumbDeadZone, settings.Aggressivity) * Gamepad.RightThumbDeadZone);
            posAverage.Z = (float)(Math.Sign(posAverage.Z) * Math.Pow(Math.Abs(posAverage.Z) / Gamepad.RightThumbDeadZone, settings.Aggressivity) * Gamepad.RightThumbDeadZone);

            // raise event
            XInputGirometerReadingChangedEventArgs newargs = new XInputGirometerReadingChangedEventArgs()
            {
                // gyro2stick
                AngularStickX = posAverage.X,
                AngularStickY = posAverage.Y,
                AngularStickZ = posAverage.Z,
                // udp
                AngularVelocityX = (float)reading.AngularVelocityX,
                AngularVelocityY = (float)reading.AngularVelocityY,
                AngularVelocityZ = (float)reading.AngularVelocityZ
            };
            ReadingChanged?.Invoke(this, newargs);
        }
    }
}
