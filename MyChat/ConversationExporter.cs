﻿namespace MyChat
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security;
    using System.Text;
    using MindLink.Recruitment.MyChat;
    using Newtonsoft.Json;
    using System.Linq;

    public enum FilterType
    {
        KEYWORD, SENDER_ID, BLACKLIST, HIDE_SENSITIVE_DATA, OBFUSCATE_IDS
    }

    /// <summary>
    /// Represents a conversation exporter that can read a conversation and write it out in JSON.
    /// </summary>
    public sealed class ConversationExporter
    {
        /// <summary>
        /// The application entry point.
        /// </summary>
        /// <param name="args">
        /// The command line arguments.
        /// </param>
        /// 

        public static Conversation conversation;


        static void Main(string[] args)
        {

            var configuration = new CommandLineArgumentParser().ParseCommandLineArguments(args);

            var conversationExporter = new ConversationExporter();

            conversationExporter.ExportConversation(configuration);

        }

        public void ExportConversation(ConversationExporterConfiguration configuration)
        {
            conversation = ReadConversation(configuration.inputFilePath);

            var action_list = configuration.GetFilterList();
            var modifier = new ConversationModifier(conversation);
            conversation = modifier.PerformActions(action_list, configuration);

            CalculateActivity(conversation);

            WriteConversation(configuration.outputFilePath);

            var activityList = CalculateActivity(conversation);
            WriteUserActivity(configuration.outputFilePath, activityList);

        }

        public List<UserInformation> CalculateActivity(Conversation conversation)
        {

            var activityList = new List<UserInformation>();

            var uniqueNames = conversation.messages.Select(c => c.senderId).Distinct().ToList();

            uniqueNames.ForEach(x => activityList.Add(new UserInformation(x)));

            foreach (Message m in conversation.messages)
            {
                for (int i = 0; i < activityList.Count; i++)
                {
                    if (activityList[i].userID.Equals(m.senderId)) { activityList[i].messageCount++; }

                }                

            }

            activityList = activityList.OrderByDescending(x => x.messageCount).ToList();


            return activityList;

        }

        public void WriteUserActivity(string outputFilePath, List<UserInformation> activityLict)
        {
            using (StreamWriter sw = File.AppendText(outputFilePath))
            {

                sw.WriteLine("\nMost active users: \n");

                foreach (UserInformation info in activityLict)
                {
                    sw.WriteLine("User ID: " + info.userID);

                    sw.WriteLine("Messages: " + info.messageCount);

                    sw.WriteLine("----------");

                }
            }


        }




        /// <summary>
        /// Exports the conversation at <paramref name="inputFilePath"/> as JSON to <paramref name="outputFilePath"/>.
        /// </summary>
        /// <param name="inputFilePath">
        /// The input file path.
        /// </param>
        /// <param name="outputFilePath">
        /// The output file path.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when a path is invalid.
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown when something bad happens.
        /// </exception>

        /// <summary>
        /// Helper method to read the conversation from <paramref name="inputFilePath"/>.
        /// </summary>
        /// <param name="inputFilePath">
        /// The input file path.
        /// </param>
        /// <returns>
        /// A <see cref="Conversation"/> model representing the conversation.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the input file could not be found.
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown when something else went wrong.
        /// </exception>
        public Conversation ReadConversation(string inputFilePath)
        {
            Console.WriteLine(inputFilePath);

            try
            {
                var reader = new StreamReader(new FileStream(inputFilePath, FileMode.Open, FileAccess.Read),
                    Encoding.ASCII);

                string conversationName = reader.ReadLine();

                var messages = new List<Message>();

                string line;

                while ((line = reader.ReadLine()) != null)
                {

                    var split = line.Split(' ');

                    if (LineValidator(split))
                    {
                        var sb = new StringBuilder();

                        var timestamp = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(split[0]));

                        var senderID = split[1];

                        split.Skip(2).Take(split.Length - 2).ToList<string>().ForEach(x => sb.Append(x + " "));

                        var content = sb.ToString().Trim();

                        messages.Add(new Message(timestamp, senderID, content));
                    }

                }

                messages.ForEach(m => Console.WriteLine(m.timestamp + " " + m.senderId + " " + m.content));

                return new Conversation(conversationName, messages);
            }
            catch (FileNotFoundException)
            {
                throw new ArgumentException("The file was not found.");
            }
            catch (IOException)
            {

                throw new Exception("Something went wrong in the IO.");
            }
        }


        public bool LineValidator(string[] split)
        
        {
            long number;

            bool checkDateTime = long.TryParse(split[0], out number);

            if(split.Length < Globals.MINIMUM_MESSAGE_LENGTH || !checkDateTime) { return false; }

            // If the message format is: "date user "  With the empty string at the end

            if (split.Length <= Globals.MINIMUM_MESSAGE_LENGTH+1)
            {
                foreach (string s in split)
                {
                    if (s.Equals(' ') || s.Equals(" ") || s.Equals("")) { return false; }
                }
            }


            return true;
        }




        /// <summary>
        /// Helper method to write the <paramref name="conversation"/> as JSON to <paramref name="outputFilePath"/>.
        /// </summary>
        /// <param name="conversation">
        /// The conversation.
        /// </param>
        /// <param name="outputFilePath">
        /// The output file path.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when there is a problem with the <paramref name="outputFilePath"/>.
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown when something else bad happens.
        /// </exception>
        public void WriteConversation(string outputFilePath)
        {
            try
            {
                File.WriteAllText(outputFilePath, string.Empty);

                var writer = new StreamWriter(new FileStream(outputFilePath, FileMode.Create, FileAccess.ReadWrite));

                var serialized = JsonConvert.SerializeObject(conversation, Formatting.Indented);
                
                writer.Write(serialized);

                writer.Flush();

                writer.Close();
            }
            catch (SecurityException)
            {
                throw new ArgumentException("No permission to file.");
            }
            catch (DirectoryNotFoundException)
            {
                throw new ArgumentException("Path invalid.");
            }
            catch (IOException)
            {
                throw new Exception("Something went wrong in the IO.");
            }
        }
    }
}
