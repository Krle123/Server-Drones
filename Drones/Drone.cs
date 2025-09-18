namespace Drones
{
    [Serializable]
    public class Drone
    {
        public int id { get; set; } = 0;
        public required DroneType type { get; set; }
        public int coordinateX { get; set; } = 0;
        public int coordinateY { get; set;} = 0;
    }
}
