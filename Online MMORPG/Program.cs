using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace Online_MMORPG
{
    class Program
    {
        static List<NetworkStream> messages = new List<NetworkStream>();
        static void Main(string[] args)
        {
            Console.WriteLine("type port");
            string portString = Console.ReadLine();
            bool success = int.TryParse(portString, out int port);
            IPAddress ip = IPAddress.Parse("10.151.168.253");
            
            TcpListener server = new TcpListener(IPAddress.Any, port);
            server.Start();

            while (true)
            {
                Console.WriteLine("waiting for connection");
                TcpClient client = server.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(SeperateThread, client);
                Console.WriteLine("connected");
            }

            

        }


        private static void SeperateThread(object obj)
        {
            var client = (TcpClient)obj;

            Byte[] bytes = new Byte[256];
            String data = null;
            NetworkStream stream = client.GetStream();

            messages.Add(stream);
            while (true)
            {
                int i;

                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                    Console.WriteLine("Data: " + data);
                    string response = data;
                    byte[] msg = System.Text.Encoding.ASCII.GetBytes(response);
                    Loop(msg);

                }
                client.Close();
                
            }

        }

        private static void Loop(Byte[] msg)
        {
            
            for (int i = 0; i < messages.Count; i++)
            {
                messages[i].Write(msg, 0, msg.Length);
            }
            
            
        }
    }
}
