using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AyaGyroAiming
{
    public class IniFile   // revision 11
    {
        string Path;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern long WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        public IniFile(string IniPath)
        {
            Path = new FileInfo(IniPath).FullName;
        }

        public string ReadString(string Key, string Section)
        {
            var RetVal = new StringBuilder(255);
            GetPrivateProfileString(Section, Key, "", RetVal, 255, Path);
            return RetVal.ToString();
        }

        public bool ReadBool(string Key, string Section)
        {
            string value = ReadString(Key, Section);
            bool output; bool.TryParse(value, out output);
            return output;
        }

        public float ReadFloat(string Key, string Section)
        {
            string value = ReadString(Key, Section);
            float output; float.TryParse(value, NumberStyles.AllowDecimalPoint, new CultureInfo("en-US"), out output);
            return output;
        }

        public uint ReadUInt(string Key, string Section)
        {
            string value = ReadString(Key, Section);
            uint output; uint.TryParse(value, out output);
            return output;
        }

        public int ReadInt(string Key, string Section)
        {
            string value = ReadString(Key, Section);
            int output; int.TryParse(value, out output);
            return output;
        }

        public void Write(string Key, string Value, string Section)
        {
            WritePrivateProfileString(Section, Key, Value, Path);
        }

        public void DeleteKey(string Key, string Section)
        {
            Write(Key, null, Section);
        }

        public void DeleteSection(string Section)
        {
            Write(null, null, Section);
        }

        public bool KeyExists(string Key, string Section)
        {
            return ReadString(Key, Section).Length > 0;
        }
    }
}
