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
    //TODO: convert instances of "userType" to "isTeacher"
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

                    Thread clientReceiveThread = new Thread(new ParameterizedThreadStart(ClientHandlingThread));

                    clientReceiveThread.Start(client);
                }
                Thread.Sleep(300);
            }

            
        }

        public static void ClientHandlingThread(object param)
        {
            Socket client = (Socket)param;

            byte[] message = new byte[1024];
            client.Receive(message);
            string jsonReceived = Encoding.Default.GetString(message);
            ServerRequest clientRequest = JsonConvert.DeserializeObject<ServerRequest>(jsonReceived);
            HandleRequest(client, clientRequest);
        }

        static void HandleRequest(Socket client, ServerRequest clientRequest)
        {
            if (clientRequest.requestSource == "SIGNUP")
            {
                string username = clientRequest.requestParameters[0];
                string password = clientRequest.requestParameters[1];
                bool isTeacher = Convert.ToBoolean(clientRequest.requestParameters[2]);


                //Escapes any apostrophies in the usernames or passwords so that syntax errors with apostrophes can't occur
                username = username.Replace("'", "''");
                password = password.Replace("'", "''");

                using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
                {
                    databaseConnection.Open();

                    string SQL = $"INSERT INTO Users (Username,Password,IsTeacher) VALUES ('{username}','{password}','{isTeacher.ToString()}')";
                    using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                    {
                        LogInResponse signUpResponse = new LogInResponse();

                        try
                        {
                            int queryResponse = command.ExecuteNonQuery();

                            Console.WriteLine($"Sign up request handled for user '{username}' at {client.RemoteEndPoint}");
                            signUpResponse.requestSuccess = true;
                            signUpResponse.isTeacher = isTeacher;
                            LogInSendToClient(client, signUpResponse);
                        }
                        catch
                        {
                            Console.WriteLine($"Sign up request failed at {client.RemoteEndPoint}");
                            signUpResponse.requestSuccess = false;
                            LogInSendToClient(client,signUpResponse);
                        }
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

                    LogInResponse logResponse = new LogInResponse();

                    string SQL = $"SELECT * FROM Users WHERE username = '{username}' AND password = '{password}'";

                    using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                    {

                        using (SQLiteDataReader rdr = command.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                logResponse.requestSuccess = true;
                                logResponse.isTeacher = Convert.ToBoolean(rdr.GetString(2));
                            }
                            else
                            {
                                logResponse.requestSuccess = false;
                            }
                        }
                        LogInSendToClient(client, logResponse);

                        Console.WriteLine($"\nClient at {client.RemoteEndPoint} tried to log in with a {logResponse.requestSuccess} response\n");
                    }
                }
            }
        }

        //Serialises the log in/sign up response object to a JSON string
        static string Serialise(LogInResponse response)
        {
            string json = JsonConvert.SerializeObject(response, Formatting.Indented);
            return json;
        }

        //Sends the result of the log in/sign up request to the client
        static void LogInSendToClient(Socket client, LogInResponse response)
        {
            string jsonResponse = Serialise(response);
            byte[] responseBytes = new byte[64];
            responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
            client.Send(responseBytes);
        }
    }

    class LogInResponse
    {
        public bool requestSuccess;
        public bool isTeacher;
    }

    class ServerRequest
    {
        public string requestSource;
        public string[] requestParameters;
    }
}
