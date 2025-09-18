using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Library
{
    [Serializable]
    public class Task
    {
        public required TaskType Type { get; set; }
        public int coordinateX { get; set; } = 0;
        public int coordinateY { get; set; } = 0;
        public TaskStatus Status { get; set; } = TaskStatus.INPROGRESS;
    }
}
