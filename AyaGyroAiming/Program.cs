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

        private static IXbox360Controller VirtualXBOX;
        private static XInputGirometer Girometer;

        static ConsoleEventDelegate CurrentHandler;
        static int CurrenthWnd;

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

            Console.WriteLine($"AyaGyroAiming ({fileVersionInfo.ProductVersion})");
            Console.WriteLine();

            CurrentHandler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(CurrentHandler, true);

            for(int i = 0; i < 4; i++)
                PhysicalControllers.Add(new XInputController((UserIndex)i));

            // default is 10ms rating and 10 samples
            Girometer = new XInputGirometer(10, 10);

            ViGEmClient client = new ViGEmClient();
            VirtualXBOX = client.CreateXbox360Controller();
            VirtualXBOX.Connect();

            if (VirtualXBOX != null)
            {
                Console.WriteLine($"Virtual {VirtualXBOX.GetType().Name} initialised.");
                foreach (XInputController PhysicalController in PhysicalControllers.Where(a => a.connected))
                {
                    PhysicalController.SetVirtualController(VirtualXBOX);
                    PhysicalController.SetGyroscope(Girometer);
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

                            bool EnableGyroscope = MyIni.ReadBool("EnableGyroscope", "Global");
                            bool EnableAccelerometer = MyIni.ReadBool("EnableAccelerometer", "Global");
                            uint GyroMaxSample = MyIni.ReadUInt("GyroMaxSample", "Gyroscope");

                            float GyroStickMagnitude = MyIni.ReadFloat("GyroStickMagnitude", "Gyroscope");
                            float GyroStickThreshold = MyIni.ReadFloat("GyroStickThreshold", "Gyroscope");
                            float GyroStickRange = MyIni.ReadFloat("GyroStickRange", "Gyroscope");

                            bool GyroStickInvertAxisX = MyIni.ReadBool("GyroStickInvertAxisX", "Gyroscope");
                            bool GyroStickInvertAxisY = MyIni.ReadBool("GyroStickInvertAxisX", "Gyroscope");
                            bool GyroStickInvertAxisZ = MyIni.ReadBool("GyroStickInvertAxisX", "Gyroscope");

                            foreach (XInputController PhysicalController in PhysicalControllers.Where(a => a.connected))
                            {
                                PhysicalController.gyrometer.Enabled = EnableGyroscope;
                                PhysicalController.gyrometer.poolsize = GyroMaxSample;
                                PhysicalController.gyrometer.GyroStickMagnitude = GyroStickMagnitude;
                                PhysicalController.gyrometer.GyroStickThreshold = GyroStickThreshold;
                                PhysicalController.gyrometer.GyroStickRange = GyroStickRange;
                                PhysicalController.gyrometer.GyroStickInvertAxisX = GyroStickInvertAxisX;
                                PhysicalController.gyrometer.GyroStickInvertAxisY = GyroStickInvertAxisY;
                                PhysicalController.gyrometer.GyroStickInvertAxisZ = GyroStickInvertAxisZ;
                            }

                            Console.WriteLine($"Gyroscope settings applied for {CurrentFile.Name}");
                        }
                    }
                    catch (Exception) { }

                    // restore default : dirty
                    foreach (XInputController PhysicalController in PhysicalControllers.Where(a => a.connected))
                    {
                        PhysicalController.gyrometer.Enabled = true;
                        PhysicalController.gyrometer.poolsize = 10;
                        PhysicalController.gyrometer.GyroStickMagnitude = 3.5f;
                        PhysicalController.gyrometer.GyroStickThreshold = 0.1f;
                        PhysicalController.gyrometer.GyroStickRange = 3.5f;
                        PhysicalController.gyrometer.GyroStickInvertAxisX = false;
                        PhysicalController.gyrometer.GyroStickInvertAxisY = false;
                        PhysicalController.gyrometer.GyroStickInvertAxisZ = false;
                    }

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
