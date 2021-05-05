
using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Xml.Serialization;
using System.Collections;

namespace Online_MMORPG
{
    class Program
    {
        //variables used by multiple threads at the same time
        static List<NetworkStream> streams = new List<NetworkStream>();
        static readonly string[,] userCredentials = new string[,] { { "Janne","User"}, { "programmering","password" } };
        //Main method just starts the main program.
        static void Main(string[] args)
        {
            Start();
        }
        private static void Start()
        {
            List<Role> roles = new List<Role>();
            List<Message> messages = new List<Message>();
            Thread timeTick = new Thread(() => BackgroundTick(roles,messages));
            timeTick.Start();
            try
            {
                roles = LoadInstances(roles, new XmlSerializer(typeof(List<Role>)));
                messages = LoadInstances(messages, new XmlSerializer(typeof(List<Message>)));
            }
            catch (Exception)
            {
                Console.WriteLine("Could not find file!");
            }
            
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
            List<Message> messages = new List<Message>();
            bool credentialsMatches = false;
            var client = (TcpClient)obj;
            byte[] bytes = new byte[256];
            string data = "";
            string credentials = "";
            NetworkStream stream = client.GetStream();
            //while loop for recieving message from client and then making it into an array and matching the values with an array that handles credentials. Important to note is that Client sends credentials automatic, so handling is not an issue.
            while (credentialsMatches == false)
            {
                try
                {
                    int messageLength = stream.Read(bytes, 0, bytes.Length);
                    credentials = System.Text.Encoding.UTF8.GetString(bytes, 0, messageLength);
                    Message newMessage = JsonConvert.DeserializeObject<Message>(credentials);
                    System.Console.WriteLine(newMessage.header);
                    if (newMessage.header == "LOGIN")
                    {
                        string[] credentialsArray = newMessage.messageText.Split(',');
                        for (int i = 0; i < userCredentials.GetLength(1); i++)
                        {
                            if (userCredentials[0, i].Contains(credentialsArray[0]) && userCredentials[1, i].Contains(credentialsArray[1]))
                            {
                                //write as JSON format
                                Message credentialMessage = new Message(){header = "MESSAGE", messageText = "yes"};
                                byte[] credentialsMatch = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(credentialMessage));
                                Console.WriteLine("Credentials matches!");
                                stream.Write(credentialsMatch, 0, credentialsMatch.Length);
                                credentialsMatches = true;
                                break;
                            }
                        }
                        break;
                    }
                }
                //catch works for shutting this client connection off and removing it from the list of different network streams if anything were to go wrong (the client probably closed the connection before telling anyone)
                catch (Exception)
                {
                    System.Console.WriteLine("ERROR ON LOGIN");
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
            //this while loop uses stream.Read() to get a message from the client and uses the Loop() method to print it out
            while (true)
            {
                int i = 1;
                while (i != 0)
                {
                    try
                    {
                        i = stream.Read(bytes, 0, bytes.Length);
                        data = System.Text.Encoding.UTF8.GetString(bytes, 0, i);
                        System.Console.WriteLine(data);
                        messages.Add(JsonConvert.DeserializeObject<Message>(data));
                        Message messageLength = new Message();
                        messageLength.header = "MESSAGELENGTH";
                        messageLength.length = i;
                        if (messages[messages.Count -1].header.ToUpper() == "MESSAGE")
                        {
                            Console.WriteLine("Message: " + data);
                            byte[] msg = System.Text.Encoding.UTF8.GetBytes(data);
                            byte[] msgLength = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageLength));
                            Loop(msgLength);
                            Loop(msg);
                        }
                        else if (messages[messages.Count -1].header.ToUpper() == "MESSAGELENGTH")
                        {
                            System.Console.WriteLine("Changing byte length");
                            bytes = new byte[messages[messages.Count - 1].length + 256];
                        }
                    
                    //if the client closes the connection, the server will remove the stream from the streams list and try to end any connection
                    //and close down this thread.
                    }
                    catch (Exception)
                    {
                        System.Console.WriteLine("ERROR ON READING MESSAGE");
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
        //This method is responsible for sending anything from one client, to all clients.
        private static void Loop(Byte[] msg)
        {
            /*the lock is used because there are several threads using the list streams at the same time and we want only one thread to use the 
             * list. This is because if several threads uses the list it can become several errors.
             * The for loop sends any message recieved from one client to all the clients. This makes sure every client can get eachothers messages
             */
            lock (streams)
            {
                Console.WriteLine("yo");
                for (int i = 0; i < streams.Count; i++)
                {
                    Console.WriteLine("tja");
                    streams[i].Write(msg, 0, msg.Length);
                }
            }
        }

        private static void BackgroundTick(List<Role> rolesList, List<Message> messages)
        {
            while (true)
            {
                Thread.Sleep(4000);
                SaveInstances(rolesList, new XmlSerializer(typeof(List<Role>)));
                SaveInstances(messages, new XmlSerializer(typeof(List<Message>)));
            }
        }

        private static void SaveInstances<T>(List<T> genericList, XmlSerializer serializer)
        {
            //filestream closes safely with using statement. Open or creates file and serializes the list inputed in parameter.
            using (FileStream serverFile = File.Open(genericList.GetType() + ".xml", FileMode.OpenOrCreate))
            {
                serializer.Serialize(serverFile, genericList);
            }
        }

        private static List<T> LoadInstances<T>(List<T> genericList, XmlSerializer serializer)
        {
            //filestream closes with using statement. Opens file, deserialize it to List with tamagochis and returns it.
            using FileStream serverStream = File.OpenRead(genericList.GetType().Namespace + ".xml");
            return (List<T>)serializer.Deserialize(serverStream);
        }
    }
}