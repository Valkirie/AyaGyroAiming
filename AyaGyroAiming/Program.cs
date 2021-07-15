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
        // DLLImports
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
        [DllImport("user32.dll")]
        private static extern int GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(int hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        static extern int GetWindowModuleFileName(int hWnd, StringBuilder text, uint count);
        [DllImport("user32.dll")]
        static extern int GetWindowText(int hWnd, StringBuilder text, int count);

        // controllers vars
        static List<XInputController> PhysicalControllers = new List<XInputController>();
        static IXbox360Controller VirtualXBOX;
        static XInputGirometer Gyrometer;

        private delegate bool ConsoleEventDelegate(int eventType);
        static ConsoleEventDelegate CurrentHandler;
        static int CurrenthWnd;

        static bool IsRunning = true;
        static string CurrentPath, CurrentPathIni;

        // settings vars
        static bool EnableGyroscope;
        static bool EnableAccelerometer;
        static uint GyroPullRate;
        static uint GyroMaxSample;
        static float GyroStickMagnitude;
        static float GyroStickThreshold;
        static float GyroStickRange;
        static bool GyroStickInvertAxisX;
        static bool GyroStickInvertAxisY;
        static bool GyroStickInvertAxisZ;
        static float GyroStickAggressivity;

        static void Main()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

            // paths
            CurrentPath = Directory.GetCurrentDirectory();
            CurrentPathIni = Path.Combine(CurrentPath, "inis");

            // default settings
            EnableGyroscope = Properties.Settings.Default.EnableGyroscope;
            EnableAccelerometer = Properties.Settings.Default.EnableAccelerometer;
            GyroPullRate = Properties.Settings.Default.GyroPullRate;
            GyroMaxSample = Properties.Settings.Default.GyroMaxSample;
            GyroStickMagnitude = Properties.Settings.Default.GyroStickMagnitude;
            GyroStickThreshold = Properties.Settings.Default.GyroStickThreshold;
            GyroStickAggressivity = Properties.Settings.Default.GyroStickAggressivity;
            GyroStickRange = Properties.Settings.Default.GyroStickRange;
            GyroStickInvertAxisX = Properties.Settings.Default.GyroStickInvertAxisX;
            GyroStickInvertAxisY = Properties.Settings.Default.GyroStickInvertAxisY;
            GyroStickInvertAxisZ = Properties.Settings.Default.GyroStickInvertAxisZ;

            Console.WriteLine($"AyaGyroAiming ({fileVersionInfo.ProductVersion})");
            Console.WriteLine();

            CurrentHandler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(CurrentHandler, true);

            for(int i = 0; i < 4; i++)
                PhysicalControllers.Add(new XInputController((UserIndex)i));

            // default is 10ms rating and 10 samples
            Gyrometer = new XInputGirometer(EnableGyroscope, GyroPullRate, GyroMaxSample, GyroStickMagnitude, GyroStickThreshold, GyroStickAggressivity, GyroStickRange, GyroStickInvertAxisX, GyroStickInvertAxisY, GyroStickInvertAxisZ);

            if (Gyrometer.motion == null)
            {
                Console.WriteLine("No Gyrometer detected. Application will stop.");
                Console.ReadLine();
                return;
            }

            ViGEmClient client = new ViGEmClient();
            VirtualXBOX = client.CreateXbox360Controller();

            if (VirtualXBOX == null)
            {
                Console.WriteLine("No Virtual controller detected. Application will stop.");
                Console.ReadLine();
                return;
            }

            VirtualXBOX.Connect();
            Console.WriteLine($"Virtual {VirtualXBOX.GetType().Name} initialised.");
            foreach (XInputController PhysicalController in PhysicalControllers.Where(a => a.connected))
            {
                PhysicalController.SetVirtualController(VirtualXBOX);
                PhysicalController.SetGyroscope(Gyrometer);
                Console.WriteLine($"Virtual {VirtualXBOX.GetType().Name} attached to {PhysicalController.GetType().Name} {PhysicalController.index}.");
            }

            // monitor processes and apply specific profile
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

                            bool EnableGyroscope = MyIni.ReadBool("EnableGyroscope", "Global");
                            bool EnableAccelerometer = MyIni.ReadBool("EnableAccelerometer", "Global");

                            float GyroStickMagnitude = MyIni.ReadFloat("GyroStickMagnitude", "Gyroscope");
                            float GyroStickThreshold = MyIni.ReadFloat("GyroStickThreshold", "Gyroscope");
                            float GyroStickAggressivity = MyIni.ReadFloat("GyroStickAggressivity", "Gyroscope");
                            float GyroStickRange = MyIni.ReadFloat("GyroStickRange", "Gyroscope");

                            bool GyroStickInvertAxisX = MyIni.ReadBool("GyroStickInvertAxisX", "Gyroscope");
                            bool GyroStickInvertAxisY = MyIni.ReadBool("GyroStickInvertAxisX", "Gyroscope");
                            bool GyroStickInvertAxisZ = MyIni.ReadBool("GyroStickInvertAxisX", "Gyroscope");

                            foreach (XInputController PhysicalController in PhysicalControllers.Where(a => a.connected))
                                PhysicalController.gyrometer.UpdateSettings(EnableGyroscope, GyroStickMagnitude, GyroStickThreshold, GyroStickAggressivity, GyroStickRange, GyroStickInvertAxisX, GyroStickInvertAxisY, GyroStickInvertAxisZ);

                            Console.WriteLine($"Gyroscope settings applied for {CurrentFile.Name}");
                        }
                    }
                    catch (Exception) { }

                    // restore default
                    foreach (XInputController PhysicalController in PhysicalControllers.Where(a => a.connected))
                        PhysicalController.gyrometer.UpdateSettings(EnableGyroscope, GyroStickMagnitude, GyroStickThreshold, GyroStickAggressivity, GyroStickRange, GyroStickInvertAxisX, GyroStickInvertAxisY, GyroStickInvertAxisZ);

                    CurrenthWnd = hWnd;
                }

                Thread.Sleep(1000);
            }
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
