using SharpDX.XInput;
using System;
using System.Linq;
using System.Numerics;
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
        uint poolidx;
        float[] GyroX;
        float[] GyroY;
        float[] GyroZ;

        // const
        const float GyroStickAlpha = 0.2f;

        // Settings
        Settings settings;

        public event XInputGirometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputGirometerReadingChangedEventHandler(Object sender, XInputGirometerReadingChangedEventArgs e);

        public XInputGirometer(Settings _settings)
        {
            sensor = Gyrometer.GetDefault();
            if (sensor != null)
            {
                settings = _settings;

                poolsize = settings.GyroMaxSample;
                GyroX = new float[poolsize];
                GyroY = new float[poolsize];
                GyroZ = new float[poolsize];

                sensor.ReportInterval = settings.GyroPullRate < sensor.MinimumReportInterval ? sensor.MinimumReportInterval : settings.GyroPullRate;
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

        static Vector3 NormalizeReading(Vector3 input, float magnitude, float threshold)
        {
            float vector = (magnitude > 0 ? magnitude : 1);
            var direction = input / vector;

            float normalizedMagnitude = 0.0f;
            if (magnitude - threshold > 0)
            {
                normalizedMagnitude = Math.Min((magnitude - threshold) / (short.MaxValue - threshold), 1);
                normalizedMagnitude = normalizedMagnitude < 0.2f ? 0.2f : normalizedMagnitude;

                return direction * normalizedMagnitude;
            }

            return new Vector3(0, 0, 0);
        }

        void GyroReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            GyrometerReading reading = args.Reading;

            Vector3 input = new Vector3((float)reading.AngularVelocityY, (float)reading.AngularVelocityX, (float)reading.AngularVelocityZ);
            input = SmoothReading(input, GyroStickAlpha);
            input = NormalizeReading(input, settings.GyroStickMagnitude, settings.GyroStickThreshold);

            GyroX[poolidx] = Math.Max(-settings.GyroStickRange, Math.Min(settings.GyroStickRange, input.X));
            GyroY[poolidx] = Math.Max(-settings.GyroStickRange, Math.Min(settings.GyroStickRange, input.Y));
            GyroZ[poolidx] = Math.Max(-settings.GyroStickRange, Math.Min(settings.GyroStickRange, input.Z));
            poolidx++;
            poolidx %= poolsize;

            // scale value
            Vector3 posAverage = new Vector3()
            {
                X = (float)(settings.GyroStickInvertAxisX ? 1.0f : -1.0f) * (float)GyroX.Average(),
                Y = (float)(settings.GyroStickInvertAxisY ? 1.0f : -1.0f) * (float)GyroY.Average(),
                Z = (float)(settings.GyroStickInvertAxisZ ? 1.0f : -1.0f) * (float)GyroZ.Average(),
            };
            posAverage *= Gamepad.RightThumbDeadZone;

            posAverage.X = (float)(Math.Sign(posAverage.X) * Math.Pow(Math.Abs(posAverage.X) / settings.GyroStickRange, settings.GyroStickAggressivity) * settings.GyroStickRange);
            posAverage.Y = (float)(Math.Sign(posAverage.Y) * Math.Pow(Math.Abs(posAverage.Y) / settings.GyroStickRange, settings.GyroStickAggressivity) * settings.GyroStickRange);
            posAverage.Z = (float)(Math.Sign(posAverage.Z) * Math.Pow(Math.Abs(posAverage.Z) / settings.GyroStickRange, settings.GyroStickAggressivity) * settings.GyroStickRange);

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
