namespace AyaGyroAiming
{
    public class Settings
    {
        public bool EnableGyroAiming { get; set; }
        public float GyroStickAggressivity { get; set; }
        public float GyroStickRange { get; set; }
        public bool GyroStickInvertAxisX { get; set; }
        public bool GyroStickInvertAxisY { get; set; }
        public bool GyroStickInvertAxisZ { get; set; }
        public string TriggerString { get; set; }
        public uint GyroPullRate { get; set; }
        public uint GyroMaxSample { get; set; }
    }
}
