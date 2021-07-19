using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
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
        float GyroStickMagnitude;
        float GyroStickThreshold;
        float GyroStickAggressivity;
        float GyroStickRange;
        bool GyroStickInvertAxisX;
        bool GyroStickInvertAxisY;
        bool GyroStickInvertAxisZ;
        public bool EnableGyroAiming;

        public event XInputGirometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputGirometerReadingChangedEventHandler(Object sender, XInputGirometerReadingChangedEventArgs e);

        public XInputGirometer(uint _rate, uint _size, float _magnitude, float _threshold, float _aggro, float _range, bool _invX, bool _invY, bool _invZ)
        {
            sensor = Gyrometer.GetDefault();
            if (sensor != null)
            {
                GyroStickMagnitude = _magnitude;
                GyroStickThreshold = _threshold;
                GyroStickAggressivity = _aggro;
                GyroStickRange = _range;
                GyroStickInvertAxisX = _invX;
                GyroStickInvertAxisY = _invY;
                GyroStickInvertAxisZ = _invZ;

                poolsize = _size;
                GyroX = new float[_size];
                GyroY = new float[_size];
                GyroZ = new float[_size];

                sensor.ReportInterval = _rate < sensor.MinimumReportInterval ? sensor.MinimumReportInterval : _rate;
                Console.WriteLine($"Gyrometer initialised.");
                Console.WriteLine($"Gyrometer report interval set to {sensor.ReportInterval}ms");
                Console.WriteLine($"Gyrometer sample pool size set to: {_size}");
                Console.WriteLine();

                sensor.ReadingChanged += GyroReadingChanged;
            }
        }

        public void UpdateSettings(bool _enable, float _magnitude, float _threshold, float _aggro, float _range, bool _invX, bool _invY, bool _invZ)
        {
            EnableGyroAiming = _enable;
            GyroStickMagnitude = _magnitude;
            GyroStickThreshold = _threshold;
            GyroStickAggressivity = _aggro;
            GyroStickRange = _range;
            GyroStickInvertAxisX = _invX;
            GyroStickInvertAxisY = _invY;
            GyroStickInvertAxisZ = _invZ;
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
            input.X = (float)(Math.Sign(input.X) * Math.Pow(Math.Abs(input.X) / GyroStickRange, GyroStickAggressivity) * GyroStickRange);
            input.Y = (float)(Math.Sign(input.Y) * Math.Pow(Math.Abs(input.Y) / GyroStickRange, GyroStickAggressivity) * GyroStickRange);
            input.Z = (float)(Math.Sign(input.Z) * Math.Pow(Math.Abs(input.Z) / GyroStickRange, GyroStickAggressivity) * GyroStickRange);

            input = SmoothReading(input, GyroStickAlpha);
            input = NormalizeReading(input, GyroStickMagnitude, GyroStickThreshold);

            GyroX[poolidx] = Math.Max(-GyroStickRange, Math.Min(GyroStickRange, input.X));
            GyroY[poolidx] = Math.Max(-GyroStickRange, Math.Min(GyroStickRange, input.Y));
            GyroZ[poolidx] = Math.Max(-GyroStickRange, Math.Min(GyroStickRange, input.Z));
            poolidx++;
            poolidx %= poolsize;

            // scale value
            Vector3 posAverage = new Vector3()
            {
                X = (float)(GyroStickInvertAxisX ? 1.0f : -1.0f) * (float)GyroX.Average(),
                Y = (float)(GyroStickInvertAxisY ? 1.0f : -1.0f) * (float)GyroY.Average(),
                Z = (float)(GyroStickInvertAxisZ ? 1.0f : -1.0f) * (float)GyroZ.Average(),
            };
            posAverage *= 10000.0f;

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
