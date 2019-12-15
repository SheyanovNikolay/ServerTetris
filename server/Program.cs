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
        static volatile int otvet = 0;
        static volatile int play = 0;
        static volatile int hit = 0;
        static volatile int hitget = 0;
        static volatile int res = 0;
        static volatile int x = 0, y = 0;
        static volatile int x1 = 0, y1 = 0;
        static readonly object targetLock = new object();//отвечает за доступ к коду
        static readonly object clientLock = new object();//отвечает за доступ к коду
        static List<NetworkStream> Clients = new List<NetworkStream>();

        // переменные от Тетриса
        static Shape currentShape;
        static int size;
        static int[,] map = new int[16, 8];
        static int linesRemoved;
        static int score;
        static int Interval;
        static System.Timers.Timer timer1 = new System.Timers.Timer();

        static int[,] bufferMatrixMap = new int[16, 8];

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
                ThreadPool.SetMinThreads(playerThreadsCount -1 , playerThreadsCount -1 );

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
                    Console.WriteLine("New client");
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
            NetworkStream clientStreamWrite = client.GetStream();
            byte[] readBuffer = new byte[1024];
            byte[] writeBuffer = new byte[1024];
            double time = 0;
            int buffer;
            lock (clientLock)
            {
                Clients.Add(clientStream);
                numberOfClient++;
            }
            buffer = clientStream.Read(readBuffer, 0, readBuffer.Length);//читаем имя клиента
            string name = Encoding.Unicode.GetString(readBuffer, 0, buffer);//записываем имя клиента
            Console.WriteLine("Имя клиента:" + name);
            string message;
            try
            {
                
                while (true)
                {
                    buffer = clientStream.Read(readBuffer, 0, readBuffer.Length);
                    message = Encoding.Unicode.GetString(readBuffer, 0, buffer);
                    Console.WriteLine(name + ":" + message);
                    switch (Convert.ToInt32(message))
                    {
                        case 1://получаем команду готовности от клиента
                            time = 0;//no time in project
                            lock (targetLock)
                            {
                                if (res != 0) { res = 0; }// result to null
                                ready++;
                            }
                            while (ready != numberOfClient) { Thread.Sleep(20); }
                            writeBuffer = Encoding.Unicode.GetBytes("go");
                            clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                            break;
                        case 2://клиент готов получить первую мишень
                            lock (targetLock)
                            {
                                if (ready != 0) { ready = 0; }
                                play++;
                            }
                            while (play != numberOfClient) { Thread.Sleep(20); }
                            Thread messageHandler = new Thread(delegate() { MessageHandler(clientStreamWrite); });
                            messageHandler.Start();
                            //lock (targetLock)
                            //{
                            //    Init();
                            //}

                            while (true)
                            {
                                Init();

                                writeBuffer = Encoding.Unicode.GetBytes(ToString(bufferMatrixMap));
                                clientStream.Write(writeBuffer, 0, writeBuffer.Length);

                                //buffer = clientStream.Read(readBuffer, 0, readBuffer.Length);
                                //message = Encoding.Unicode.GetString(readBuffer, 0, buffer);

                                //switch (Convert.ToInt32(message))
                                //{
                                //    case 1://key up
                                //        if (!IsIntersects())
                                //        {
                                //            bufferMatrixMap = ResetArea();
                                //            writeBuffer = Encoding.Unicode.GetBytes(ToString(bufferMatrixMap));
                                //            clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                                //            currentShape.RotateShape();
                                //            bufferMatrixMap = Merge();
                                //            writeBuffer = Encoding.Unicode.GetBytes(ToString(bufferMatrixMap));
                                //            clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                                //        }
                                //        break;
                                //    case 2:
                                //        timer1.Interval = 10;
                                //        break;
                                //    case 3://move right
                                //        if (!CollideHor(1))
                                //        {
                                //            bufferMatrixMap = ResetArea();
                                //            writeBuffer = Encoding.Unicode.GetBytes(ToString(bufferMatrixMap));
                                //            clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                                //            currentShape.MoveRight();
                                //            bufferMatrixMap = Merge();
                                //            writeBuffer = Encoding.Unicode.GetBytes(ToString(bufferMatrixMap));
                                //            clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                                //        }
                                //        break;
                                //    case 4://move left
                                //        if (!CollideHor(-1))
                                //        {
                                //            bufferMatrixMap = ResetArea();
                                //            writeBuffer = Encoding.Unicode.GetBytes(ToString(bufferMatrixMap));
                                //            clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                                //            currentShape.MoveLeft();
                                //            bufferMatrixMap = Merge();
                                //            writeBuffer = Encoding.Unicode.GetBytes(ToString(bufferMatrixMap));
                                //            clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                                //        }
                                //        break;

                                //}
                            }
                            //break;
                        //case 3://клиент нажал кнопку
                        //    //lock (targetlock)
                        //    //{
                        //    //    if (play != 0) { play = 0; }
                        //    //    if (hitget != 0) { hitget = 0; }
                        //    //    x1 = y1 = 0;
                        //    //    hit++;
                        //    //}

                        //    while (hit != numberOfClient) { Thread.Sleep(20); }
                        //    writeBuffer = Encoding.Unicode.GetBytes("time");
                        //    clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                        //    size = clientStream.Read(readBuffer, 0, readBuffer.Length);
                        //    message = Encoding.Unicode.GetString(readBuffer, 0, size);
                        //    time += Convert.ToDouble(message);
                        //    lock (targetLock)
                        //    {
                        //        if (x != x1 && y != y1)
                        //        {
                        //            Random r = new Random();
                        //            float between = 0;
                        //            while (between < 90)
                        //            {
                        //                x1 = r.Next(1, 500);
                        //                y1 = r.Next(1, 500);
                        //                between = (float)Math.Sqrt(((x - x1) * (x - x1)) + ((y - y1) * (y - y1)));
                        //            }
                        //            x = x1;
                        //            y = y1;
                        //        }
                        //    }
                        //    string hh = x.ToString() + "|" + y.ToString();
                        //    writeBuffer = Encoding.Unicode.GetBytes(hh);
                        //    clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                        //    lock (targetLock)
                        //    {
                        //        hitget++;
                        //        if (hitget == numberOfClient)
                        //        {
                        //            hit = 0;
                        //        }
                        //    }

                        //    break;
                        //case 4://клиент готов получить результат
                        //    lock (targetLock)
                        //    {
                        //        if (otvet != 0)
                        //        {
                        //            otvet = 0;
                        //        }
                        //        res++;//количесвтво клиентого которые готовы получить результаты
                        //    }
                        //    while (res != numberOfClient) { Thread.Sleep(20); }//ждем других клиентов
                        //    writeBuffer = Encoding.Unicode.GetBytes(numberOfClient.ToString());//отправляем количество влиентов клиентам
                        //    clientStream.Write(writeBuffer, 0, writeBuffer.Length);
                        //    size = clientStream.Read(readBuffer, 0, readBuffer.Length);//ждем команды от клиента что он получил сообщение
                        //    message = Encoding.Unicode.GetString(readBuffer, 0, size);
                        //    if (message != "ready") { break; }
                        //    otvet++;
                        //    while (otvet != numberOfClient) { Thread.Sleep(20); }//ждем других клиентов
                        //    lock (targetLock)
                        //    {
                        //        h = name + ":" + (time / 10).ToString();
                        //        byte[] bytes = Encoding.Unicode.GetBytes(h);
                        //        for (int i = 0; i < Clients.Count; i++)
                        //        {
                        //            Clients[i].Write(bytes, 0, bytes.Length);
                        //        }

                        //    }
                        //    break;
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
                clientStreamWrite.Close();
                client.Close();
                lock (clientLock)
                {
                    Clients.Remove(clientStream);
                    numberOfClient--;
                }
                Console.WriteLine("disconnect,Number of Client:" + numberOfClient.ToString());
            }
        }

        public static void MessageHandler(NetworkStream clientStreamWrite)
        {
            byte[] readBuffer = new byte[1024];
            byte[] writeBuffer = new byte[1024];
            double time = 0;
            int buffer;
            string message;

            while (true)
            {
                buffer = clientStreamWrite.Read(readBuffer, 0, readBuffer.Length);
                message = Encoding.Unicode.GetString(readBuffer, 0, buffer);

                switch (Convert.ToInt32(message))
                {
                    case 1://key up
                        if (!IsIntersects())
                        {
                            bufferMatrixMap = ResetArea();
                            //writeBuffer = Encoding.Unicode.GetBytes(ToString(bufferMatrixMap));
                            //clientStreamWrite.Write(writeBuffer, 0, writeBuffer.Length);
                            currentShape.RotateShape();
                            bufferMatrixMap = Merge();
                            //writeBuffer = Encoding.Unicode.GetBytes(ToString(bufferMatrixMap));
                            //clientStreamWrite.Write(writeBuffer, 0, writeBuffer.Length);
                        }
                        break;
                    case 2:
                        timer1.Interval = 10;
                        break;
                    case 3://move right
                        if (!CollideHor(1))
                        {
                            bufferMatrixMap = ResetArea();
                            //writeBuffer = Encoding.Unicode.GetBytes(ToString(bufferMatrixMap));
                            //clientStreamWrite.Write(writeBuffer, 0, writeBuffer.Length);
                            currentShape.MoveRight();
                            bufferMatrixMap = Merge();
                            //writeBuffer = Encoding.Unicode.GetBytes(ToString(bufferMatrixMap));
                            //clientStreamWrite.Write(writeBuffer, 0, writeBuffer.Length);
                        }
                        break;
                    case 4://move left
                        if (!CollideHor(-1))
                        {
                            bufferMatrixMap = ResetArea();
                            //writeBuffer = Encoding.Unicode.GetBytes(ToString(bufferMatrixMap));
                            //clientStreamWrite.Write(writeBuffer, 0, writeBuffer.Length);
                            currentShape.MoveLeft();
                            bufferMatrixMap = Merge();
                            //writeBuffer = Encoding.Unicode.GetBytes(ToString(bufferMatrixMap));
                            //clientStreamWrite.Write(writeBuffer, 0, writeBuffer.Length);
                        }
                        break;

                }
                Thread.Sleep(10);
            }
        }

        public static void Init()
        {
            size = 25;//размер квадратика в пикселях
            score = 0;
            linesRemoved = 0;
            currentShape = new Shape(3, 0);// должна приходить с сервака
            Interval = 300;

            timer1.Interval = Interval;
            timer1.Elapsed += new ElapsedEventHandler(update);
            timer1.AutoReset = true;
            timer1.Enabled = true;
            timer1.Start();
        }

        private static void update(object sender, ElapsedEventArgs e)
        {
            ResetArea();
            if (!Collide())
            {
                currentShape.MoveDown();
            }
            else
            {
                Merge();
                SliceMap();
                timer1.Interval = Interval;
                currentShape.ResetShape(3, 0);
                if (Collide())
                {
                    for (int i = 0; i < 16; i++)
                    {
                        for (int j = 0; j < 8; j++)
                        {
                            map[i, j] = 0;
                        }
                    }
                    timer1.Elapsed -= new ElapsedEventHandler(update);
                    timer1.Stop();
                    //Init();
                }
            }
            Merge();
       }

        public static int[,] Merge()
        {
            for (int i = currentShape.y; i < currentShape.y + currentShape.sizeMatrix; i++)
            {
                for (int j = currentShape.x; j < currentShape.x + currentShape.sizeMatrix; j++)
                {
                    if (currentShape.matrix[i - currentShape.y, j - currentShape.x] != 0)
                        map[i, j] = currentShape.matrix[i - currentShape.y, j - currentShape.x];
                }
            }
            return map;
        }

        public static int[,] SliceMap()
        {
            int count = 0;
            int curRemovedLines = 0;
            for (int i = 0; i < 16; i++)
            {
                count = 0;
                for (int j = 0; j < 8; j++)
                {
                    if (map[i, j] != 0)
                        count++;
                }
                if (count == 8)
                {
                    curRemovedLines++;
                    for (int k = i; k >= 1; k--)
                    {
                        for (int o = 0; o < 8; o++)
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
            return map;
        }

        public static bool Collide()
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

        public static bool CollideHor(int dir)
        {
            for (int i = currentShape.y; i < currentShape.y + currentShape.sizeMatrix; i++)
            {
                for (int j = currentShape.x; j < currentShape.x + currentShape.sizeMatrix; j++)
                {
                    if (currentShape.matrix[i - currentShape.y, j - currentShape.x] != 0)
                    {
                        if (j + 1 * dir > 7 || j + 1 * dir < 0)
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

        public static int[,] ResetArea()
        {
            for (int i = currentShape.y; i < currentShape.y + currentShape.sizeMatrix; i++)
            {
                for (int j = currentShape.x; j < currentShape.x + currentShape.sizeMatrix; j++)
                {
                    if (i >= 0 && j >= 0 && i < 16 && j < 8)
                    {
                        if (currentShape.matrix[i - currentShape.y, j - currentShape.x] != 0)
                        {
                            map[i, j] = 0;
                        }
                    }
                }
            }
            return map;
        }

        public static bool IsIntersects()
        {
            for (int i = currentShape.y; i < currentShape.y + currentShape.sizeMatrix; i++)
            {
                for (int j = currentShape.x; j < currentShape.x + currentShape.sizeMatrix; j++)
                {
                    if (j >= 0 && j <= 7)
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
                for (int x = 0; x < 8; x++)
                {
                    resultString = String.Concat(resultString, array[y,x].ToString() + " ");
                }
                resultString = String.Concat(resultString, "/");
            }

            return resultString;
        }

    }
}
