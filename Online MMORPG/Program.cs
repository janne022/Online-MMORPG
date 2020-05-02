
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
        static List<NetworkStream> streams = new List<NetworkStream>();
        static readonly string[,] userCredentials = new string[,] { { "Janne","Micke" }, { "programmering","Programmering2" } };
        static void Main(string[] args)
        {
            Start();
        }
        private static void Start()
        {
            //Ask for port and then takes local ip, then uses TCPListener to start listening for ports
            Console.WriteLine("type port");
            string portString = Console.ReadLine();
            bool success = int.TryParse(portString, out int port);
            TcpListener server = new TcpListener(IPAddress.Any, port);
            Console.Clear();
            server.Start();

            //accepts any incoming connection and uses the threadpool to safely queue the client into a seperate thread, within a method
            while (true)
            {
                Console.WriteLine("waiting for connection");
                TcpClient client = server.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(SendAndRecieve, client);
                Console.WriteLine("connected");
            }
        }
        private static void SendAndRecieve(object obj)
        {
            bool credentialsMatches = false;
            var client = (TcpClient)obj;
            Byte[] bytes = new Byte[256];
            string data = "";
            string credentials = "";
            NetworkStream stream = client.GetStream();
            //while loop for recieving message from client and then making it into an array and matching the values with an array that handles credentials. Import to note is that Client sends credentials automatic, so handling is not an issue.
            while (credentialsMatches == false)
            {
                try
                {
                    int messageLength = stream.Read(bytes, 0, bytes.Length);
                    credentials = System.Text.Encoding.UTF8.GetString(bytes, 0, messageLength);
                    Console.WriteLine(credentials);
                    string[] credentialsArray = credentials.Split(',');
                    for (int i = 0; i < userCredentials.GetLength(1); i++)
                    {
                        if (userCredentials[0, i].Contains(credentialsArray[0]) && userCredentials[1, i].Contains(credentialsArray[1]))
                        {
                            Console.WriteLine("Credentials matches!");

                            credentialsMatches = true;
                        }
                    }
                }
                //catch works for shutting this client connection off and removing it from the list of different network streams if anything were to go wrong (the client probably closed the connection before telling anyone)
                catch (Exception)
                {
                    lock (streams)
                    {
                        streams.Remove(stream);
                    }
                    client.Close();
                    return;
                }
            }
            lock (streams)
            {
                streams.Add(stream);
            }
            //
            while (true)
            {
                int i = 1;
                while (i != 0)
                {
                    try
                    {
                        i = stream.Read(bytes, 0, bytes.Length);
                        data = System.Text.Encoding.UTF8.GetString(bytes, 0, i);
                        Console.WriteLine("Messages: " + data);
                        byte[] msg = System.Text.Encoding.UTF8.GetBytes(data.ToString());
                        Loop(msg);
                    }
                    catch (Exception)
                    {
                        lock (streams)
                        {
                            streams.Remove(stream);
                        }
                        client.Close();
                        return;
                    }
                }
            }

        }

        private static void Loop(Byte[] msg)
        {
            lock (streams)
            {
                for (int i = 0; i < streams.Count; i++)
                {
                    streams[i].Write(msg, 0, msg.Length);
                }
            }
        }
    }
}