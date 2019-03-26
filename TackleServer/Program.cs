using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using System.Data.SQLite;
using System.Collections.Generic;
using System.Diagnostics;

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
                case "CREATECLASS":
                    HandleCreateClass(client, clientRequest);
                    break;
                case "CLASSLIST":
                    HandleClassList(client, clientRequest);
                    break;
                case "DELETECLASS":
                    HandleDeleteClass(client, clientRequest);
                    break;
                case "QUIZMARKINGVIEW":
                    HandleQuizAttemptReturn(client, clientRequest);
                    break;
                case "CREATEQUIZ":
                    HandleCreateQuiz(client, clientRequest);
                    break;
                case "QUIZLIST":
                    HandleQuizList(client, clientRequest);
                    break;
                case "OPENQUIZ":
                    HandleOpenQuiz(client, clientRequest);
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
            client.Close();
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

                        command.Reset();

                        //Checking that the user's not in the class - if they are, then an exception will be thrown so that the catch will catch it
                        SQL = $"SELECT * FROM UserClasses WHERE ClassID='{classID}' AND Username='{username}'";
                        command.CommandText = SQL;
                        reader = command.ExecuteReader();
                        if (reader.HasRows)
                        {
                            Console.WriteLine($"{username} is already in {classID}");
                            throw new SQLiteException();
                        }

                        command.Reset();

                        //Inserting the row that makes the user join the class
                        SQL = $"INSERT INTO UserClasses (ClassID,Username) VALUES ('{classID}','{username}')";
                        command.CommandText = SQL;
                        command.ExecuteNonQuery();

                        //Increasing the class' member count
                        SQL = $"UPDATE Classes SET MemberCount = MemberCount + 1 WHERE ClassID={classID}";
                        command.CommandText = SQL;
                        command.ExecuteNonQuery();

                        Console.WriteLine($"User {username} at {client.RemoteEndPoint} joined class {classID}");
                        JoinClassSendToClient(client, "success");
                    }
                    catch (System.InvalidOperationException exception)
                    {
                        Console.WriteLine(exception.Message);
                    }
                    catch
                    {
                        Console.WriteLine($"User {username} at {client.RemoteEndPoint} failed to join class {classID}");
                        JoinClassSendToClient(client, "failed");
                    }
                }
            }
        }

        static void HandleCreateClass(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];

            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                string SQL = $"INSERT INTO Classes (Username, MemberCount) VALUES ('{username}','0');";

                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    try
                    {
                        command.CommandText = SQL;
                        command.ExecuteNonQuery();
                        Console.WriteLine("New class has been created");
                        CreateClassSendToClient(client, "success");
                    }
                    catch
                    {
                        Console.WriteLine("Class creation failed");
                        CreateClassSendToClient(client, "failed");
                    }
                }
            }
        }

        static void HandleClassList(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];

            //Replaces UTF-8 \0 whitespace with blank
            username = username.Replace("\0", string.Empty);

            //Replaces apostrophies to prevent syntax errors/SQL injection
            username = username.Replace("'", "''");

            string SQL = $"SELECT * FROM Classes WHERE Username='{username}'";

            ClassList list = new ClassList();

            //SQLite search
            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    command.CommandText = SQL;
                    var reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        list.classIDs.Add(reader.GetInt32(0));
                        list.memberCounts.Add(reader.GetInt32(2));
                    }
                }

            }
            ClassListSendToClient(client, list);
        }

        static void HandleDeleteClass(Socket client, ServerRequest clientRequest)
        {
            string classID = clientRequest.requestParameters[0];

            string SQL = $"DELETE FROM Classes WHERE ClassID='{classID}'";

            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    try
                    {
                        command.CommandText = SQL;
                        command.ExecuteNonQuery();
                        DeleteClassSendToClient(client, "success");
                    }
                    catch
                    {
                        DeleteClassSendToClient(client, "failed");
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

        static void HandleCreateQuiz(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];
            string quizType = clientRequest.requestParameters[1];
            string quizTitle = clientRequest.requestParameters[2];
            string quizContent = clientRequest.requestParameters[3];

            string success = CreateQuiz(username, quizType,quizTitle,quizContent);

            CreateQuizSendToClient(client,success);
        }

        static void HandleQuizList(Socket client, ServerRequest clientRequest)
        {
            string searchTerm = clientRequest.requestParameters[0];

            //Replaces UTF-8 \0 whitespace with blank
            searchTerm = searchTerm.Replace("\0", string.Empty);

            //Replaces apostrophies to prevent syntax errors/SQL injection
            searchTerm = searchTerm.Replace("'", "''");

            string SQL = $"SELECT QuizID, Username, QuizType, QuizName FROM Quizzes WHERE Public='True'";

            //If there's a search term, add WHERE to the SQL query
            if (searchTerm != "")
            {
                SQL += $" AND QuizName='{searchTerm}'";
            }

            QuizList list = new QuizList();

            //SQLite search
            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    command.CommandText = SQL;
                    var reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        list.quizIDs.Add(reader.GetInt32(0));
                        list.usernames.Add(reader.GetString(1));
                        list.quizType.Add(reader.GetString(2));
                        list.quizNames.Add(reader.GetString(3));
                    }
                }

            }
            
            QuizListSendToClient(client, list);
        }

        static void HandleOpenQuiz(Socket client, ServerRequest clientRequest)
        {
            int quizID = Int32.Parse(clientRequest.requestParameters[0]);

            string SQL = $"SELECT QuizContent FROM Quizzes WHERE QuizID='{quizID}'";

            QuizList list = new QuizList();

            string JSON = "";

            //SQLite search
            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    command.CommandText = SQL;
                    var reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        JSON = reader.GetString(0);
                    }
                }
            }

            OpenQuizSendToClient(client, JSON);
        }

        //Serialises the log in/sign up response object to a JSON string
        static string Serialise(LogInResponse response)
        {
            string json = JsonConvert.SerializeObject(response, Formatting.Indented);
            return json;
        }

        //Serialises QuizList list
        static string Serialise(QuizList list)
        {
            string json = JsonConvert.SerializeObject(list, Formatting.Indented);
            return json;
        }

        //Serialises ClassList list
        static string Serialise(ClassList list)
        {
            string json = JsonConvert.SerializeObject(list, Formatting.Indented);
            return json;
        }

        //Sends the server response to the client for each type of request

        static void LogInSendToClient(Socket client, LogInResponse response)
        {
            string jsonResponse = Serialise(response);
            byte[] responseBytes = new byte[64];
            responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
            client.Send(responseBytes);
            client.Close();
        }

        static void JoinClassSendToClient(Socket client, string success)
        {
            byte[] responseBytes = new byte[64];
            responseBytes = Encoding.UTF8.GetBytes(success);
            client.Send(responseBytes);
            client.Close();
        }

        static void CreateClassSendToClient(Socket client, string success)
        {
            byte[] responseBytes = new byte[64];
            responseBytes = Encoding.UTF8.GetBytes(success);
            client.Send(responseBytes);
            client.Close();
        }

        static void DeleteClassSendToClient(Socket client, string success)
        {
            byte[] responseBytes = new byte[64];
            responseBytes = Encoding.UTF8.GetBytes(success);
            client.Send(responseBytes);
            client.Close();
        }

        static void QuizAttemptSendToClient(Socket client, string QuizAttempt)
        {
            byte[] responseBytes = new byte[Encoding.UTF8.GetBytes(QuizAttempt).Length];
            responseBytes = Encoding.UTF8.GetBytes(QuizAttempt);
            client.Send(responseBytes);
            client.Close();
        }

        static void CreateQuizSendToClient(Socket client, string success)
        {
            byte[] responseBytes = new byte[64];
            responseBytes = Encoding.UTF8.GetBytes(success);
            client.Send(responseBytes);
            client.Close();
        }

        static void QuizListSendToClient(Socket client, QuizList list)
        {
            string jsonResponse = Serialise(list);
            byte[] responseBytes = new byte[1000];
            responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
            client.Send(responseBytes);
            client.Close();
        }

        static void ClassListSendToClient(Socket client, ClassList list)
        {
            string jsonResponse = Serialise(list);
            byte[] responseBytes = new byte[1000];
            responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
            client.Send(responseBytes);
            client.Close();
        }

        static void OpenQuizSendToClient(Socket client, string JSON)
        {
            byte[] responseBytes = new byte[1000];
            responseBytes = Encoding.UTF8.GetBytes(JSON);
            client.Send(responseBytes);
            client.Close();
        }

        //Creates the new quiz row in the database
        static string CreateQuiz(string username, string quizType, string quizTitle, string quizContent)
        {
            //Escapes any apostrophies to prevent syntax errors/SQL injection
            quizTitle = quizTitle.Replace("'", "''");
            quizContent = quizContent.Replace("'", "''");

            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                string SQL = $"INSERT INTO Quizzes (Username,QuizType,QuizName, QuizContent) VALUES ('{username}','{quizType}','{quizTitle}','{quizContent}')";
                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    try
                    {
                        int queryResponse = command.ExecuteNonQuery();

                        Console.WriteLine($"New quiz created, title {quizTitle}");
                        return "success";
                    }
                    catch
                    {
                        Console.WriteLine($"Quiz creation failed");
                        return "failed";
                    }
                }
            }
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

    //Lists to append the quiz listing info to
    class QuizList
    {
        public List<int> quizIDs;
        public List<string> usernames;
        public List<string> quizType;
        public List<string> quizNames;

        public QuizList()
        {
            //Initiates all of the lists
            quizIDs = new List<int>();
            usernames = new List<string>();
            quizType = new List<string>();
            quizNames = new List<string>();
        }
    }

    class ClassList
    {
        public List<int> classIDs;
        public List<int> memberCounts;

        public ClassList()
        {
            classIDs = new List<int>();
            memberCounts = new List<int>();
        }
    }
}
