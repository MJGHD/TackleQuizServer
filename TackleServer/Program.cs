using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Data.SQLite;

namespace TackleServer
{
    class Program
    {
        static void Main()
        {
            TcpListener server = new TcpListener(IPAddress.Any, 8888);
            server.Start();

            //Loop that constantly checks whether there's an incoming request
            Console.WriteLine("Client checking loop started");
            while (true)
            {
                if (server.Pending())
                {
                    Socket client = server.AcceptSocket();
                    Console.WriteLine("Client handling thread started");

                    Thread clientReceiveThread = new Thread(() =>
                    {
                        byte[] message = new byte[1024];
                        client.Receive(message);
                        string jsonReceived = Encoding.Default.GetString(message);
                        ServerRequest clientRequest = JsonConvert.DeserializeObject<ServerRequest>(jsonReceived);
                        HandleRequest(client, clientRequest);
                    });

                    clientReceiveThread.Start();
                }
                Thread.Sleep(300);
            }

            void HandleRequest(Socket client, ServerRequest clientRequest)
            {
                if(clientRequest.requestSource == "SIGNUP")
                {
                    string username = clientRequest.requestParameters[0];
                    string password = clientRequest.requestParameters[1];
                    int userType = Int32.Parse(clientRequest.requestParameters[2]);

                    SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;");
                    databaseConnection.Open();

                    //Escapes any apostrophies in the usernames or passwords so that syntax errors with apostrophes can't occur
                    username = username.Replace("'", "''");
                    password = password.Replace("'", "''");

                    string SQL = $"INSERT INTO Users (Username,Password,UserType) VALUES ('{username}','{password}',{userType})";
                    SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection);
                    command.ExecuteNonQuery();

                    Console.WriteLine($"Sign up request handled for user '{username}' at {client.RemoteEndPoint}");
                    byte[] response = new byte[] {1};
                    client.Send(response);
                    client.Close();
                }
            }
        }

        class ServerRequest
        {
            public string requestSource;
            public string[] requestParameters;
        }
    }
}
