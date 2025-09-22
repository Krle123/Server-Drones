namespace Library
{
    [Serializable]
    public class Alarm
    {
        public required AlarmType Type { get; set; }
        public int CoordinateX { get; set; } = 0;
        public int CoordinateY { get; set; } = 0;
        public int Priority { get; set; } = 0;

        public override string ToString()
        {
            string s = string.Empty;
            if (Type == AlarmType.WEATHER)
                s += "Alarm - Weather";
            else
                s += "Alarm - Broken drone";
            s += $"\tCoordinates: {CoordinateX} {CoordinateY}";
            return s;
        }
    }
}
