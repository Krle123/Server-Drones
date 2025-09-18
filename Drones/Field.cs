using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Library
{
    [Serializable]
    public class Field
    {
        public FieldType Type { get; set; } = FieldType.UNCULTIVATED;
        public FieldStatus Status { get; set; } = FieldStatus.FREE;
    }
}
