using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace server
{
    class Program
    {

        static volatile int numberOfClient = 0;
        static volatile int ready = 0;
        static volatile int play = 0;
        static volatile int res = 0;
        static readonly object targetLock = new object();//отвечает за доступ к коду
        static readonly object clientLock = new object();//отвечает за доступ к коду
        static readonly object mapLock = new object();
        static List<NetworkStream> Clients = new List<NetworkStream>();

        // переменные от Тетриса
        static Shape currentShape1;
        static Shape currentShape2;
        static readonly int mapWidth = 12;
        static int[,] map = new int[16, mapWidth];
        static int linesRemoved;
        static int size = 25;
        static int score;
        static int Interval1;
        static int Interval2;
        static System.Timers.Timer timer1 = new System.Timers.Timer();
        static System.Timers.Timer timer2 = new System.Timers.Timer();
        static int playerThreadsCount;
        static bool firstPlayer = false;
        static void Main(string[] args)
        {

            TcpListener serverSocket = null;
            try
            {
                //адрес сервера
                IPAddress localAddress = IPAddress.Parse("127.0.0.1");
                //на один процессор 4 потока
                playerThreadsCount = 1;
                //макс кол-во потоков
                ThreadPool.SetMaxThreads(playerThreadsCount, playerThreadsCount);
                //мин кол-во потоков
                ThreadPool.SetMinThreads(playerThreadsCount, playerThreadsCount);

                serverSocket = new TcpListener(localAddress, 8080);//создаем сокет

                Console.WriteLine("Server Started");
                Console.WriteLine("IP-адрес:" + localAddress.ToString());
                Console.WriteLine("Port:8080");
                Console.WriteLine("Потоки:" + playerThreadsCount.ToString());

                serverSocket.Start();//старт сервера
                while (true)
                {
                    TcpClient clientSocket = serverSocket.AcceptTcpClient();//ждем клиентов
                    ThreadPool.QueueUserWorkItem(ClientThread, clientSocket);//добавляем клиентов в поток
                    Console.WriteLine("Accept new client");
                    Thread.Sleep(50);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                if (serverSocket != null)
                    serverSocket.Stop();
            }

        }

        static void ClientThread(object clientObj)
        {
            TcpClient client = clientObj as TcpClient;
            NetworkStream clientStream = client.GetStream();
            byte[] readBuffer = new byte[512];
            byte[] writeBuffer = new byte[512];
            int buffer;
            int localNumber;
            lock (clientLock)
            {
                Clients.Add(clientStream);
                numberOfClient++;
                localNumber = numberOfClient;
                Console.WriteLine("Количество клиентов: " + numberOfClient);
            }

            buffer = clientStream.Read(readBuffer, 0, readBuffer.Length);//читаем имя клиента
            string name = Encoding.Unicode.GetString(readBuffer, 0, buffer);//записываем имя клиента
            Console.WriteLine("Имя клиента: " + name);
            string message;
            try
            {
                while (true)
                {
                    buffer = clientStream.Read(readBuffer, 0, readBuffer.Length);// чтение конекшена и готовности всех игроков
                    message = Encoding.Unicode.GetString(readBuffer, 0, buffer);
                    Console.WriteLine(name + ": " + (message == "1" ? "Подключился" : "Готов играть"));
                    switch (Convert.ToInt32(message))
                    {
                        case 1://получаем команду готовности от клиента
                            lock (targetLock)
                            {
                                if (res != 0) { res = 0; }// result to null
                                ready++;
                            }
                            while (ready != playerThreadsCount) { Thread.Sleep(20); }
                            writeBuffer = Encoding.Unicode.GetBytes("go");//отправляем клиенту, что все подключились
                            clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                            break;
                        case 2: //готовность играть
                            lock (targetLock)
                            {
                                if (ready != 0) { ready = 0; }
                                play++;
                            }
                            while (play != playerThreadsCount) { Thread.Sleep(20); }

                            Thread keyPressListener1 = new Thread(delegate () { ClientPressKeyHandlerPlayer1(client); });
                            keyPressListener1.Start();
                            Thread keyPressListener2 = new Thread(delegate () { ClientPressKeyHandlerPlayer2(client); });
                            keyPressListener2.Start();

                            Thread gameLoopThread1 = new Thread(Init1);
                            gameLoopThread1.Start();
                            Thread gameLoopThread2 = new Thread(Init2);
                            gameLoopThread2.Start();

                            while (true)
                            {
                                writeBuffer = Encoding.Unicode.GetBytes(ToString(map));
                                clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                                //RandomMatrix(map);
                                Thread.Sleep(300);//300
                            }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                clientStream.Close();
                client.Close();
                lock (clientLock)
                {
                    Clients.Remove(clientStream);
                    numberOfClient--;
                }
                Console.WriteLine("Disconnect, Number of Client:" + numberOfClient.ToString());
            }
        }

        public static int[,] RandomMatrix(int[,] map)
        {
            Random rnd = new Random();
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    map[y, x] = rnd.Next(-1, 8);
                }
            }

            return map;
        }

        //обработчик нажатия клавиш клиентом
        static void ClientPressKeyHandlerPlayer2(TcpClient client)
        {
            NetworkStream clientPressKeyStream = client.GetStream();
            try
            {
                while (true)
                {
                    string inputString;
                    byte[] readBuffer = new byte[256];
                    int inputBuffer = clientPressKeyStream.Read(readBuffer, 0, readBuffer.Length);
                    inputString = Encoding.Unicode.GetString(readBuffer, 0, inputBuffer);

                    switch (inputString)
                    {
                        case "Up":
                            Console.Write("Up ");
                            if (!IsIntersects(currentShape2))
                            {
                                ResetArea(currentShape2);
                                currentShape2.RotateShape();
                                Merge(currentShape2);
                            }
                            break;
                        case "Down":
                            Console.Write("Down ");
                            break;
                        case "Right":
                            Console.Write("Right ");
                            if (!CollideHor(1, currentShape2, "currentShape2"))
                            {
                                ResetArea(currentShape2);
                                currentShape2.MoveRight();
                                Merge(currentShape2);
                            }
                            break;
                        case "Left":
                            Console.Write("Left ");
                            if (!CollideHor(-1, currentShape2, "currentShape2"))
                            {
                                ResetArea(currentShape2);
                                currentShape2.MoveLeft();
                                Merge(currentShape2);
                            }
                            break;
                    }
                }
            }
            catch (Exception e)
            { clientPressKeyStream.Close(); }
        }

        static void ClientPressKeyHandlerPlayer1(TcpClient client)
        {

            NetworkStream clientPressKeyStream = client.GetStream();
            try
            {
                while (true)
                {
                    string inputString;
                    byte[] readBuffer = new byte[256];
                    int inputBuffer = clientPressKeyStream.Read(readBuffer, 0, readBuffer.Length);
                    inputString = Encoding.Unicode.GetString(readBuffer, 0, inputBuffer);

                    switch (inputString)
                    {
                        case "WUp":
                            Console.Write("WUp ");
                            if (!IsIntersects(currentShape1))
                            {
                                ResetArea(currentShape1);
                                currentShape1.RotateShape();
                                Merge(currentShape1);
                            }
                            break;
                        case "SDown":
                            Console.Write("SDown ");
                            break;
                        case "DRight":
                            Console.Write("DRight ");
                            if (!CollideHor(1, currentShape1, "currentShape1"))
                            {
                                ResetArea(currentShape1);
                                currentShape1.MoveRight();
                                Merge(currentShape1);
                            }
                            break;
                        case "ALeft":
                            Console.Write("ALeft ");
                            if (!CollideHor(-1, currentShape1, "currentShape1"))
                            {
                                ResetArea(currentShape1);
                                currentShape1.MoveLeft();
                                Merge(currentShape1);
                            }
                            break;
                    }
                }
            }
            catch (Exception e)
            { clientPressKeyStream.Close(); }
        }

        public static void Init1()
        {
            size = 25;//размер квадратика в пикселях
            score = 0;
            Interval1 = 300;
            currentShape1 = new Shape(2, 0);// должна приходить с сервака

            timer1.Interval = Interval1;
            timer1.Elapsed += new ElapsedEventHandler(update1);
            timer1.Start();
        }

        private static void update1(object sender, ElapsedEventArgs e)
        {
            ResetArea(currentShape1);
            if (!Collide(currentShape1))
            {
                currentShape1.MoveDown();
            }
            else
            {
                Merge(currentShape1);
                //while (!Collide(currentShape2)) Thread.Sleep(30);
                SliceMap();
                timer1.Interval = Interval1;
                currentShape1.ResetShape(2, 0);
                if (Collide(currentShape1))
                {
                    for (int i = 0; i < 16; i++)
                    {
                        for (int j = 0; j < mapWidth; j++)
                        {
                            map[i, j] = 0;
                        }
                    }
                    timer1.Elapsed -= new ElapsedEventHandler(update1);
                    timer1.Stop();
                    Init1();
                }
            }

            Merge(currentShape1);
        }

        public static void Init2()
        {
            size = 25;//размер квадратика в пикселях
            Interval2 = 300;
            currentShape2 = new Shape(8, 0);// должна приходить с сервака

            timer2.Interval = Interval2;
            timer2.Elapsed += new ElapsedEventHandler(update2);
            timer2.Start();
        }

        private static void update2(object sender, ElapsedEventArgs e)
        {
            ResetArea(currentShape2);
            if (!Collide(currentShape2))
            {
                currentShape2.MoveDown();
            }
            else
            {
                Merge(currentShape2);
                //while (!Collide(currentShape1)) Thread.Sleep(30);

                SliceMap();
                timer2.Interval = Interval2;
                currentShape2.ResetShape(8, 0);
                if (Collide(currentShape2))
                {
                    for (int i = 0; i < 16; i++)
                    {
                        for (int j = 0; j < mapWidth; j++)
                        {
                            map[i, j] = 0;
                        }
                    }
                    timer2.Elapsed -= new ElapsedEventHandler(update2);
                    timer2.Stop();
                    Init2();
                }
            }
            Merge(currentShape2);
        }

        public static bool Merge(Shape currentShape)
        {
            for (int i = currentShape.y; i < currentShape.y + currentShape.sizeMatrix; i++)
            {
                for (int j = currentShape.x; j < currentShape.x + currentShape.sizeMatrix; j++)
                {
                    if (currentShape.matrix[i - currentShape.y, j - currentShape.x] != 0)
                        map[i, j] = currentShape.matrix[i - currentShape.y, j - currentShape.x];
                }
            }
            return true;
        }

        public static void SliceMap()
        {
            int count = 0;
            for (int i = 0; i < 16; i++)
            {
                count = 0;
                for (int j = 0; j < mapWidth; j++)
                {
                    if (map[i, j] != 0)
                        count++;
                }
                if (count == mapWidth)
                {
                    for (int k = i; k >= 1; k--)
                    {
                        for (int o = 0; o < mapWidth; o++)
                        {
                            map[k, o] = map[k - 1, o];
                        }
                    }
                }
            }
        }

        public static bool Collide(Shape currentShape)
        {
            for (int i = currentShape.y + currentShape.sizeMatrix - 1; i >= currentShape.y; i--)
            {
                for (int j = currentShape.x; j < currentShape.x + currentShape.sizeMatrix; j++)
                {
                    if (currentShape.matrix[i - currentShape.y, j - currentShape.x] != 0)
                    {
                        if (i + 1 == 16)
                            return true;
                        if (map[i + 1, j] != 0)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static bool CollideHor(int dir, Shape currentShape, string currentShapeString)
        {
            int leftBorder;
            int rightBorder;
            if (currentShapeString == "currentShape1")
            {
                leftBorder = 0;
                rightBorder = mapWidth / 2 - 1;
            }
            else
            {
                leftBorder = mapWidth / 2; ;
                rightBorder = mapWidth - 1;
            }
            for (int i = currentShape.y; i < currentShape.y + currentShape.sizeMatrix; i++)
            {
                for (int j = currentShape.x; j < currentShape.x + currentShape.sizeMatrix; j++)
                {
                    if (currentShape.matrix[i - currentShape.y, j - currentShape.x] != 0)
                    {
                        if (j + 1 * dir > rightBorder || j + 1 * dir < leftBorder)
                            return true;

                        if (map[i, j + 1 * dir] != 0)
                        {
                            if (j - currentShape.x + 1 * dir >= currentShape.sizeMatrix || j - currentShape.x + 1 * dir < 0)
                            {
                                return true;
                            }
                            if (currentShape.matrix[i - currentShape.y, j - currentShape.x + 1 * dir] == 0)
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        public static void ResetArea(Shape currentShape)
        {
            for (int i = currentShape.y; i < currentShape.y + currentShape.sizeMatrix; i++)
            {
                for (int j = currentShape.x; j < currentShape.x + currentShape.sizeMatrix; j++)
                {
                    if (i >= 0 && j >= 0 && i < 16 && j < mapWidth)
                    {
                        if (currentShape.matrix[i - currentShape.y, j - currentShape.x] != 0)
                        {
                            map[i, j] = 0;
                        }
                    }
                }
            }
        }

        public static bool IsIntersects(Shape currentShape)
        {
            for (int i = currentShape.y; i < currentShape.y + currentShape.sizeMatrix; i++)
            {
                for (int j = currentShape.x; j < currentShape.x + currentShape.sizeMatrix; j++)
                {
                    if (j >= 0 && j <= mapWidth - 1)
                    {
                        if (map[i, j] != 0 && currentShape.matrix[i - currentShape.y, j - currentShape.x] == 0)
                            return true;
                    }
                }
            }
            return false;
        }

        public static string ToString(int[,] array)
        {
            string resultString = "";
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    resultString = String.Concat(resultString, array[y, x].ToString() + " ");
                }
                resultString = String.Concat(resultString, "/");
            }

            return resultString;
        }

    }
}
