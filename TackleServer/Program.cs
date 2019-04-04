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

            byte[] message = new byte[50000];
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
                case "QUIZMARKINGLIST":
                    HandleMarkList(client, clientRequest);
                    break;
                case "FINISHMARKING":
                    HandleFinishMarking(client, clientRequest);
                    break;
                case "CREATEQUIZ":
                    HandleCreateQuiz(client, clientRequest);
                    break;
                case "CREATEDRAFT":
                    HandleCreateDraft(client, clientRequest);
                    break;
                case "DRAFTLIST":
                    HandleDraftList(client, clientRequest);
                    break;
                case "DELETEDRAFT":
                    HandleDeleteDraft(client, clientRequest);
                    break;
                case "SUBMITQUIZTOCLASS":
                    HandleSubmitToClass(client, clientRequest);
                    break;
                case "SENDTOCLASS":
                    HandleSendToClass(client, clientRequest);
                    break;
                case "QUIZLIST":
                    HandleQuizList(client, clientRequest);
                    break;
                case "OPENQUIZ":
                    HandleOpenQuiz(client, clientRequest);
                    break;
                case "GETLEADERBOARD":
                    HandleLeaderboardGet(client, clientRequest);
                    break;
                case "HOMEWORKLIST":
                    HandleHomeworkList(client, clientRequest);
                    break;
                case "REQUESTLIST":
                    HandleRequestList(client, clientRequest);
                    break;
                case "ACCEPTREQUEST":
                    HandleRequestAccept(client, clientRequest);
                    break;
                case "REMOVECLASSMEMBER":
                    HandleRemoveClassMember(client, clientRequest);
                    break;
                case "CLASSMEMBERLIST":
                    HandleClassMemberList(client, clientRequest);
                    break;
                case "TEACHERQUIZHISTORY":
                    HandleTeacherQuizHistory(client, clientRequest);
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

                string SQL = $"INSERT INTO QuizAttempts (QuizID,Username,QuizInfo, Correct) VALUES ('{clientRequest.requestParameters[0]}','{clientRequest.requestParameters[1]}','{clientRequest.requestParameters[2]}','{clientRequest.requestParameters[3]}')";
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

                        //Putting the user request into the database
                        SQL = $"INSERT INTO ClassRequests (ClassID,Username) VALUES ('{classID}','{username}')";
                        command.CommandText = SQL;
                        command.ExecuteNonQuery();

                        Console.WriteLine($"User {username} at {client.RemoteEndPoint} requested to join class {classID}");
                        JoinClassSendToClient(client, "success");
                    }
                    // was used to fix a bug once
                    //catch (System.InvalidOperationException exception)
                    //{
                    //    Console.WriteLine(exception.Message);
                    //}
                    catch
                    {
                        Console.WriteLine($"User {username} at {client.RemoteEndPoint} failed to join class {classID}");
                        JoinClassSendToClient(client, "failed");
                    }
                }
            }
        }

        static void HandleRequestList(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];

            string[] classes = GetTeacherClasses(username);

            ClassRequests classRequests = new ClassRequests();

            string SQL = RequestListSQL(classes);

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
                        classRequests.classIDs.Add(reader.GetInt32(0).ToString());
                        classRequests.usernames.Add(reader.GetString(1));
                    }
                }
            }

            ClassRequestSendToClient(client, classRequests);
        }

        static string RequestListSQL(string[] classes)
        {
            //the part that will be added to the SQL
            string SQLConditional = "";

            int counter = 0;

            foreach (string ID in classes)
            {
                if (counter == 0)
                {
                    SQLConditional += $"'{ID}'";
                }
                else
                {
                    SQLConditional += $" OR ClassID='{ID}'";
                }
                counter += 1;
            }

            return $"SELECT ClassID, Username FROM ClassRequests WHERE ClassID={SQLConditional}";
        }

        static string[] GetTeacherClasses(string username)
        {
            //temporarily uses a list as it's dynamic
            List<string> classes = new List<string>();

            string SQL = $"SELECT ClassID FROM Classes WHERE Username='{username}'";

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
                        classes.Add(reader.GetInt32(0).ToString());
                    }
                }
            }

            //returns list of classes as string array
            return classes.ToArray();
        }

        static void HandleRequestAccept(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];
            string classID = clientRequest.requestParameters[1];

            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();
                
                string SQL = $"DELETE FROM ClassRequests WHERE ClassID='{classID}' AND Username='{username}'";

                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    try
                    {
                        command.ExecuteNonQuery();

                        command.Reset();

                        //Inserting the user into the class
                        SQL = $"INSERT INTO UserClasses (ClassID,Username) VALUES ('{classID}','{username}')";
                        command.CommandText = SQL;
                        command.ExecuteNonQuery();

                        command.Reset();

                        //Increasing the class' member count
                        SQL = $"UPDATE Classes SET MemberCount = MemberCount + 1 WHERE ClassID={classID}";
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

        static void HandleRemoveClassMember(Socket client, ServerRequest clientRequest)
        {
            string classID = clientRequest.requestParameters[0];
            string username = clientRequest.requestParameters[1];

            string success;

            try
            {
                using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
                {
                    databaseConnection.Open();

                    string SQL = $"DELETE FROM UserClasses WHERE ClassID='{classID}' AND Username='{username}'";

                    using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                    {
                        command.ExecuteNonQuery();
                        success = "success";
                    }
                }
            }
            catch
            {
                success = "failed";
            }

            DeleteClassSendToClient(client, success);
        }

        static void HandleClassMemberList(Socket client, ServerRequest clientRequest)
        {
            string classID = clientRequest.requestParameters[0];
            List<string> classMembers = new List<string>();

            string SQL = $"SELECT Username FROM UserClasses WHERE ClassID='{classID}'";

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
                        classMembers.Add(reader.GetString(0));
                    }
                }
            }

            ClassMemberListSendToClient(client, classMembers);
        }

        static void HandleMarkList(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];

            string[] classes = GetTeacherClasses(username);

            string[] quizzes = GetSetQuizzes(classes);

            string SQL = MarkListSQL(quizzes);

            MarkList markList = new MarkList();

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
                        markList.quizIDs.Add(reader.GetInt32(0).ToString());
                        markList.usernames.Add(reader.GetString(1));
                    }
                }
            }

            MarkListSendToClient(client, markList);
        }

        static string MarkListSQL(string[] setQuizzes)
        {
            //the part that will be added to the SQL
            string SQLConditional = "";

            int counter = 0;

            foreach (string ID in setQuizzes)
            {
                if (counter == 0)
                {
                    SQLConditional += $"{ID}";
                }
                else
                {
                    SQLConditional += $" OR QuizID='{ID}'";
                }
            }

            //return final SQL
            return $"SELECT QuizID, Username FROM QuizAttempts WHERE QuizID='{SQLConditional}'";
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

        static void HandleCreateDraft(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];
            string quizType = clientRequest.requestParameters[1];
            string quizTitle = clientRequest.requestParameters[2];
            string quizContent = clientRequest.requestParameters[3];
        
            string success = CreateQuiz(username, quizType, quizTitle, quizContent,"False","True",false);
            
            //Sends whether it was a success to the client
            CreateQuizSendToClient(client, success);
        }

        static void HandleSubmitToClass(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];
            string quizType = clientRequest.requestParameters[1];
            string quizTitle = clientRequest.requestParameters[2];
            string quizContent = clientRequest.requestParameters[3];
            string classID = clientRequest.requestParameters[4];

            string ID = CreateQuiz(username, quizType, quizTitle, quizContent, "False", "False", true);

            string success;

            //If the create quiz query returned an ID, submit it to the SetQuizzes table
            if(ID != "failed")
            {
                success = SetQuiz(ID, classID, username);
            }
            else
            {
                success = "failed";
            }

            //Sends whether it was a success to the client
            CreateQuizSendToClient(client, success);
        }

        static void HandleSendToClass(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];
            string classID = clientRequest.requestParameters[1];
            string quizID = clientRequest.requestParameters[2];

            string success = SetQuiz(quizID, classID, username);

            //Sends whether it was a success to the client
            CreateQuizSendToClient(client, success);
        }

        static void HandleDeleteDraft(Socket client, ServerRequest clientRequest)
        {
            string quizID = clientRequest.requestParameters[0];

            Debug.WriteLine(quizID);

            string success;

            try
            {
                using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
                {
                    databaseConnection.Open();

                    string SQL = $"DELETE FROM Quizzes WHERE QuizID='{quizID}'";

                    using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                    {
                        command.ExecuteNonQuery();
                        success = "success";
                    }
                }
            }
            catch
            {
                success = "failed";
            }

            //Sends whether it was a success to the client
            CreateQuizSendToClient(client, success);
        }

        static void HandleLeaderboardGet(Socket client, ServerRequest clientRequest)
        {
            string quizID = clientRequest.requestParameters[0];

            Leaderboards leaderboard = new Leaderboards();

            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                string SQL = $"SELECT Username, Correct FROM QuizAttempts WHERE QuizID='{quizID}'";

                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    var reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        leaderboard.usernames.Add(reader.GetString(0));
                        leaderboard.correct.Add(reader.GetInt32(1).ToString());
                    }
                }
            }

            //Sends leaderboard to client
            LeaderboardSendToClient(client, leaderboard);
        }

        static void HandleFinishMarking(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];
            string quizID = clientRequest.requestParameters[1];
            string correctTotal = clientRequest.requestParameters[2];

            string success;

            try
            {
                using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
                {
                    databaseConnection.Open();

                    string SQL = $"UPDATE QuizAttempts SET Correct = '{correctTotal}' WHERE QuizID='{quizID}' AND Username='{username}'";

                    using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                    {
                        command.ExecuteNonQuery();
                        success = "success";
                    }
                }
            }
            catch
            {
                success = "failed";
            }

            //Sends whether it was a success to the client
            CreateQuizSendToClient(client, success);
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

        static void HandleDraftList(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];

            string SQL = $"SELECT QuizID, Username, QuizType, QuizName FROM Quizzes WHERE Username='{username}' AND Draft='True'";

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
            //weird naming due to "public" being reserved in C# - i'm not bad at naming variables i swear
            string publicState = clientRequest.requestParameters[4];

            string success = CreateQuiz(username, quizType,quizTitle,quizContent,publicState,"False",false);

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

        static void HandleHomeworkList(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];

            //Replaces UTF-8 \0 whitespace with blank
            username = username.Replace("\0", string.Empty);

            //Gets the classes that the user is a member of
            string[] classes = GetClasses(username);

            //Gets the quiz IDs of the quizzes that are set for the classes that the student is a member of
            string[] setQuizzes = GetSetQuizzes(classes);

            string SQL = HomeworkListSQL(setQuizzes);

            QuizList list = new QuizList();

            //SQLite search for the list of set quizzes
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

                //goes through each quiz ID getting the highest score
                foreach (int quizID in list.quizIDs)
                {
                    //High score search
                    SQL = $"SELECT Correct FROM QuizAttempts WHERE Username = '{username}' AND QuizID='{quizID}' ORDER BY Correct DESC LIMIT 1";

                    using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                    {
                        command.CommandText = SQL;
                        var reader = command.ExecuteReader();

                        //if there's a response, add that, else the user hasn't taken the quiz yet
                        if (reader.Read())
                        {
                            list.topMarks.Add(reader.GetInt32(0).ToString());
                        }
                        else
                        {
                            list.topMarks.Add("N/A");
                        }
                    }
                }
            }

            QuizListSendToClient(client, list);
        }

        static string[] GetClasses(string username)
        {
            //temporarily uses a list as it's dynamic
            List<string> classes = new List<string>();

            string SQL = $"SELECT ClassID FROM UserClasses WHERE Username='{username}'";

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
                        classes.Add(reader.GetInt32(0).ToString());
                    }
                }
            }

            //returns list of classes as string array
            return classes.ToArray();
        }

        static string[] GetSetQuizzes(string[] classes)
        {
            List<string> quizIDs = new List<string>();

            //if there's a class, or classes, then iterate through them getting the quiz IDs set for that class
            if(classes.Length != 0)
            {
                foreach(string classID in classes)
                {
                    string SQL = $"SELECT QuizID FROM SetQuizzes WHERE ClassID='{classID}'";

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
                                quizIDs.Add(reader.GetInt32(0).ToString());
                            }
                        }
                    }

                }

                //returns list of classes as string array
                return quizIDs.ToArray();
            }
            else
            {
                //just return 0, as there isn't a quiz ID 0 so it'll just be empty
                return new string[] { "0" };
            }
        }

        static string HomeworkListSQL(string[] setQuizzes)
        {
            //the part that will be added to the SQL
            string SQLConditional = "";

            int counter = 0;

            foreach(string ID in setQuizzes)
            {
                if(counter == 0)
                {
                    SQLConditional += $"{ID}";
                }
                else
                {
                    SQLConditional += $" OR QuizID='{ID}'";
                }
            }

            //return final SQL
            return $"SELECT QuizID, Username, QuizType, QuizName FROM Quizzes WHERE QuizID='{SQLConditional}'";
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

        static void HandleTeacherQuizHistory(Socket client, ServerRequest clientRequest)
        {
            string username = clientRequest.requestParameters[0];

            SetQuizResponse setQuizResponse = new SetQuizResponse();

            string[] classes = GetTeacherClasses(username);

            setQuizResponse.quizIDs = GetSetQuizzes(classes);

            string SQL = TeacherQuizHistorySQL(setQuizResponse.quizIDs);

            QuizList list = new QuizList();

            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    command.CommandText = SQL;
                    var reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        //checking if the quiz ID is unique in the list
                        if (!(list.quizIDs.Contains(reader.GetInt32(0))))
                        {
                            list.quizIDs.Add(reader.GetInt32(0));
                        }

                        list.usernames.Add(reader.GetString(1));
                        list.quizContents.Add(reader.GetString(2));
                    }
                }

            }

            setQuizResponse.attemptList = list;

            TeacherQuizHistorySendToClient(client, setQuizResponse);
        }

        static string TeacherQuizHistorySQL(string[] quizzes)
        {
            //the part that will be added to the SQL
            string SQLConditional = "";

            int counter = 0;

            foreach (string quizID in quizzes)
            {
                if (counter == 0)
                {
                    SQLConditional += $"{quizID}";
                }
                else
                {
                    SQLConditional += $" OR QuizID='{quizID}'";
                }
            }

            return $"SELECT * FROM QuizAttempts WHERE QuizID='{SQLConditional}'";
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

        //Serialises teacher set quiz history response
        static string Serialise(SetQuizResponse setQuizResponse)
        {
            string json = JsonConvert.SerializeObject(setQuizResponse, Formatting.Indented);
            return json;
        }

        //Serialises ClassList list
        static string Serialise(ClassList list)
        {
            string json = JsonConvert.SerializeObject(list, Formatting.Indented);
            return json;
        }

        //Serialises MarkList list
        static string Serialise(MarkList list)
        {
            string json = JsonConvert.SerializeObject(list, Formatting.Indented);
            return json;
        }

        //Serialises class joining requests
        static string Serialise(ClassRequests list)
        {
            string json = JsonConvert.SerializeObject(list, Formatting.Indented);
            return json;
        }

        //serialises leaderboard
        static string Serialise(Leaderboards leaderboard)
        {
            string json = JsonConvert.SerializeObject(leaderboard, Formatting.Indented);
            return json;
        }

        //Serialises the list of class members
        static string Serialise(List<string> list)
        {
            string json = JsonConvert.SerializeObject(list, Formatting.Indented);
            return json;
        }


        //Sends the server response to the client for each type of request, although many of the types of request use the same one because they're similar

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

        static void LeaderboardSendToClient(Socket client, Leaderboards leaderboard)
        {
            string jsonResponse = Serialise(leaderboard);
            byte[] responseBytes = new byte[1000];
            responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
            client.Send(responseBytes);
            client.Close();
        }

        static void TeacherQuizHistorySendToClient(Socket client, SetQuizResponse setQuizResponse)
        {
            string jsonResponse = Serialise(setQuizResponse);
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

        static void MarkListSendToClient(Socket client, MarkList list)
        {
            string jsonResponse = Serialise(list);
            byte[] responseBytes = new byte[1000];
            responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
            client.Send(responseBytes);
            client.Close();
        }

        static void ClassMemberListSendToClient(Socket client, List<string> classMembers)
        {
            string jsonResponse = Serialise(classMembers);
            byte[] responseBytes = new byte[1000];
            responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
            client.Send(responseBytes);
            client.Close();
        }

        static void ClassRequestSendToClient(Socket client, ClassRequests list)
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
        static string CreateQuiz(string username, string quizType, string quizTitle, string quizContent,string publicState,string draftState, bool IDReturn)
        {
            //Escapes any apostrophies to prevent syntax errors/SQL injection
            quizTitle = quizTitle.Replace("'", "''");
            quizContent = quizContent.Replace("'", "''");

            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                string SQL = $"INSERT INTO Quizzes (Username,QuizType,QuizName, QuizContent, Public, Draft) VALUES ('{username}','{quizType}','{quizTitle}','{quizContent}','{publicState}','{draftState}')";

                //If the ID return is needed, then return the autoincremented ID in the SQL query
                if (IDReturn)
                {
                    SQL += "; SELECT last_insert_rowid();";
                }
                
                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    try
                    {
                        if (IDReturn)
                        {
                            string quizID = command.ExecuteScalar().ToString();
                            return quizID;
                        }
                        else
                        {
                            command.ExecuteNonQuery();
                            Console.WriteLine($"New quiz created, title {quizTitle}");
                            return "success";
                        }
                    }
                    catch
                    {
                        Console.WriteLine($"Quiz creation failed");
                        return "failed";
                    }
                }
            }
        }

        static string SetQuiz(string quizID, string classID, string username)
        {
            //checks that the teacher actually owns the class before they submit it
            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                string SQL = $"SELECT * FROM Classes WHERE ClassID='{classID}' AND Username='{username}'";

                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    var query = command.ExecuteScalar();

                    //If there are no rows (if the teacher doesn't own the class) it will just return as failed
                    if (query is null)
                    {
                        Console.WriteLine($"failed at the first hurdle! {username} {classID}");
                        return "failed";
                    }
                }
            }

            //actually sets the quiz
            using (SQLiteConnection databaseConnection = new SQLiteConnection("Data Source=TackleDatabase.db;Version=3;"))
            {
                databaseConnection.Open();

                string SQL = $"INSERT INTO SetQuizzes (QuizID, ClassID) VALUES ('{quizID}','{classID}')";

                using (SQLiteCommand command = new SQLiteCommand(SQL, databaseConnection))
                {
                    command.ExecuteNonQuery();
                    return "success";
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
        public List<string> quizContents;
        public List<string> topMarks;

        public QuizList()
        {
            //Initiates all of the lists
            quizIDs = new List<int>();
            usernames = new List<string>();
            quizType = new List<string>();
            quizNames = new List<string>();
            quizContents = new List<string>();
            topMarks = new List<string>();
        }
    }

    //List of class joining requests
    class ClassRequests
    {
        public List<string> classIDs;
        public List<string> usernames;

        public ClassRequests()
        {
            //Initiates all of the lists
            classIDs = new List<string>();
            usernames = new List<string>();
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

    //used in TeacherQuizHistory
    class SetQuizResponse
    {
        public string[] quizIDs;
        public QuizList attemptList;
    }

    //Used in MarkListViewModel
    class MarkList
    {
        public List<string> usernames;
        public List<string> quizIDs;

        public MarkList()
        {
            this.usernames = new List<string>();
            this.quizIDs = new List<string>();
        }
    }

    class Leaderboards
    {
        public List<string> usernames;
        public List<string> correct;

        public Leaderboards()
        {
            this.usernames = new List<string>();
            this.correct = new List<string>();
        }
    }
}
