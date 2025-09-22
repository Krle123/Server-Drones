using Drones;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Library
{
    public class DroneInfo
    {
            public Drone Drone { get; set; }
            public Socket TcpSocket { get; set; }
            public EndPoint EndPoint { get; set; }
    }
}
