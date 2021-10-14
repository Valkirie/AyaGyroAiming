﻿using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Management;
using System.Windows.Forms;
using System.Drawing;

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
        static string CurrentPath, CurrentPathIni, CurrentPathCli;
        static PhysicalAddress PadMacAddress = new PhysicalAddress(new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 });

        // settings vars
        static Settings settings = new Settings();
        static int UdpPort;
        static StringCollection HidHideDevices;
        static bool EnableScreenRatio;

        static HidHide hidder;

        static void Main()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

            Console.WriteLine($"AyaGyroAiming ({fileVersionInfo.ProductVersion})");
            Console.WriteLine();

            // paths
            CurrentPath = Directory.GetCurrentDirectory();
            CurrentPathIni = Path.Combine(CurrentPath, "profiles");
            CurrentPathCli = @"C:\Program Files\Nefarius Software Solutions e.U\HidHideCLI\HidHideCLI.exe";

            // settings
            UpdateSettings();

            // resolution settings
            Rectangle resolution = Screen.PrimaryScreen.Bounds;
            float ratio = EnableScreenRatio ? ((float)resolution.Width / (float)resolution.Height) : 1.0f;

            if (!File.Exists(CurrentPathCli))
            {
                Console.WriteLine("HidHide is missing. Please get it from: https://github.com/ViGEm/HidHide/releases");
                Console.ReadLine();
                return;
            }

            // initialize HidHide
            hidder = new HidHide(CurrentPathCli);
            hidder.RegisterApplication(Assembly.GetExecutingAssembly().Location);
            hidder.RegisterApplication(@"C:\Program Files (x86)\AYASpace\AYASpace.exe");

            // todo : store default baseContainerDeviceInstancePath somewhere
            foreach (Device d in hidder.GetDevices().Where(a => a.gamingDevice))
            {
                // deviceInstancePath = "HID\VID_045E&PID_028E&IG_00\8&21ef185b&0&0000"
                string VID = Between(d.deviceInstancePath, "VID_", "&");
                string PID = Between(d.deviceInstancePath, "PID_", "&");

                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE \"%VID_{VID}&PID_{PID}%\"";

                var moSearch = new ManagementObjectSearcher(query);
                var moCollection = moSearch.Get();

                foreach (ManagementObject mo in moCollection)
                {
                    foreach (var item in mo.Properties)
                    {
                        if (item.Name == "DeviceID")
                        {
                            string DeviceID = ((string)item.Value);
                            hidder.HideDevice(DeviceID);
                            hidder.HideDevice(d.deviceInstancePath);
                            Console.WriteLine($"HideDevice hidding {DeviceID}");
                            break;
                        }
                    }
                }
            }

            hidder.SetCloaking(true);

            // initialize ViGem
            try
            {
                ViGEmClient client = new ViGEmClient();
                VirtualXBOX = client.CreateXbox360Controller();

                if (VirtualXBOX == null)
                {
                    Console.WriteLine("No Virtual controller detected. Application will stop.");
                    Console.ReadLine();
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ViGEm is missing. Please get it from: https://github.com/ViGEm/ViGEmBus/releases");
                Console.ReadLine();
                return;
            }

            CurrentHandler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(CurrentHandler, true);

            // prepare physical controller
            PhysicalController = new XInputController(0, settings, PadMacAddress);

            if (PhysicalController == null)
            {
                Console.WriteLine("No physical controller detected. Application will stop.");
                Console.ReadLine();
                Environment.Exit(0);
            }

            // default is 10ms rating and 10 samples
            Gyrometer = new XInputGirometer(settings, ratio);
            if (Gyrometer.sensor == null)
            {
                Console.WriteLine("No Gyrometer detected. Application will stop.");
                Console.ReadLine();
                Environment.Exit(0);
            }

            // default is 10ms rating
            Accelerometer = new XInputAccelerometer(settings);
            if (Accelerometer.sensor == null)
            {
                Console.WriteLine("No Accelerometer detected. Application will stop.");
                Console.ReadLine();
                Environment.Exit(0);
            }

            // start UDP server (temp)
            UdpServer _udpServer = new UdpServer(PadMacAddress);
            _udpServer.Start(UdpPort);

            if (_udpServer != null)
            {
                Console.WriteLine($"UDP server has started. Listening to port: {UdpPort}");
                Console.WriteLine();
                PhysicalController.SetUdpServer(_udpServer);
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

        static string Between(string STR, string FirstString, string LastString)
        {
            string FinalString;
            int Pos1 = STR.IndexOf(FirstString) + FirstString.Length;
            int Pos2 = STR.IndexOf(LastString, Pos1);
            FinalString = STR.Substring(Pos1, Pos2 - Pos1);
            return FinalString;
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
                                default:
                                    Properties.Settings.Default[variable] = array[2];
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

                }
                catch (Exception /*ex*/) { }
            }
        }

        static void UpdateSettings()
        {
            settings.EnableGyroAiming = Properties.Settings.Default.EnableGyroAiming;
            settings.GyroPullRate = Properties.Settings.Default.GyroPullRate;
            settings.GyroMaxSample = Properties.Settings.Default.GyroMaxSample;
            settings.GyroStickAggressivity = Properties.Settings.Default.GyroStickAggressivity;
            settings.GyroStickRange = Properties.Settings.Default.GyroStickRange;
            settings.GyroStickInvertAxisX = Properties.Settings.Default.GyroStickInvertAxisX;
            settings.GyroStickInvertAxisY = Properties.Settings.Default.GyroStickInvertAxisY;
            settings.GyroStickInvertAxisZ = Properties.Settings.Default.GyroStickInvertAxisZ;
            settings.TriggerString = Properties.Settings.Default.TriggerString;

            UdpPort = Properties.Settings.Default.UdpPort; // 26760
            HidHideDevices = Properties.Settings.Default.HidHideDevices; // not yet implemented
            EnableScreenRatio = Properties.Settings.Default.EnableScreenRatio;

            // update controller settings
            if (PhysicalController != null)
                PhysicalController.UpdateSettings(settings);
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

                    try
                    {
                        Process CurrentProcess = Process.GetProcessById((int)processId);

                        // check if a specific profile exists for the foreground executable
                        string filename = $"{CurrentPathIni}\\{CurrentProcess.ProcessName}.json";
                        if (File.Exists(filename))
                        {
                            string jsonString = File.ReadAllText(filename);
                            Settings settings = JsonSerializer.Deserialize<Settings>(jsonString);

                            // update controller settings
                            PhysicalController.UpdateSettings(settings);
                            Console.WriteLine($"Gyroscope settings applied for {CurrentProcess.ProcessName}");
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
            try
            {
                if (VirtualXBOX != null)
                    VirtualXBOX.Disconnect();
            }
            catch (Exception) { }

            IsRunning = false;
            hidder.SetCloaking(false);

            return true;
        }
    }
}
