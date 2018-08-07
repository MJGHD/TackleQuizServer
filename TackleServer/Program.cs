﻿using System;
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

            
        }

        static void HandleRequest(Socket client, ServerRequest clientRequest)
        {
            if (clientRequest.requestSource == "SIGNUP")
            {
                string username = clientRequest.requestParameters[0];
                string password = clientRequest.requestParameters[1];
                int userType = Int32.Parse(clientRequest.requestParameters[2]);


                //Escapes any apostrophies in the usernames or passwords so that syntax errors with apostrophes can't occur
                username = username.Replace("'", "''");
                password = password.Replace("'", "''");

                using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
                {
                    databaseConnection.Open();

                    string SQL = $"INSERT INTO Users (Username,Password,UserType) VALUES ('{username}','{password}',{userType})";
                    using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                    {
                        int queryResponse = command.ExecuteNonQuery();
                        string responseString;

                        if (queryResponse == 0)
                        {
                            Console.WriteLine($"Failed signup request from user at {client.RemoteEndPoint}");
                            responseString = "FAILED";
                        }
                        else
                        {
                            Console.WriteLine($"Sign up request handled for user '{username}' at {client.RemoteEndPoint}");
                            responseString = "SUCCESS";
                        }

                        byte[] response = new byte[16];
                        response = Encoding.UTF8.GetBytes(responseString);

                        client.Send(response);
                    }

                }

                
            }
            else if (clientRequest.requestSource == "LOGIN")
            {
                string username = clientRequest.requestParameters[0];
                string password = clientRequest.requestParameters[1];

                //Escapes any apostrophies in the usernames or passwords so that syntax errors with apostrophes can't occur
                username = username.Replace("'", "''");
                password = password.Replace("'", "''");

                using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
                {
                    databaseConnection.Open();

                    string SQL = $"SELECT * FROM Users WHERE username = '{username}' AND password = '{password}'";

                    using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                    {
                        int userType = 0;

                        using (SQLiteDataReader rdr = command.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                userType = rdr.GetInt32(2);
                            }
                        }

                        LogInResponse logResponse = new LogInResponse();
                        logResponse.requestSuccess = true;
                        logResponse.userType = userType;

                        string jsonResponse = Serialise(logResponse);

                        byte[] response = new byte[64];

                        response = Encoding.UTF8.GetBytes(jsonResponse);


                        client.Send(response);

                        Console.WriteLine($"\nClient at {client.RemoteEndPoint} tried to log in with a {logResponse.requestSuccess} response\n");
                    }
                }
            }
        }

        public static string Serialise(LogInResponse response)
        {
            string json = JsonConvert.SerializeObject(response, Formatting.Indented);
            return json;
        }
    }

    class LogInResponse
    {
        public bool requestSuccess;
        public int userType;
    }

    class ServerRequest
    {
        public string requestSource;
        public string[] requestParameters;
    }
}
