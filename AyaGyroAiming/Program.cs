using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;

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
        static IDualShock4Controller VirtualController;
        static XInputGirometer Gyrometer;
        static XInputAccelerometer Accelerometer;
        static UdpServer UDPServer;

        private delegate bool ConsoleEventDelegate(int eventType);
        static ConsoleEventDelegate CurrentHandler;
        static int CurrenthWnd;

        static bool IsRunning = true;
        static string CurrentPath, CurrentPathCli;
        static PhysicalAddress PadMacAddress = new PhysicalAddress(new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 });

        // settings vars
        static int UdpPort;
        static StringCollection HidHideDevices;

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
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathCli = @"C:\Program Files\Nefarius Software Solutions e.U\HidHideCLI\HidHideCLI.exe";

            // settings
            UdpPort = Properties.Settings.Default.UdpPort; // 26760
            HidHideDevices = Properties.Settings.Default.HidHideDevices; // not yet implemented

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
                string VID = Utils.Between(d.deviceInstancePath.ToLower(), "vid_", "&");
                string PID = Utils.Between(d.deviceInstancePath.ToLower(), "pid_", "&");

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
                VirtualController = client.CreateDualShock4Controller();

                if (VirtualController == null)
                {
                    Console.WriteLine("No Virtual controller detected. Application will stop.");
                    Console.ReadLine();
                    Environment.Exit(0);
                }
            }
            catch (Exception)
            {
                Console.WriteLine("ViGEm is missing. Please get it from: https://github.com/ViGEm/ViGEmBus/releases");
                Console.ReadLine();
                return;
            }

            CurrentHandler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(CurrentHandler, true);

            // prepare physical controller
            PhysicalController = new XInputController(0, PadMacAddress);

            if (PhysicalController == null)
            {
                Console.WriteLine("No physical controller detected. Application will stop.");
                Console.ReadLine();
                Environment.Exit(0);
            }

            // default is 10ms rating and 10 samples
            Gyrometer = new XInputGirometer();
            if (Gyrometer.sensor == null)
            {
                Console.WriteLine("No Gyrometer detected. Application will stop.");
                Console.ReadLine();
                Environment.Exit(0);
            }

            // default is 10ms rating
            Accelerometer = new XInputAccelerometer();
            if (Accelerometer.sensor == null)
            {
                Console.WriteLine("No Accelerometer detected. Application will stop.");
                Console.ReadLine();
                Environment.Exit(0);
            }

            // start UDP server
            UDPServer = new UdpServer(PadMacAddress);
            UDPServer.Start(UdpPort);

            if (UDPServer != null)
            {
                Console.WriteLine($"UDP server has started. Listening to port: {UdpPort}");
                Console.WriteLine();
                PhysicalController.SetUdpServer(UDPServer);
            }

            VirtualController.Connect();
            Console.WriteLine($"Virtual {VirtualController.GetType().Name} initialised.");
            PhysicalController.SetVirtualController(VirtualController);
            PhysicalController.SetGyroscope(Gyrometer);
            PhysicalController.SetAccelerometer(Accelerometer);
            Console.WriteLine($"Virtual {VirtualController.GetType().Name} attached to {PhysicalController.GetType().Name} {PhysicalController.index}.");

            // monitor device battery status and notify UDP server
            Thread MonitorBattery = new Thread(MonitorBatteryLife);
            MonitorBattery.Start();
        }

        static void MonitorBatteryLife()
        {
            while (IsRunning)
            {
                BatteryChargeStatus ChargeStatus = SystemInformation.PowerStatus.BatteryChargeStatus;
                // float ChargePercent = SystemInformation.PowerStatus.BatteryLifePercent;

                if (ChargeStatus.HasFlag(BatteryChargeStatus.Charging))
                    UDPServer.padMeta.BatteryStatus = DsBattery.Charging;
                else if (ChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery))
                    UDPServer.padMeta.BatteryStatus = DsBattery.None;
                else if (ChargeStatus.HasFlag(BatteryChargeStatus.High))
                    UDPServer.padMeta.BatteryStatus = DsBattery.High;
                else if (ChargeStatus.HasFlag(BatteryChargeStatus.Low))
                    UDPServer.padMeta.BatteryStatus = DsBattery.Low;
                else if (ChargeStatus.HasFlag(BatteryChargeStatus.Critical))
                    UDPServer.padMeta.BatteryStatus = DsBattery.Dying;
                else
                    UDPServer.padMeta.BatteryStatus = DsBattery.Medium;

                Thread.Sleep(1000);
            }
        }

        static bool ConsoleEventCallback(int eventType)
        {
            try
            {
                if (VirtualController != null)
                    VirtualController.Disconnect();
            }
            catch (Exception) { }

            IsRunning = false;
            hidder.SetCloaking(false);

            return true;
        }
    }
}
