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
        private string path;
        public HidHide(string _path)
        {
            path = _path;
        }

        public void RegisterSelf()
        {
            Process process = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    FileName = path,
                    Arguments = $"--app-reg {Assembly.GetExecutingAssembly().Location}"
                }
            };
            process.Start();
            process.WaitForExit();
        }

        public List<Device> GetDevices()
        {
            Process process = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    FileName = path,
                    Arguments = $"--dev-gaming"
                }
            };
            process.Start();
            process.WaitForExit();

            string jsonString = process.StandardOutput.ReadToEnd();
            jsonString = jsonString.Replace("\"friendlyName\" : ", "\"friendlyName\" : \"");
            jsonString = jsonString.Replace("[ {", "{");
            jsonString = jsonString.Replace(" } ] } ] ", " } ] }");
            jsonString = jsonString.Replace(@"\", @"\\");
            RootDevice root = JsonSerializer.Deserialize<RootDevice>(jsonString);

            return root.devices;
        }

        public void SetCloaking(bool status)
        {
            Process process = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    FileName = path,
                    Arguments = status ? $"--cloak-on" : $"--cloak-off"
                }
            };
            process.Start();
            process.WaitForExit();
        }

        public void HideDevice(string deviceInstancePath)
        {
            Process process = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    FileName = path,
                    Arguments = $"--dev-hide \"{deviceInstancePath}\""
                }
            };
            process.Start();
            process.WaitForExit();
        }

        public void UnHideDevice(string deviceInstancePath)
        {
            Process process = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    FileName = path,
                    Arguments = $"--dev-unhide \"{deviceInstancePath}\""
                }
            };
            process.Start();
            process.WaitForExit();
        }
    }
}
