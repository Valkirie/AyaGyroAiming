using MathNet.Numerics.Statistics;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using PInvoke;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using Windows.Devices.Sensors;
using Windows.Foundation;
using static AyaGyroAiming.UdpServer;

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
        static XInputController PhysicalController;
        static IXbox360Controller VirtualXBOX;
        static XInputGirometer Gyrometer;
        static XInputAccelerometer Accelerometer;

        private delegate bool ConsoleEventDelegate(int eventType);
        static ConsoleEventDelegate CurrentHandler;
        static int CurrenthWnd;

        static bool IsRunning = true;
        static string CurrentPath, CurrentPathIni;
        static PhysicalAddress PadMacAddress = new PhysicalAddress(new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 });

        // settings vars
        static bool EnableGyroAiming;
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
            UpdateSettings();

            Console.WriteLine($"AyaGyroAiming ({fileVersionInfo.ProductVersion})");
            Console.WriteLine();

            CurrentHandler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(CurrentHandler, true);

            // prepare physical controller
            PhysicalController = new XInputController(0, 10, PadMacAddress);

            if (PhysicalController == null)
            {
                Console.WriteLine("No physical controller detected. Application will stop.");
                Console.ReadLine();
                return;
            }

            // start UDP server (temp)
            UdpServer _udpServer = new UdpServer(PadMacAddress);
            _udpServer.Start(26760);

            if (_udpServer != null)
            {
                Console.WriteLine($"UDP server has started. Listening to port: 26760");
                Console.WriteLine();
                PhysicalController.SetUdpServer(_udpServer);
            }

            // default is 10ms rating and 10 samples
            Gyrometer = new XInputGirometer(GyroPullRate, GyroMaxSample, GyroStickMagnitude, GyroStickThreshold, GyroStickAggressivity, GyroStickRange, GyroStickInvertAxisX, GyroStickInvertAxisY, GyroStickInvertAxisZ);

            if (Gyrometer.sensor == null)
            {
                Console.WriteLine("No Gyrometer detected. Application will stop.");
                Console.ReadLine();
                return;
            }

            // default is 10ms rating
            Accelerometer = new XInputAccelerometer(GyroPullRate);

            if (Accelerometer.sensor == null)
            {
                Console.WriteLine("No Accelerometer detected. Application will stop.");
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
            PhysicalController.SetVirtualController(VirtualXBOX);
            PhysicalController.SetGyroscope(Gyrometer);
            PhysicalController.SetAccelerometer(Accelerometer);
            Console.WriteLine($"Virtual {VirtualXBOX.GetType().Name} attached to {PhysicalController.GetType().Name} {PhysicalController.index}.");

            // monitor processes and apply specific profile
            Thread MonitorThread = new Thread(MonitorProcess);
            MonitorThread.Start();

            // listen to user inputs (a bit too rigid, improve me)
            Thread MonitorConsole = new Thread(ConsoleListener);
            MonitorConsole.Start();
        }

        static void ConsoleListener()
        {
            while (IsRunning)
            {
                string input = Console.ReadLine();
                string[] array = input.Split(' ');

                string command = array[0];
                string variable;

                try
                {
                    switch (command)
                    {
                        case "/set":
                            if (array.Length < 3)
                                Console.WriteLine("expected format: /set settings value");
                            variable = array[1];

                            switch (Type.GetTypeCode(Properties.Settings.Default[variable].GetType()))
                            {
                                case TypeCode.Boolean:
                                    Properties.Settings.Default[variable] = bool.Parse(array[2]);
                                    break;
                                case TypeCode.Single:
                                case TypeCode.Decimal:
                                    Properties.Settings.Default[variable] = float.Parse(array[2]);
                                    break;
                                case TypeCode.Int16:
                                case TypeCode.Int32:
                                case TypeCode.Int64:
                                    Properties.Settings.Default[variable] = int.Parse(array[2]);
                                    break;
                                case TypeCode.UInt16:
                                case TypeCode.UInt32:
                                case TypeCode.UInt64:
                                    Properties.Settings.Default[variable] = uint.Parse(array[2]);
                                    break;
                            }
                            Console.WriteLine($"{variable} set to: {array[2]}");
                            Properties.Settings.Default.Save();
                            UpdateSettings();
                            break;
                        case "/get":
                            if (array.Length < 2)
                                Console.WriteLine("expected format: /get settings");
                            variable = array[1];
                            Console.WriteLine($"{variable}: {Properties.Settings.Default[variable]}");
                            break;
                        case "/cpl":
                            Process.Start("joy.cpl");
                            break;
                        case "/help":
                            Console.WriteLine($"Available settings are:");
                            foreach (SettingsProperty setting in Properties.Settings.Default.Properties)
                                Console.WriteLine($"\t{setting.Name}");
                            Console.WriteLine();
                            Console.WriteLine("Availables commands are:");
                            Console.WriteLine("\t/set settings value (define global value)");
                            Console.WriteLine("\t/get settings (retrieve global value)");
                            Console.WriteLine("\t/cpl (display the Game Controllers)");
                            break;
                    }

                }catch(Exception /*ex*/) { }
            }
        }

        static void UpdateSettings()
        {
            EnableGyroAiming = Properties.Settings.Default.EnableGyroAiming;
            GyroPullRate = Properties.Settings.Default.GyroPullRate;
            GyroMaxSample = Properties.Settings.Default.GyroMaxSample;
            GyroStickMagnitude = Properties.Settings.Default.GyroStickMagnitude;
            GyroStickThreshold = Properties.Settings.Default.GyroStickThreshold;
            GyroStickAggressivity = Properties.Settings.Default.GyroStickAggressivity;
            GyroStickRange = Properties.Settings.Default.GyroStickRange;
            GyroStickInvertAxisX = Properties.Settings.Default.GyroStickInvertAxisX;
            GyroStickInvertAxisY = Properties.Settings.Default.GyroStickInvertAxisY;
            GyroStickInvertAxisZ = Properties.Settings.Default.GyroStickInvertAxisZ;

            // update controller settings
            if (PhysicalController != null)
                PhysicalController.gyrometer.UpdateSettings(EnableGyroAiming, GyroStickMagnitude, GyroStickThreshold, GyroStickAggressivity, GyroStickRange, GyroStickInvertAxisX, GyroStickInvertAxisY, GyroStickInvertAxisZ);
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
                        string filename = Path.Combine(CurrentPathIni, CurrentFile.Name.Replace("exe", "ini")).ToLower();

                        // check if a specific profile exists for the foreground executable
                        if (File.Exists(filename))
                        {
                            IniFile MyIni = new IniFile(filename);

                            bool EnableGyroAiming = MyIni.ReadBool("EnableGyroAiming", "Gyroscope");

                            float GyroStickMagnitude = MyIni.ReadFloat("GyroStickMagnitude", "Gyroscope");
                            float GyroStickThreshold = MyIni.ReadFloat("GyroStickThreshold", "Gyroscope");
                            float GyroStickAggressivity = MyIni.ReadFloat("GyroStickAggressivity", "Gyroscope");
                            float GyroStickRange = MyIni.ReadFloat("GyroStickRange", "Gyroscope");

                            bool GyroStickInvertAxisX = MyIni.ReadBool("GyroStickInvertAxisX", "Gyroscope");
                            bool GyroStickInvertAxisY = MyIni.ReadBool("GyroStickInvertAxisX", "Gyroscope");
                            bool GyroStickInvertAxisZ = MyIni.ReadBool("GyroStickInvertAxisX", "Gyroscope");

                            // update controller settings
                            if (PhysicalController != null)
                                PhysicalController.gyrometer.UpdateSettings(EnableGyroAiming, GyroStickMagnitude, GyroStickThreshold, GyroStickAggressivity, GyroStickRange, GyroStickInvertAxisX, GyroStickInvertAxisY, GyroStickInvertAxisZ);

                            Console.WriteLine($"Gyroscope settings applied for {CurrentFile.Name}");
                        }
                        else
                            UpdateSettings();
                    }
                    catch (Exception) { }

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
