using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using System.Data.SQLite;
using System.Collections.Generic;

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

                    Thread clientReceiveThread = new Thread(new ParameterizedThreadStart(ClientHandlingThread));

                    clientReceiveThread.Start(client);
                }
                Thread.Sleep(300);
            }

            
        }

        //The function that is called when the thread for handling a client is started
        public static void ClientHandlingThread(object param)
        {
            Socket client = (Socket)param;

            byte[] message = new byte[1024];
            client.Receive(message);
            string jsonReceived = Encoding.Default.GetString(message);
            ServerRequest clientRequest = JsonConvert.DeserializeObject<ServerRequest>(jsonReceived);
            HandleRequest(client, clientRequest);
        }

        //Handles the client's request
        static void HandleRequest(Socket client, ServerRequest clientRequest)
        {
            switch (clientRequest.requestSource)
            {
                case "SIGNUP":
                    HandleSignup(client, clientRequest);
                    break;
                case "LOGIN":
                    HandleLogin(client, clientRequest);
                    break;
                case "SUBMITRESULTS":
                    HandleResultSubmit(client, clientRequest);
                    break;
                case "JOINCLASS":
                    HandleJoinClass(client, clientRequest);
                    break;
                case "QUIZMARKINGVIEW":
                    HandleQuizAttemptReturn(client, clientRequest);
                    break;
            }
        }

        static void HandleResultSubmit(Socket client, ServerRequest clientRequest)
        {
            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                //Escapes any of apostrophes in the JSON
                clientRequest.requestParameters[2] = clientRequest.requestParameters[2].Replace("'", "''");

                string SQL = $"INSERT INTO QuizAttempts (QuizID,Username,QuizInfo) VALUES ('{clientRequest.requestParameters[0]}','{clientRequest.requestParameters[1]}','{clientRequest.requestParameters[2]}')";
                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    int modifiedRows = command.ExecuteNonQuery();
                    Console.WriteLine($"Result submission request handled with {0} row(s) affected", modifiedRows);
                }
            }
        }

        static void HandleLogin(Socket client, ServerRequest clientRequest)
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

        static void HandleSignup(Socket client, ServerRequest clientRequest)
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
                        LogInSendToClient(client, signUpResponse);
                    }
                }
            }
        }

        static void HandleJoinClass(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];
            string classID = clientRequest.requestParameters[1];

            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                //Checking if the class exists
                string SQL = $"SELECT * FROM Classes WHERE ClassID='{classID}'";
                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    try
                    {
                        //Executes the reader to check if any rows are returned by the query - if not then an SQLite exception to be caught by
                        //the catch is thrown
                        var reader = command.ExecuteReader();

                        if (!reader.HasRows)
                        {
                            throw new SQLiteException();
                        }
                        

                        //Checking that the user's not in the class - if they are, then an exception will be thrown so that the catch will catch it
                        SQL = $"SELECT * FROM UserClasses WHERE ClassID='{classID}' AND Username='{username}'";
                        command.CommandText = SQL;
                        reader = command.ExecuteReader();
                        if (reader.HasRows)
                        {
                            Console.WriteLine($"{username} is already in {classID}");
                            throw new SQLiteException();
                        }

                        //Inserting the row that makes the user join the class
                        SQL = $"INSERT INTO UserClasses (ClassID,Username) VALUES ('{classID}','{username}')";
                        command.CommandText = SQL;
                        command.ExecuteNonQuery();

                        Console.WriteLine($"User {username} at {client.RemoteEndPoint} joined class {classID}");
                        JoinClassSendToClient(client, "success");
                    }
                    catch
                    {
                        Console.WriteLine($"User {username} at {client.RemoteEndPoint} failed to join class {classID}");
                        JoinClassSendToClient(client, "failed");
                    }
                }
            }
        }

        static void HandleQuizAttemptReturn(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];
            string quizID = clientRequest.requestParameters[1];

            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                //Getting the QuizAttempt row
                string SQL = $"SELECT * FROM QuizAttempts WHERE QuizID='{quizID}' AND Username='{username}';";
                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    string QuizAttempt;

                    //Executes the reader to read the QuizAttempt row
                    using (SQLiteDataReader rdr = command.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            QuizAttempt = rdr.GetString(2);
                        }
                        else
                        {
                            QuizAttempt = "FALSE";
                        }
                        QuizAttemptSendToClient(client, QuizAttempt);
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

        static void JoinClassSendToClient(Socket client, string success)
        {
            byte[] responseBytes = new byte[64];
            responseBytes = Encoding.UTF8.GetBytes(success);
            client.Send(responseBytes);
        }

        static void QuizAttemptSendToClient(Socket client, string QuizAttempt)
        {
            byte[] responseBytes = new byte[Encoding.UTF8.GetBytes(QuizAttempt).Length];
            responseBytes = Encoding.UTF8.GetBytes(QuizAttempt);
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
