namespace AyaGyroAiming
{
    public class Settings
    {
        public bool GyroAiming { get; set; }
        public float Aggressivity { get; set; }

        public float RangeAxisX { get; set; }
        public float RangeAxisY { get; set; }
        public float RangeAxisZ { get; set; }

        public bool InvertAxisX { get; set; }
        public bool InvertAxisY { get; set; }
        public bool InvertAxisZ { get; set; }

        public string Trigger { get; set; }
        public uint PullRate { get; set; }
        public uint MaxSample { get; set; }
    }
}
