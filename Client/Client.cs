using Drones;
using Library;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Client
{
    internal class Client
    {
        static void Main(string[] args)
        {
            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, 50001);
            IPEndPoint tcpEP = new IPEndPoint(IPAddress.Loopback, 50002);
            EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

            int id = 8;
            DroneType type = DroneType.EXECUTIVE;

            if (args.Length >= 1)
                int.TryParse(args[0], out id);

            if (args.Length >= 2 && Enum.TryParse(args[1], true, out DroneType parsedType))
                type = parsedType;

            Drone drone = new Drone { id = id, type = type, status = DroneStatus.FREE };
            DroneTask currentTask = null;

            List<DroneTask> completedTasks = new List<DroneTask>();

            byte[] buffer = new byte[8192];

            try
            {
                udpSocket.Connect(serverEP);
                tcpSocket.Connect(tcpEP);
                Console.WriteLine($"Drone {id} ({type}) successfully connected");
            }
            catch
            {
                Console.WriteLine("Error connecting to server");
                return;
            }

            try
            {
                while (true)
                {
                    SendStatus(udpSocket, serverEP, drone);

                    if (drone.status == DroneStatus.FREE)
                    {
                        int bytesRec = udpSocket.ReceiveFrom(buffer, ref endPoint);
                        if (bytesRec != 0)
                        {
                            string msgJson = Encoding.UTF8.GetString(buffer, 0, bytesRec);
                            Message message = JsonSerializer.Deserialize<Message>(msgJson);

                            if (message.msg == "Task")
                            {
                                DroneTask task = JsonSerializer.Deserialize<DroneTask>(message.json);
                                currentTask = task;
                                drone.status = DroneStatus.BUSY;
                                Console.WriteLine($"Drone {drone.id} starting task {currentTask.Type}");
                                string msg = PerformTask(drone, currentTask);

                                if (msg == "DroneError" || msg == "Weather")
                                {
                                    Console.WriteLine($"Alarm occurred: {msg}");
                                    SendWarning(currentTask, msg, tcpSocket, udpSocket, serverEP);
                                }
                                else
                                {
                                    SendTask(udpSocket, serverEP, currentTask);
                                }

                                drone.status = DroneStatus.FREE;
                                if(currentTask != null)
                                    completedTasks.Add(currentTask);
                                currentTask = null;
                            }
                        }
                    }

                    Thread.Sleep(2000);
                }
                
            }
            catch (SocketException)
            {
                Console.WriteLine("Socket error");
            }
            Console.WriteLine("\n[Completed Tasks]");
            foreach (var task in completedTasks)
                Console.WriteLine(task);
            Console.ReadKey();
        }

        static void SendStatus(Socket udpSocket, IPEndPoint serverEP, Drone drone)
        {
            Message message = new Message("Status", JsonSerializer.Serialize(drone));
            byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            udpSocket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, serverEP);
            Console.WriteLine($"Drone {drone.id} sent status: {drone.status}");
        }

        static void SendTask(Socket udpSocket, IPEndPoint serverEP, DroneTask task)
        {
            Message message = new Message("TaskCompleted", JsonSerializer.Serialize(task));
            byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            udpSocket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, serverEP);
            Console.WriteLine($"Task {task.Type} sent back");
        }

        static void SendWarning(DroneTask task, string warning, Socket tcpSocket, Socket udpSocket, IPEndPoint serverEP)
        {
            try
            {
                Alarm alarm;
                if(warning == "Weather")
                {
                    alarm = new Alarm
                    {
                        CoordinateX = task.coordinateX,
                        CoordinateY = task.coordinateY,
                        Type = AlarmType.WEATHER,
                        Priority = 1
                    };
                }
                else
                {
                    alarm = new Alarm
                    {
                        CoordinateX = task.coordinateX,
                        CoordinateY = task.coordinateY,
                        Type = AlarmType.BROKEN,
                        Priority = 2
                    };
                }
                Message message = new Message("Warning", JsonSerializer.Serialize(alarm));
                byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
                tcpSocket.Send(buffer);
                buffer = new byte[1024];
                if (warning == "DroneError")
                {
                    while (true)
                    {
                        int bytesRec = tcpSocket.Receive(buffer);

                        if (bytesRec == 0)
                        {
                            Console.WriteLine("Server closed TCP connection.");
                            break;
                        }

                        string msgJson = Encoding.UTF8.GetString(buffer, 0, bytesRec);
                        Message messageRcv = JsonSerializer.Deserialize<Message>(msgJson);

                        if (messageRcv.msg == "Fix")
                        {
                            Console.WriteLine("Drone fixed, resuming task.");
                            break;
                        }
                    }

                }
                SendTask(udpSocket, serverEP, task);
            }
            catch
            {
                Console.WriteLine("Failed to send alarm");
            }
        }

        static string PerformTask(Drone drone, DroneTask task)
        {
            drone.coordinateX = task.coordinateX;
            drone.coordinateY = task.coordinateY;

            string msg = "";

            switch (task.Type)
            {
                case TaskType.SCOUT:
                    msg = TaskScout(task.field);
                    break;
                case TaskType.SOWING:
                    msg = TaskSowing(task.field);
                    break;
                case TaskType.IRRIGATION:
                    msg = TaskIrrigation(task.field);
                    break;
                case TaskType.HARVEST:
                    msg = TaskHarvest(task.field);
                    break;
                case TaskType.FIX:
                    msg = TaskFix(task.field);
                    break;
            }

            return msg;
        }

        static string TaskFix(Field field)
        {
            Console.WriteLine("Fixing broken drone......");
            Thread.Sleep(2000);
            Console.WriteLine("Drone fixed!");
            return "";
        }
        static string TaskScout(Field field)
        {
            Random r = new Random();
            Console.WriteLine("Scouting the field...");
            Thread.Sleep(3000);
            if (field.growth == 0)
            {
                Thread.Sleep(2000);
                Console.WriteLine("Field ready for sowing");
                field.Type = FieldType.UNCULTIVATED;
                return "Sowing";
            }

            field.growth += 20;
            field.humidity -= 15;
            if (field.growth >= 80)
            {
                Thread.Sleep(2000);
                Console.WriteLine("Growth complete - Harvest recommended");
                field.Type = FieldType.UNCULTIVATED;
                return "Harvest";
            }

            if (field.humidity <= 0)
            {
                Thread.Sleep(2000);
                Console.WriteLine("Humidity too low - Irrigation required");
                field.Type = FieldType.UNCULTIVATED;
                return "Irrigation";
            }

            int alarm = r.Next(0, 10);
            if (alarm == 2)
            {
                Console.WriteLine("WARNING - Bad weather incoming");
                field.humidity += 30;
                return "Weather";
            }

            return "";
        }

        static string TaskSowing(Field field)
        {
            Random r = new Random();
            Console.WriteLine("Starting sowing...");
            Thread.Sleep(4000);
            field.growth = 20;
            field.humidity = 30;
            
            if (r.Next(0, 30) == 0)
            {
                Console.WriteLine("Drone error during sowing!");
                return "DroneError";
            }

            field.Type = FieldType.CULTIVATED;
            return "";
        }

        static string TaskIrrigation(Field field)
        {
            Random r = new Random();
            Console.WriteLine("Starting irrigation...");
            Thread.Sleep(4000);
            field.humidity = 30;
            
            if (r.Next(0, 30) == 0)
            {
                Console.WriteLine("Drone error during irrigation!");
                return "DroneError";
            }

            field.Type = FieldType.CULTIVATED;
            return "";
        }

        static string TaskHarvest(Field field)
        {
            Random r = new Random();
            Console.WriteLine("Starting harvest...");
            Thread.Sleep(4000);
            field.growth = 0;
            field.humidity = 30;
            
            if (r.Next(0, 30) == 0)
            {
                Console.WriteLine("Drone error during harvest!");
                return "DroneError";
            }

            return "";
        }
    }
}
