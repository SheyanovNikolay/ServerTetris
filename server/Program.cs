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
        static int Interval;
        static System.Timers.Timer timer1 = new System.Timers.Timer();

        static void Main(string[] args)
        {

            TcpListener serverSocket = null;
            try
            {
                //адрес сервера
                IPAddress localAddress = IPAddress.Parse("127.0.0.1");
                //на один процессор 4 потока
                int playerThreadsCount = 2;
                //макс кол-во потоков
                ThreadPool.SetMaxThreads(playerThreadsCount, playerThreadsCount);
                //мин кол-во потоков
                ThreadPool.SetMinThreads(playerThreadsCount -1, playerThreadsCount -1);

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
            catch (Exception e) {
                Console.WriteLine(e.Message);
            }
            finally {
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
            bool firstPlayer = false;
            lock (clientLock)
            {
                Clients.Add(clientStream);
                numberOfClient++;
                if (numberOfClient == 1) firstPlayer = true;
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
                    Console.WriteLine(name + ": " + (message == "1" ? "Подключился":"Готов играть"));
                    switch (Convert.ToInt32(message))
                    {
                        case 1://получаем команду готовности от клиента
                            lock (targetLock)
                            {
                                if (res != 0) { res = 0; }// result to null
                                ready++;
                            }
                            while (ready != numberOfClient) { Thread.Sleep(20); }
                            writeBuffer = Encoding.Unicode.GetBytes("go");//отправляем клиенту, что все подключились
                            clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                            break;
                        case 2: //готовность играть
                            lock (targetLock)
                            {
                                if (ready != 0) { ready = 0; }
                                play++;
                            }
                            while (play != numberOfClient) { Thread.Sleep(20); }

                            Thread keyPressListener = new Thread(delegate() { ClientPressKeyHandler(client); });
                            keyPressListener.Start();

                            if (firstPlayer == true)
                            {
                                Thread gameLoopThread = new Thread(Init1);
                                gameLoopThread.Start();
                            }
                            else
                            {
                                Thread gameLoopThread = new Thread(Init2);
                                gameLoopThread.Start();
                            }
                            //Init();
                            while (true)
                            {
                                lock (mapLock)
                                { 
                                writeBuffer = Encoding.Unicode.GetBytes(ToString(map));
                                clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                                //RandomMatrix(map);
                                Thread.Sleep(300);//300
                                }
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
        static void ClientPressKeyHandler(TcpClient client)
        {
            NetworkStream clientPressKeyStream = client.GetStream();
            while (true)
            {
                string inputString;
                byte[] readBuffer = new byte[256];
                int inputBuffer = clientPressKeyStream.Read(readBuffer, 0, readBuffer.Length);
                inputString = Encoding.Unicode.GetString(readBuffer, 0, inputBuffer);
                lock (mapLock)
                {
                    switch (inputString)
                    {
                        case "Up":
                            Console.Write("Up ");
                            if (!IsIntersects())
                            {
                                ResetArea();
                                currentShape1.RotateShape();
                                Merge();
                            }
                            break;
                        case "Down":
                            Console.Write("Down ");
                            break;
                        case "Right":
                            Console.Write("Right ");
                            if (!CollideHor(1))
                            {
                                ResetArea();
                                currentShape1.MoveRight();
                                Merge();
                            }
                            break;
                        case "Left":
                            Console.Write("Left ");
                            if (!CollideHor(-1))
                            {
                                ResetArea();
                                currentShape1.MoveLeft();
                                Merge();
                            }
                            break;
                    }
                }
            }
        }
        
        public static void Init1()
        {
            size = 25;//размер квадратика в пикселях
            score = 0;
            Interval = 500;
            currentShape1 = new Shape(2, 0);// должна приходить с сервака

            timer1.Interval = Interval;
            timer1.Elapsed += new ElapsedEventHandler(update1);
            timer1.Start();
        }

        private static void update1(object sender, ElapsedEventArgs e)
        {
            lock (mapLock)
            {
                ResetArea();
                if (!Collide())
                {
                    currentShape1.MoveDown();
                }
                else
                {
                    Merge();
                    SliceMap();
                    timer1.Interval = Interval;
                    currentShape1.ResetShape(2, 0);
                    if (Collide())
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
                Merge();
            }
       }

        public static void Init2()
        {
            size = 25;//размер квадратика в пикселях
            score = 0;
            Interval = 500;
            currentShape2 = new Shape(8, 0);// должна приходить с сервака

            timer1.Interval = Interval;
            timer1.Elapsed += new ElapsedEventHandler(update2);
            timer1.Start();
        }

        private static void update2(object sender, ElapsedEventArgs e)
        {
            lock (mapLock)
            {
                ResetArea();
                if (!Collide())
                {
                    currentShape2.MoveDown();
                }
                else
                {
                    Merge();
                    SliceMap();
                    timer1.Interval = Interval;
                    currentShape2.ResetShape(8, 0);
                    if (Collide())
                    {
                        for (int i = 0; i < 16; i++)
                        {
                            for (int j = 0; j < mapWidth; j++)
                            {
                                map[i, j] = 0;
                            }
                        }
                        timer1.Elapsed -= new ElapsedEventHandler(update2);
                        timer1.Stop();
                        Init1();
                    }
                }
                Merge();
            }
        }

        public static void Merge()
        {
            for (int i = currentShape1.y; i < currentShape1.y + currentShape1.sizeMatrix; i++)
            {
                for (int j = currentShape1.x; j < currentShape1.x + currentShape1.sizeMatrix; j++)
                {
                    if (currentShape1.matrix[i - currentShape1.y, j - currentShape1.x] != 0)
                        map[i, j] = currentShape1.matrix[i - currentShape1.y, j - currentShape1.x];
                }
            }
        }

        public static void SliceMap()
        {
            int count = 0;
            int curRemovedLines = 0;
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
                    curRemovedLines++;
                    for (int k = i; k >= 1; k--)
                    {
                        for (int o = 0; o < mapWidth; o++)
                        {
                            map[k, o] = map[k - 1, o];
                        }
                    }
                }
            }
            for (int i = 0; i < curRemovedLines; i++)
            {
                score += 10 * (i + 1);
            }
            linesRemoved += curRemovedLines;

            if (linesRemoved % 5 == 0)
            {
                if (Interval > 60)
                    Interval -= 10;
            }
        }

        public static bool Collide()
        {
            for (int i = currentShape1.y + currentShape1.sizeMatrix - 1; i >= currentShape1.y; i--)
            {
                for (int j = currentShape1.x; j < currentShape1.x + currentShape1.sizeMatrix; j++)
                {
                    if (currentShape1.matrix[i - currentShape1.y, j - currentShape1.x] != 0)
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

        public static bool CollideHor(int dir)
        {
            for (int i = currentShape1.y; i < currentShape1.y + currentShape1.sizeMatrix; i++)
            {
                for (int j = currentShape1.x; j < currentShape1.x + currentShape1.sizeMatrix; j++)
                {
                    if (currentShape1.matrix[i - currentShape1.y, j - currentShape1.x] != 0)
                    {
                        if (j + 1 * dir > mapWidth-1 || j + 1 * dir < 0)
                            return true;

                        if (map[i, j + 1 * dir] != 0)
                        {
                            if (j - currentShape1.x + 1 * dir >= currentShape1.sizeMatrix || j - currentShape1.x + 1 * dir < 0)
                            {
                                return true;
                            }
                            if (currentShape1.matrix[i - currentShape1.y, j - currentShape1.x + 1 * dir] == 0)
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        public static void ResetArea()
        {
            for (int i = currentShape1.y; i < currentShape1.y + currentShape1.sizeMatrix; i++)
            {
                for (int j = currentShape1.x; j < currentShape1.x + currentShape1.sizeMatrix; j++)
                {
                    if (i >= 0 && j >= 0 && i < 16 && j < mapWidth)
                    {
                        if (currentShape1.matrix[i - currentShape1.y, j - currentShape1.x] != 0)
                        {
                            map[i, j] = 0;
                        }
                    }
                }
            }
        }

        public static bool IsIntersects()
        {
            for (int i = currentShape1.y; i < currentShape1.y + currentShape1.sizeMatrix; i++)
            {
                for (int j = currentShape1.x; j < currentShape1.x + currentShape1.sizeMatrix; j++)
                {
                    if (j >= 0 && j <= mapWidth-1)
                    {
                        if (map[i, j] != 0 && currentShape1.matrix[i - currentShape1.y, j - currentShape1.x] == 0)
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
                    resultString = String.Concat(resultString, array[y,x].ToString() + " ");
                }
                resultString = String.Concat(resultString, "/");
            }

            return resultString;
        }

    }
}
