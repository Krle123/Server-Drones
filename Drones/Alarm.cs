using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Library
{
    [Serializable]
    public class Alarm
    {
        public required AlarmType Type { get; set; }
        public int CoordinateX { get; set; } = 0;
        public int CoordinateY { get; set; } = 0;
        public int Priority { get; set; } = 0;
    }
}
