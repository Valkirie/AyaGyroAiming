using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AyaGyroAiming
{
    class HidHide
    {
        private Process process;

        public HidHide(string _path)
        {
            process = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    FileName = _path
                }
            };
        }

        public void RegisterApplication(string path)
        {
            process.StartInfo.Arguments = $"--app-reg \"{path}\"";
            process.Start();
            process.WaitForExit();

            string jsonString = process.StandardOutput.ReadToEnd();
        }

        public List<Device> GetDevices()
        {
            process.StartInfo.Arguments = $"--dev-gaming";
            process.Start();
            process.WaitForExit();

            string jsonString = process.StandardOutput.ReadToEnd();

            if (jsonString == "")
                return new List<Device>();

            jsonString = jsonString.Replace("\"friendlyName\" : ", "\"friendlyName\" : \"");
            jsonString = jsonString.Replace("[ {", "{");
            jsonString = jsonString.Replace(" } ] } ] ", " } ] }");
            jsonString = jsonString.Replace(@"\", @"\\");
            RootDevice root = JsonSerializer.Deserialize<RootDevice>(jsonString);

            return root.devices;
        }

        public void SetCloaking(bool status)
        {
            process.StartInfo.Arguments = status ? $"--cloak-on" : $"--cloak-off";
            process.Start();
            process.WaitForExit();

            string jsonString = process.StandardOutput.ReadToEnd();
        }

        public void HideDevice(string deviceInstancePath)
        {
            process.StartInfo.Arguments = $"--dev-hide \"{deviceInstancePath}\"";
            process.Start();
            process.WaitForExit();

            string jsonString = process.StandardOutput.ReadToEnd();
        }

        public void UnHideDevice(string deviceInstancePath)
        {
            process.StartInfo.Arguments = $"--dev-unhide \"{deviceInstancePath}\"";
            process.Start();
            process.WaitForExit();

            string jsonString = process.StandardOutput.ReadToEnd();
        }
    }
}
