using Drones;
using Library;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Server
{
    internal class Server
    {
        static List<DroneInfo> drones = new List<DroneInfo>();
        static List<DroneInfo> brokenDrones = new List<DroneInfo>();
        static List<DroneTask> tasks = new List<DroneTask>();
        static List<DroneTask> scoutTasks = new List<DroneTask>();
        static List<DroneTask> ongoingTasks = new List<DroneTask>();
        static List<DroneTask> priorityTasks = new List<DroneTask>();
        static List<Alarm> alarms = new List<Alarm>();
        static Dictionary<DroneTask, DroneInfo> taskAssignments = new Dictionary<DroneTask, DroneInfo>();
        static Field[,] fields = new Field[3, 3];

        static void Main(string[] args)
        {
            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, 50001);
            udpSocket.Bind(serverEP);
            udpSocket.Blocking = false;

            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint tcpEP = new IPEndPoint(IPAddress.Any, 50002);
            tcpSocket.Bind(tcpEP);
            tcpSocket.Blocking = false;
            tcpSocket.Listen();
            List<Socket> tcp = new List<Socket>();

            EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

            InitializeFields();
            StartFieldMonitor();
            StartPrint();

            int alarmNum = 0;

            byte[] buffer = new byte[8192];

            try
            {
                StartClients();

                while (true)
                {
                    List<Socket> checkRead = new List<Socket> { udpSocket, tcpSocket };
                    List<Socket> checkError = new List<Socket> { udpSocket, tcpSocket };
                    checkRead.AddRange(tcp);

                    Socket.Select(checkRead, null, checkError, 500);

                    foreach (Socket s in checkRead)
                    {
                        if (s == udpSocket)
                        {
                            int bytesRec = s.ReceiveFrom(buffer, ref endPoint);
                            string msgJson = Encoding.UTF8.GetString(buffer, 0, bytesRec);
                            Message message = JsonSerializer.Deserialize<Message>(msgJson);

                            switch (message.msg)
                            {
                                case "Status":
                                    HandleStatusMessage(udpSocket, endPoint, message);
                                    break;

                                case "TaskCompleted":
                                    HandleTaskCompleted(udpSocket, message);
                                    break;
                            }

                            buffer = new byte[8192];
                        }
                        else if (s == tcpSocket)
                        {
                            Socket alarm = tcpSocket.Accept();
                            alarm.Blocking = false;
                            tcp.Add(alarm);
                        }
                        else
                        {
                            int bytesRead = s.Receive(buffer);
                            if (bytesRead == 0)
                            {
                                s.Close();
                                tcp.Remove(s);
                                continue;
                            }
                            else
                            {
                                string msgJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                Message message = JsonSerializer.Deserialize<Message>(msgJson);
                                Alarm alarm = JsonSerializer.Deserialize<Alarm>(message.json);

                                var broken = drones.FirstOrDefault(d =>
                                    d.Drone.coordinateX == alarm.CoordinateX &&
                                    d.Drone.coordinateY == alarm.CoordinateY);
                                if (broken != null)
                                {
                                    broken.TcpSocket = s;
                                }

                                fields[alarm.CoordinateX, alarm.CoordinateY].Type = FieldType.ALARM;
                                alarmNum++;
                                HandleWarning(alarm);
                            }
                        }
                    }

                    if (checkError.Count > 0)
                    {
                        foreach (Socket es in checkError)
                        {
                            Console.WriteLine($"ERROR on socket {es.LocalEndPoint}");
                            es.Close();
                        }
                    }

                    DispatchTasks(udpSocket);
                    if (Console.KeyAvailable)
                    {
                        if (Console.ReadKey().Key == ConsoleKey.Escape)
                        {
                            Console.WriteLine($"\n\nNumber of alarms solved: {alarmNum}");
                            break;
                        }
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine($"There was an error {e}");
            }

            udpSocket.Close();
            tcpSocket.Close();
            Console.WriteLine("Server finished");
            Console.ReadKey();
        }

        static void HandleStatusMessage(Socket udpSocket, EndPoint endPoint, Message message)
        {
            Drone drone = JsonSerializer.Deserialize<Drone>(message.json);
            var existing = drones.FirstOrDefault(d => d.Drone.id == drone.id);

            if (existing == null)
            {
                drones.Add(new DroneInfo { Drone = drone, EndPoint = endPoint });
                Console.WriteLine($"New drone registered: {drone.id} ({drone.type})");
            }
            else
            {
                existing.Drone = drone;
                existing.EndPoint = endPoint;
                Console.WriteLine($"Drone {drone.id} status updated: {drone.status}");
            }

            DispatchTasks(udpSocket);
        }

        static void HandleTaskCompleted(Socket udpSocket, Message message)
        {
            DroneTask completedTask = JsonSerializer.Deserialize<DroneTask>(message.json);
            fields[completedTask.coordinateX, completedTask.coordinateY] = completedTask.field;

            ongoingTasks.RemoveAll(t =>
                t.coordinateX == completedTask.coordinateX &&
                t.coordinateY == completedTask.coordinateY &&
                t.Type == completedTask.Type);

            if (taskAssignments.TryGetValue(completedTask, out var droneInfo))
            {
                droneInfo.Drone.status = DroneStatus.FREE;
                taskAssignments.Remove(completedTask);
            }

            alarms.RemoveAll(a =>
            a.Type == AlarmType.BROKEN &&
            a.CoordinateX == completedTask.coordinateX &&
            a.CoordinateY == completedTask.coordinateY);

            if (completedTask.Type == TaskType.FIX)
            {
                if (brokenDrones.Count > 0)
                {
                    var repairedDrone = brokenDrones[0];
                    brokenDrones.RemoveAt(0);

                    repairedDrone.Drone.status = DroneStatus.FREE;

                    try
                    {
                        Message fixMessage = new Message("Fix", "");
                        string json = JsonSerializer.Serialize(fixMessage);
                        byte[] data = Encoding.UTF8.GetBytes(json);

                        repairedDrone.TcpSocket.Send(data);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"TCP error: {ex.Message}");
                    }
                }
            }
        }

        static void HandleWarning(Alarm alarm)
        {
            alarms.Add(alarm);

            if (alarm.Type == AlarmType.BROKEN)
            {
                priorityTasks.Add(new DroneTask
                {
                    coordinateX = alarm.CoordinateX,
                    coordinateY = alarm.CoordinateY,
                    field = fields[alarm.CoordinateX, alarm.CoordinateY],
                    Type = TaskType.FIX,
                    Status = TaskStatus.INPROGRESS
                });

                var broken = drones.FirstOrDefault(d => d.Drone.coordinateX == alarm.CoordinateX &&
                                                        d.Drone.coordinateY == alarm.CoordinateY);
                if (broken != null)
                {
                    broken.Drone.status = DroneStatus.BROKEN;
                    brokenDrones.Add(broken);
                }
            }
        }

        static void SendTaskToDrone(Socket udpSocket, DroneInfo droneInfo, DroneTask task)
        {
            droneInfo.Drone.status = DroneStatus.BUSY;
            taskAssignments[task] = droneInfo;

            Message response = new Message("Task", JsonSerializer.Serialize(task));
            byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));
            udpSocket.SendTo(data, 0, data.Length, SocketFlags.None, droneInfo.EndPoint);

            ongoingTasks.Add(task);
        }

        static void DispatchTasks(Socket udpSocket)
        {
            foreach (var droneInfo in drones)
            {
                if (droneInfo.Drone.status == DroneStatus.FREE)
                {
                    DroneTask? taskToSend = null;

                    if (droneInfo.Drone.type == DroneType.EXECUTIVE && (tasks.Count > 0 || priorityTasks.Count > 0))
                    {
                        if (priorityTasks.Count > 0)
                        {
                            taskToSend = priorityTasks[0];
                            priorityTasks.RemoveAt(0);
                        }
                        else
                        {
                            taskToSend = tasks[0];
                            tasks.RemoveAt(0);
                        }
                    }
                    else if (droneInfo.Drone.type == DroneType.SUPERVISORY && scoutTasks.Count > 0)
                    {
                        taskToSend = scoutTasks[0];
                        scoutTasks.RemoveAt(0);
                    }

                    if (taskToSend != null)
                    {
                        SendTaskToDrone(udpSocket, droneInfo, taskToSend);
                    }
                }
            }
        }

        static void InitializeFields()
        {
            for (int x = 0; x < fields.GetLength(0); x++)
            {
                for (int y = 0; y < fields.GetLength(1); y++)
                {
                    fields[x, y] = new Field
                    {
                        Type = FieldType.CULTIVATED,
                        growth = 0,
                        humidity = 30,
                        Status = FieldStatus.FREE
                    };
                }
            }
        }

        static void LaunchClient(int id, int type)
        {
            string ClientExe = @"C:\Users\User\source\repos\PRMuIS\Client\bin\Debug\net8.0\Client.exe";
            var psi = new ProcessStartInfo
            {
                FileName = ClientExe,
                Arguments = $"{id} {type}",
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            Process.Start(psi);
        }

        static void StartClients()
        {
            LaunchClient(1, 0); 
            LaunchClient(2, 0);
            LaunchClient(3, 0);
            LaunchClient(4, 0);
            LaunchClient(5, 1);
            LaunchClient(6, 1);
            LaunchClient(7, 1);
        }

        static void StartFieldMonitor()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    CheckFields();
                    Thread.Sleep(1000);
                    if (Console.KeyAvailable)
                    {
                        if (Console.ReadKey().Key == ConsoleKey.Escape)
                        {
                            break;
                        }
                    }
                }
            });
        }

        static void StartPrint()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(5000);
                    PrintServerState();
                    if (Console.KeyAvailable)
                    {
                        if (Console.ReadKey().Key == ConsoleKey.Escape)
                        {
                            break;
                        }
                    }
                }
            });
        }

        static void PrintServerState()
        {
            Console.WriteLine("========== SERVER STATE ==========");

            Console.WriteLine("\n[Drones]");
            if (drones.Count == 0)
                Console.WriteLine("No drones registered.");
            else
                foreach (var droneInfo in drones)
                    Console.WriteLine(droneInfo.Drone);

            Console.WriteLine("\n[Pending Tasks]");
            if (tasks.Count == 0 && scoutTasks.Count == 0 && priorityTasks.Count == 0)
                Console.WriteLine("No pending tasks.");
            else
            {
                foreach (var task in priorityTasks)
                    Console.WriteLine($"PRIORITY: {task}");
                foreach (var task in tasks)
                    Console.WriteLine(task);
                foreach (var task in scoutTasks)
                    Console.WriteLine(task);
            }

            Console.WriteLine("\n[Ongoing Tasks]");
            if (ongoingTasks.Count == 0)
                Console.WriteLine("No ongoing tasks.");
            else
                foreach (var task in ongoingTasks)
                    Console.WriteLine(task);

            // 🔹 Ongoing alarms (broken drones)
            Console.WriteLine("\n[Ongoing Alarms]");
            if (brokenDrones.Count == 0)
                Console.WriteLine("No active alarms.");
            else
            {
                foreach (var alarm in alarms)
                    Console.WriteLine(alarm);

                alarms.RemoveAll(a => a.Type == AlarmType.WEATHER);
            }

            Console.WriteLine("=================================");
        }

        static void CheckFields()
        {
            for (int x = 0; x < fields.GetLength(0); x++)
            {
                for (int y = 0; y < fields.GetLength(1); y++)
                {
                    Field field = fields[x, y];

                    if (field.Type == FieldType.CULTIVATED)
                    {
                        bool exists = scoutTasks.Any(t => t.coordinateX == x && t.coordinateY == y && t.Type == TaskType.SCOUT) ||
                                      ongoingTasks.Any(t => t.coordinateX == x && t.coordinateY == y && t.Type == TaskType.SCOUT);

                        if (!exists)
                        {
                            scoutTasks.Add(new DroneTask
                            {
                                coordinateX = x,
                                coordinateY = y,
                                field = field,
                                Status = TaskStatus.INPROGRESS,
                                Type = TaskType.SCOUT
                            });
                        }
                    }
                    else
                    {
                        var tasksCopy = tasks.ToList();
                        var ongoingCopy = ongoingTasks.ToList();

                        bool exists = tasksCopy.Any(t => t.coordinateX == x && t.coordinateY == y) ||
                                      ongoingCopy.Any(t => t.coordinateX == x && t.coordinateY == y);

                        if (exists) continue;

                        if (field.growth == 0)
                        {
                            tasks.Add(new DroneTask { coordinateX = x, coordinateY = y, field = field, Status = TaskStatus.INPROGRESS, Type = TaskType.SOWING });
                        }
                        else if (field.growth >= 80)
                        {
                            tasks.Add(new DroneTask { coordinateX = x, coordinateY = y, field = field, Status = TaskStatus.INPROGRESS, Type = TaskType.HARVEST });
                        }
                        else if (field.humidity <= 0)
                        {
                            tasks.Add(new DroneTask { coordinateX = x, coordinateY = y, field = field, Status = TaskStatus.INPROGRESS, Type = TaskType.IRRIGATION });
                        }
                    }
                }
            }
        }
    }


}