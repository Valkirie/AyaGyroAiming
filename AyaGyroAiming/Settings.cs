using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace AyaGyroAiming
{
    public class Settings
    {
        public bool GyroAiming { get; set; }
        public float Aggressivity { get; set; }
        public float Range { get; set; }
        public bool InvertAxisX { get; set; }
        public bool InvertAxisY { get; set; }
        public bool InvertAxisZ { get; set; }
        public string Trigger { get; set; }
        public uint PullRate { get; set; }
        public uint MaxSample { get; set; }
        public bool MonitorRatio { get; set; }
    }
}
