﻿
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
        static List<List<string>> users = new List<List<string>>();
        static readonly string[,] userCredentials = new string[,] { { "Janne","User"}, { "programmering","password" } };
        static string arguments;
        static Dictionary<string, Action> commands = new System.Collections.Generic.Dictionary<string, Action>()
        {
            {"!ping",() => Ping(arguments)},
            {"!help",() => Help(arguments)}
        };
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
            User currentUser = new User();
            List<Message> messages = new List<Message>();
            bool credentialsMatches = false;
            var client = (TcpClient)obj;
            byte[] bytes = new byte[1026];
            string data = "";
            NetworkStream stream = client.GetStream();
            //while loop reads message and deserializes it as Message(class), then it checks the header of the message to know how to treat content
            while (true)
            {
                try
                {
                    //Reads message from networkstream and deserializes it as Message class
                    int messageLength = stream.Read(bytes, 0, bytes.Length);
                    data = System.Text.Encoding.UTF8.GetString(bytes, 0, messageLength);
                    Message newMessage = JsonConvert.DeserializeObject<Message>(data);
                    System.Console.WriteLine(newMessage.header);
                    //first message is always going to be a MESSAGELENGTH, containing int on how long byte[] should be
                    if (newMessage.header == "MESSAGELENGTH")
                    {
                        bytes = new byte[newMessage.length + 256];
                    }
                    //Login will check if credential matches, if it does messages by Client will be recieved and sent to rest of Clients
                    //FIX: SEND MESSAGE FOR IF LOGIN FAIL TO CLIENT
                    else if (newMessage.header == "LOGIN" && credentialsMatches == false)
                    {
                        string[] credentialsArray = newMessage.messageText.Split(',');
                        for (int j = 0; j < userCredentials.GetLength(1); j++)
                        {
                            if (userCredentials[0, j].Contains(credentialsArray[0]) && userCredentials[1, j].Contains(credentialsArray[1]))
                            {
                                //write as JSON format
                                currentUser.Username = credentialsArray[0];
                                currentUser.Password = credentialsArray[1];
                                Message credentialMessage = new Message() { header = "MESSAGE", messageText = "yes" };
                                byte[] credentialsMatch = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(credentialMessage));
                                Console.WriteLine("Credentials matches!");
                                stream.Write(credentialsMatch, 0, credentialsMatch.Length);
                                lock (streams)
                                {
                                    streams.Add(stream);
                                }
                                credentialsMatches = true;
                                break;
                            }
                        }
                    }
                    //if user sends message, the message will be saved by the server and send to every other client
                    else if (newMessage.header == "MESSAGE" && credentialsMatches == true)
                    {
                        //TODO: Match uuid with username and send back message with String name
                        messages.Add(newMessage);
                        Message newLength = new Message();
                        newLength.header = "MESSAGELENGTH";
                        newLength.length = messageLength;
                        if (messages[messages.Count - 1].header.ToUpper() == "MESSAGE")
                        {
                            Console.WriteLine("Message: " + data);
                            newMessage.name = currentUser.Username;
                            byte[] msg = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(newMessage));
                            byte[] msgLength = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(newLength));
                            Loop(msgLength);
                            Loop(msg);
                        }
                    }
                    //if user sends command, it will try accessing an Action in Dictionary using input key from user text
                    else if (newMessage.header == "COMMAND" && credentialsMatches == true)
                    {
                        string[] commandInput = newMessage.messageText.Split(" ");
                        try
                        {
                            commands[commandInput[0]]();
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Invalid command!");
                        }
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
                for (int i = 0; i < streams.Count; i++)
                {
                    streams[i].Write(msg, 0, msg.Length);

                }
            }
        }
        //BackggroundTick is responisble for running methods once in a while in the background. Currently only runs SaveInstances, which saves classes to xml files
        private static void BackgroundTick(List<Role> rolesList, List<Message> messages)
        {
            while (true)
            {
                Thread.Sleep(4000);
                SaveInstances(rolesList, new XmlSerializer(typeof(List<Role>)));
                SaveInstances(messages, new XmlSerializer(typeof(List<Message>)));
            }
        }
        //Saves List to xml file
        //POSSIBLE FIX: GetType class saved by list, for better name
        private static void SaveInstances<T>(List<T> genericList, XmlSerializer serializer)
        {
            //filestream closes safely with using statement. Open or creates file and serializes the list inputed in parameter.
            using (FileStream serverFile = File.Open(genericList.GetType() + ".xml", FileMode.OpenOrCreate))
            {
                serializer.Serialize(serverFile, genericList);
            }
        }
        //Loads xml file into list and returns the list (SHOULD ONLY BE RAN ON START OF PROGRAM)
        private static List<T> LoadInstances<T>(List<T> genericList, XmlSerializer serializer)
        {
            using FileStream serverStream = File.OpenRead(genericList.GetType().Namespace + ".xml");
            return (List<T>)serializer.Deserialize(serverStream);
        }
        //Ping command sends "Pong!" back
        //FIX: Move to new class for commands
        private static void Ping(string arguments)
        {
            Message pingMessage = new Message();
            Message newLength = new Message();
            pingMessage.header = "MESSAGE";
            pingMessage.messageText = "Pong!";
            pingMessage.name = "Server";
            newLength.length = Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(newLength));
            byte[] messageLength = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(newLength));
            byte[] message = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(pingMessage));
            Loop(messageLength);
            Loop(message);
        }
        //Command for listing all Actions in Dictonary (listing all commands and sending them to clients)
        private static void Help(string arguments)
        {
            string commandList = "";
            foreach (string item in commands.Keys)
            {
                commandList += item + " , ";
            }
            Message pingMessage = new Message();
            Message newLength = new Message();
            pingMessage.header = "MESSAGE";
            pingMessage.messageText = commandList;
            pingMessage.name = "Server";
            newLength.length = Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(newLength));
            byte[] messageLength = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(newLength));
            byte[] message = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(pingMessage));
            Loop(messageLength);
            Loop(message);
        }
    }
}