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
        public short AngularStickX { get; set; }
        public short AngularStickY { get; set; }
        public short AngularStickZ { get; set; }
    }

    public class XInputGirometer
    {
        public Gyrometer gyrometer;
        public uint poolsize;

        // Compute & Maths
        uint poolidx;
        float[] GyroX;
        float[] GyroY;
        float[] GyroZ;

        // const
        const float GyroStickAlpha = 0.2f;

        // Settings
        public float GyroStickMagnitude = 3.5f;
        public float GyroStickThreshold = 0.1f;
        public float GyroStickRange = 3.5f;
        public bool GyroStickInvertAxisX = false;
        public bool GyroStickInvertAxisY = false;
        public bool GyroStickInvertAxisZ = false;
        public bool Enabled = true;

        public event XInputGirometerReadingChangedEventHandler ReadingChanged;
        public delegate void XInputGirometerReadingChangedEventHandler(Object sender, XInputGirometerReadingChangedEventArgs e);

        public XInputGirometer(uint _rate, uint _size)
        {
            gyrometer = Gyrometer.GetDefault();
            if (gyrometer != null)
            {
                poolsize = _size;
                GyroX = new float[_size];
                GyroY = new float[_size];
                GyroZ = new float[_size];

                gyrometer.ReportInterval = _rate < gyrometer.MinimumReportInterval ? gyrometer.MinimumReportInterval : _rate;
                Console.WriteLine($"Gyrometer initialised.");
                Console.WriteLine($"Gyrometer report interval set to {gyrometer.MinimumReportInterval}ms");

                Console.WriteLine($"Gyrometer sample pool size set to: {_size}");
                Console.WriteLine();

                gyrometer.ReadingChanged += GyroReadingChanged;
            }
            else
            {
                Console.WriteLine("No Gyrometer detected. Application will stop.");
                Console.ReadLine();
                return;
            }
        }

        static Vector3 SmoothReading(Vector3 input, float GyroStickAlpha)
        {
            Vector3 lowpass = new Vector3();
            lowpass.X = input.X * GyroStickAlpha + lowpass.X * (1.0f - GyroStickAlpha);
            lowpass.Y = input.Y * GyroStickAlpha + lowpass.Y * (1.0f - GyroStickAlpha);
            lowpass.Z = input.Z * GyroStickAlpha + lowpass.Z * (1.0f - GyroStickAlpha);

            Vector3 medpass = new Vector3();
            medpass.X = GyroStickAlpha * medpass.X + (1 - GyroStickAlpha) * lowpass.X;
            medpass.Y = GyroStickAlpha * medpass.Y + (1 - GyroStickAlpha) * lowpass.Y;
            medpass.Z = GyroStickAlpha * medpass.Z + (1 - GyroStickAlpha) * lowpass.Z;

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
            if (!Enabled)
                return;

            GyrometerReading reading = args.Reading;

            Vector3 input = new Vector3((float)reading.AngularVelocityX, (float)reading.AngularVelocityY, (float)reading.AngularVelocityZ);
            input = SmoothReading(input, GyroStickAlpha);
            input = NormalizeReading(input, GyroStickMagnitude, GyroStickThreshold);

            if (poolidx <= poolsize - 1)
            {
                GyroX[poolidx] = Math.Max(-GyroStickRange, Math.Min(GyroStickRange, input.X));
                GyroY[poolidx] = Math.Max(-GyroStickRange, Math.Min(GyroStickRange, input.Y));
                GyroZ[poolidx] = Math.Max(-GyroStickRange, Math.Min(GyroStickRange, input.Z));
                poolidx++;
            }
            else
                poolidx = 0;

            // scale value
            Vector3 posAverage = new Vector3()
            {
                X = GyroStickInvertAxisX ? 1 : -1 * GyroY.Median(),
                Y = GyroStickInvertAxisY ? 1 : -1 * GyroX.Median(),
                Z = GyroStickInvertAxisZ ? 1 : -1 * GyroZ.Median(),
            };
            posAverage *= 10000.0f;

            // raise event
            XInputGirometerReadingChangedEventArgs newargs = new XInputGirometerReadingChangedEventArgs()
            {
                AngularStickX = (short)posAverage.X,
                AngularStickY = (short)posAverage.Y,
                AngularStickZ = (short)posAverage.Z
            };
            ReadingChanged?.Invoke(this, newargs);
        }
    }
}
