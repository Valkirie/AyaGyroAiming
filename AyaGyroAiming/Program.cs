using MathNet.Numerics.Statistics;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using PInvoke;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using Windows.Devices.Sensors;
using Windows.Foundation;

namespace AyaGyroAiming
{
    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
        private delegate bool ConsoleEventDelegate(int eventType);

        [DllImport("user32.dll")]
        private static extern int GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(int hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        static extern int GetWindowModuleFileName(int hWnd, StringBuilder text, uint count);
        [DllImport("user32.dll")]
        static extern int GetWindowText(int hWnd, StringBuilder text, int count);

        private static Gyrometer CurrentGyrometer;
        private static IXbox360Controller VirtualXBOX;

        static CultureInfo CurrentCulture;
        static ConsoleEventDelegate CurrentHandler;
        static int CurrenthWnd;

        static uint GyroCursor;
        static Vector3[] GyroVectors;

        // Settings
        static float GyroStickAlpha = 0.2f;
        static float GyroStickSensitivityX = 25.0f;
        static float GyroStickSensitivityY = 25.0f;
        static float GyroStickSensitivityZ = 25.0f;
        static float GyroStickAgressivity = 0.55f;
        static uint GyroMaxSample = 4;
        static bool GyroStickInvertAxisX = false;
        static bool GyroStickInvertAxisY = false;
        static bool GyroStickInvertAxisZ = false;
        static bool EnableGyroscope = true;
        static bool EnableAccelerometer = false; // not implemented

        static bool IsRunning = true;
        static string CurrentPath, CurrentPathIni;

        static List<XInputController> PhysicalControllers = new List<XInputController>();

        static void Main()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

            // settings
            CurrentPath = Directory.GetCurrentDirectory();
            CurrentPathIni = Path.Combine(CurrentPath, "inis");
            CurrentCulture = new CultureInfo("en-US");

            Console.WriteLine($"AyaGyroAiming ({fileVersionInfo.ProductVersion})");
            Console.WriteLine();

            CurrentHandler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(CurrentHandler, true);

            for(int i = 0; i < 4; i++)
                PhysicalControllers.Add(new XInputController((UserIndex)i));

            CurrentGyrometer = Gyrometer.GetDefault();
            if (CurrentGyrometer != null)
            {
                uint minReportInterval = CurrentGyrometer.MinimumReportInterval;
                uint reportInterval = minReportInterval > 16 ? minReportInterval : 16;
                CurrentGyrometer.ReportInterval = reportInterval;
                Console.WriteLine($"Gyrometer initialised.");
                Console.WriteLine($"Report interval set to {reportInterval}ms");

                GyroVectors = new Vector3[GyroMaxSample];
                Console.WriteLine($"Sample pool set to: {GyroMaxSample}");
                Console.WriteLine();

                CurrentGyrometer.ReadingChanged += GyroReadingChanged;
            }
            else
            {
                Console.WriteLine("No Gyrometer detected. Application will stop.");
                Console.ReadLine();
                return;
            }

            ViGEmClient client = new ViGEmClient();
            VirtualXBOX = client.CreateXbox360Controller();
            VirtualXBOX.Connect();

            if (VirtualXBOX != null)
            {
                Console.WriteLine($"Virtual {VirtualXBOX.GetType().Name} initialised.");
                foreach (XInputController PhysicalController in PhysicalControllers.Where(a => a.connected))
                {
                    PhysicalController.SetVirtualController(VirtualXBOX);
                    Console.WriteLine($"Virtual {VirtualXBOX.GetType().Name} attached to {PhysicalController.GetType().Name} {PhysicalController.index}.");
                }
            }
            else
            {
                Console.WriteLine("No Virtual controller detected. Application will stop.");
                Console.ReadLine();
                return;
            }

            Thread MonitorThread = new Thread(MonitorProcess);
            MonitorThread.Start();
        }

        static void MonitorProcess()
        {
            while (IsRunning)
            {
                int hWnd = GetForegroundWindow();

                if (hWnd != CurrenthWnd)
                {
                    uint processId;
                    if (GetWindowThreadProcessId(hWnd, out processId) == 0)
                        continue;

                    Process CurrentProcess = Process.GetProcessById((int)processId);

                    try
                    {
                        FileInfo CurrentFile = new FileInfo(CurrentProcess.MainModule.FileName);
                        string filename = Path.Combine(CurrentPathIni, CurrentFile.Name.Replace("exe", "ini"));

                        // check if a specific profile exists for the foreground executable
                        if (File.Exists(filename))
                        {
                            IniFile MyIni = new IniFile(filename);
                            bool.TryParse(MyIni.Read("EnableGyroscope", "Global"), out EnableGyroscope);
                            bool.TryParse(MyIni.Read("EnableAccelerometer", "Global"), out EnableAccelerometer);

                            uint.TryParse(MyIni.Read("GyroMaxSample", "Gyroscope"), out GyroMaxSample);
                            float.TryParse(MyIni.Read("GyroStickSensitivityX", "Gyroscope"), NumberStyles.AllowDecimalPoint, CurrentCulture, out GyroStickSensitivityX);
                            float.TryParse(MyIni.Read("GyroStickSensitivityY", "Gyroscope"), NumberStyles.AllowDecimalPoint, CurrentCulture, out GyroStickSensitivityY);
                            float.TryParse(MyIni.Read("GyroStickAgressivity", "Gyroscope"), NumberStyles.AllowDecimalPoint, CurrentCulture, out GyroStickAgressivity);
                            bool.TryParse(MyIni.Read("GyroStickInvertAxisX", "Gyroscope"), out GyroStickInvertAxisX);
                            bool.TryParse(MyIni.Read("GyroStickInvertAxisY", "Gyroscope"), out GyroStickInvertAxisY);
                            continue;
                        }
                    }
                    catch (Exception) { }

                    // restore default
                    EnableGyroscope = true;
                    EnableAccelerometer = false;
                    GyroMaxSample = 4;
                    GyroStickSensitivityX = 25.0f;
                    GyroStickSensitivityY = 25.0f;
                    GyroStickAgressivity = 0.55f;
                    GyroStickInvertAxisX = false;
                    GyroStickInvertAxisY = false;

                    CurrenthWnd = hWnd;
                }

                Thread.Sleep(1000);
            }
        }

