namespace Drones
{
    [Serializable]
    public class Drone
    {
        public int id { get; set; } = 0;
        public required DroneType type { get; set; }
        public DroneStatus status { get; set; } = DroneStatus.FREE;
        public int coordinateX { get; set; } = 0;
        public int coordinateY { get; set; } = 0;

        public override string ToString()
        {
            string s = $"Drone {id}";
            if (type == DroneType.SUPERVISORY)
                s += $" - Supervisory";
            else
                s += $" - Executive";
            if (status == DroneStatus.FREE)
                s += $" - Free";
            else if (status == DroneStatus.BUSY)
                s += $" - Busy";
            else
                s += $" - Broken";
            s += $"\tCoordinates: {coordinateX} {coordinateY}";
            return s;
        }
    }
}
