namespace Library
{
    [Serializable]
    public class DroneTask
    {
        public required TaskType Type { get; set; }
        public int coordinateX { get; set; } = 0;
        public int coordinateY { get; set; } = 0;
        public Field field { get; set; } = new Field();
        public TaskStatus Status { get; set; } = TaskStatus.INPROGRESS;

        public override string ToString()
        {
            string s = string.Empty;
            switch (Type)
            {
                case TaskType.SOWING:
                    s += "Task - Sowing";
                    break;
                case TaskType.IRRIGATION:
                    s += "Task - Irrigation";
                    break;
                case TaskType.HARVEST:
                    s += "Task - Harvest";
                    break;
                case TaskType.SCOUT:
                    s += "Task - Scout";
                    break;
                case TaskType.FIX:
                    s += "Task - Fix";
                    break;
            }
            s += $"\tCoordinates: {coordinateX} {coordinateY}";
            return s;
        }
    }
}