        static Vector3 lowPass(Vector3 input)
        {
            Vector3 output = new Vector3();
            output.X = input.X * GyroStickAlpha + output.X * (1.0f - GyroStickAlpha);
            output.Y = input.Y * GyroStickAlpha + output.Y * (1.0f - GyroStickAlpha);
            output.Z = input.Z * GyroStickAlpha + output.Z * (1.0f - GyroStickAlpha);
            return output;
        }

        static Vector3 highPass(Vector3 input)
        {
            Vector3 gravity = new Vector3();
            gravity.X = GyroStickAlpha * gravity.X + (1 - GyroStickAlpha) * input.X;
            gravity.Y = GyroStickAlpha * gravity.Y + (1 - GyroStickAlpha) * input.Y;
            gravity.Z = GyroStickAlpha * gravity.Z + (1 - GyroStickAlpha) * input.Z;

            Vector3 output = new Vector3();
            output.X = Math.Max(-140.0f, Math.Min(140.0f, input.X - gravity.X));
            output.Y = Math.Max(-140.0f, Math.Min(140.0f, input.Y - gravity.Y));
            output.Z = Math.Max(-140.0f, Math.Min(140.0f, input.Z - gravity.Z));
            return output;
        }

        static void GyroReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            if (!EnableGyroscope)
                return;

            GyrometerReading reading = args.Reading;

            Vector3 gyro = new Vector3()
            {
                X = (float)reading.AngularVelocityX,
                Y = (float)reading.AngularVelocityY,
                Z = (float)reading.AngularVelocityZ
            };

            Vector3 low = lowPass(gyro);
            Vector3 high = highPass(low);

            SendGyro(high, VirtualXBOX);
        }

        static Vector3 AverageVector(Vector3[] gyro)
        {
            Vector3 Average = new Vector3();
            for (int i = 0; i < gyro.Length; i++)
            {
                Average.X += gyro[i].X;
                Average.Y += gyro[i].Y;
                Average.Z += gyro[i].Z;
            }

            Average.X /= gyro.Length;
            Average.Y /= gyro.Length;
            Average.Z /= gyro.Length;

            return Average;
        }

        static void SendGyro(Vector3 gyro, IXbox360Controller VirtualController)
        {
            double AngularVelocityX = Math.Sign(gyro.X) * Math.Pow((Math.Abs(gyro.X) / GyroStickSensitivityX), GyroStickAgressivity) * GyroStickSensitivityX * 2000;
            double AngularVelocityY = Math.Sign(gyro.Y) * Math.Pow((Math.Abs(gyro.Y) / GyroStickSensitivityY), GyroStickAgressivity) * GyroStickSensitivityY * 2000;
            double AngularVelocityZ = Math.Sign(gyro.Z) * Math.Pow((Math.Abs(gyro.Z) / GyroStickSensitivityZ), GyroStickAgressivity) * GyroStickSensitivityZ * 2000;

            GyroVectors[GyroCursor].X = (GyroStickInvertAxisX ? 1 : -1) * (float)Math.Max(-32767, Math.Min(32767, AngularVelocityX));
            GyroVectors[GyroCursor].Y = (GyroStickInvertAxisY ? 1 : -1) * (float)Math.Max(-32767, Math.Min(32767, AngularVelocityY));
            GyroVectors[GyroCursor].Z = (GyroStickInvertAxisZ ? 1 : -1) * (float)Math.Max(-32767, Math.Min(32767, AngularVelocityZ));

            Vector3 output = AverageVector(GyroVectors);
            foreach (XInputController PhysicalController in PhysicalControllers.Where(a => a.connected))
                PhysicalController.UpdateGyro(output);

            if (GyroCursor < GyroMaxSample - 1)
                GyroCursor++;
            else
                GyroCursor = 0;
        }

        static bool ConsoleEventCallback(int eventType)
        {
            if (VirtualXBOX != null)
                VirtualXBOX.Disconnect();
            IsRunning = false;
            return true;
        }
    }
}
