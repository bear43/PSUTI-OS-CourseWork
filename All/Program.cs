using System;
using System.Collections.Generic;
using static System.Console;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using static Util.Block;
using Util;
//d
namespace Client
{
    public class Program
    {
        public const String logFileName = "log.txt";
        /// <summary>
        /// Time in seconds to waiting between sending requests
        /// </summary>
        const int REFRESH_RATE = 4;
        public static String clientTitle = "cliEnt";
        public static String defaultRequest = "defReq";
        static int maxMessages = 100;
        /// <summary>
        /// For start equals the multiplier. Formula: max messages * structure size
        /// </summary>
        static int defaultMemoryMappedFileSize = maxMessages * Marshal.SizeOf(new Request());
        /// <summary>
        /// Default name to memory mapped area
        /// </summary>
        public static String defaultMemoryTitle = "xchgR";
        /// <summary>
        /// Path to map file
        /// </summary>
        static String mapFile;
        /// <summary>
        /// Path to process log file
        /// </summary>
        static String procFileLog;
        /// <summary>
        /// File in memory
        /// </summary>
        static MemoryMappedFile mappedFile = null;
        /// <summary>
        /// Accessor to memory
        /// </summary>
        static MemoryMappedViewAccessor mapAccessor = null;
        /// <summary>
        /// Sleep time between checking messages
        /// </summary>
        static int SLEEP_TIME = 150;
        /// <summary>
        /// Servers' vars
        /// </summary>
        const String firstServerName = "first.exe";
        const String secondServerName = "second.exe";
        static Process firstServer = null;
        static Process secondServer = null;
        public static String firstServerTitle = "firstBOSS";
        public static String secondServerTitle = "secondSufferer";
        /// <summary>
        /// Listening thread - a thread which listening messages out of turn
        /// </summary>
        static Thread listeningThread = null;
        /// <summary>
        /// Saves new processes
        /// </summary>
        static Thread procThread = null;
        /// <summary>
        /// Send requests by timer
        /// </summary>
        static Thread sendingThread = null;
        /// <summary>
        /// Indicates a work state to the app
        /// </summary>
        static bool isWorking = true;
        static bool isListening = false;
        static bool isSending = false;
        /// <summary>
        /// Printing menu out
        /// </summary>
        static void printMenu()
        {
            WriteLine("1. Exit");
            WriteLine("2. Run servers");
            WriteLine("3. Get data from servers");
            WriteLine("4. Close servers");
            WriteLine("5. Run an independent listening thread");
            WriteLine("6. Run an independent sending thread");
            WriteLine("7. Stop the listening thread");
            WriteLine("8. Stop the sending thread");
            WriteLine("9. Clear the messages queue");
        }
        /// <summary>
        /// Closing servers
        /// </summary>
        static void closeServers()
        {
            if (firstServer != null && !firstServer.HasExited) firstServer.Kill();
            if (secondServer != null && !secondServer.HasExited) secondServer.Kill();
        }
        /// <summary>
        /// Fill req with free block data with getting position by getFreeBlock
        /// </summary>
        /// <param name="req">Object to fill</param>
        static void getFreeRequest(out Request req)
        {
            mapAccessor.Read(getFree(defaultMemoryMappedFileSize, maxMessages, mapAccessor), out req);
        }
        /// <summary>
        /// Reciving requests from servers
        /// </summary>
        static async void listenMessages(Object isListen)
        {
            while (isWorking && isListening)
            {
                Thread.Sleep(SLEEP_TIME);
                Request req;
                int pos = getNotHandledPos(out req, defaultMemoryMappedFileSize, maxMessages, mapAccessor, clientTitle);
                if (pos != -1)
                {
                    String str;
                    unsafe
                    {
                        str = new String(req.message);
                    }
                    await Out.WriteLineAsync(str);
                    File.AppendAllText(logFileName, str);
                    free(pos, mapAccessor);
                }
                if (!(bool)isListen) break; 
            }
        }
        /// <summary>
        /// Send default request to servers
        /// </summary>
        static unsafe void sendRequest(Object loop)
        {
            Request req = new Request();
            int pos = -1;
            while (isWorking && isSending)
            {
                req.clearMessage();
                req.clearOwner();
                req.clearRecepient();
                pos = getFree(defaultMemoryMappedFileSize, maxMessages, mapAccessor);
                Marshal.Copy(defaultRequest.ToCharArray(), 0, (IntPtr)req.message, defaultRequest.Length);
                Marshal.Copy(clientTitle.ToCharArray(), 0, (IntPtr)req.owner, clientTitle.Length);
                Marshal.Copy(firstServerTitle.ToCharArray(), 0, (IntPtr)req.recepient, firstServerTitle.Length);
                fill(pos, req, mapAccessor);
                pos = getFree(defaultMemoryMappedFileSize, maxMessages, mapAccessor);
                req.clearRecepient();
                Marshal.Copy(secondServerTitle.ToCharArray(), 0, (IntPtr)req.recepient, secondServerTitle.Length);
                fill(pos, req, mapAccessor);
                if (!(bool)loop) break;
                Thread.Sleep(REFRESH_RATE*1000);
            }
        }
        /// <summary>
        /// Handling user's choice
        /// </summary>
        /// <param name="choice">Menu item choosed by user</param>
        static void handleChoice(int choice)
        {
            try
            {
                switch (choice)
                {
                    case 1:
                        isWorking = false;
                        closeServers();
                        break;
                    case 2:
                        if (firstServer == null || firstServer.HasExited) firstServer = Process.Start(firstServerName);
                        if (secondServer == null || secondServer.HasExited) secondServer = Process.Start(secondServerName);
                        break;
                    case 3:
                        sendRequest(false);
                        break;
                    case 4:
                        closeServers();
                        break;
                    case 5:
                        if (listeningThread == null || !listeningThread.ThreadState.Equals(System.Threading.ThreadState.Running | System.Threading.ThreadState.Background))
                        {
                            listeningThread = new Thread(new ParameterizedThreadStart(listenMessages));
                            listeningThread.IsBackground = true;
                            isListening = true;
                            listeningThread.Start(true);
                        }
                        break;
                    case 6:
                        if (sendingThread == null || !sendingThread.ThreadState.Equals(System.Threading.ThreadState.Running | System.Threading.ThreadState.Background))
                        {
                            sendingThread = new Thread(new ParameterizedThreadStart(sendRequest));
                            sendingThread.IsBackground = true;
                            isSending = true;
                            sendingThread.Start(true);
                        }
                        break;
                    case 7:
                        if (isListening)
                            isListening = false;
                        else
                            WriteLine("Thread is not run!");
                        break;
                    case 8:
                        if (isSending)
                            isSending = false;
                        else
                            WriteLine("Thread is not run!");
                        break;
                    case 9:
                        if (mapAccessor != null)
                        {
                            clearQueue(mapAccessor, defaultMemoryMappedFileSize, defaultMemoryMappedFileSize / maxMessages);
                            WriteLine("Queue has been cleared");
                        }
                        break;
                }
            }
            catch(Exception ex)
            {
                WriteLine("Catched some exception while handling user's choice\n{0}", ex.Message);
            }
        }
        /// <summary>
        /// Getting paths to files such procLog & memory mapped file
        /// </summary>
        static void getPaths()
        {
            try
            {
                SaveFileDialog sfd = new SaveFileDialog();
                OpenFileDialog ofd = new OpenFileDialog();
                sfd.Title = "Choose file to save process log";
                sfd.AddExtension = true;
                sfd.DefaultExt = "txt";
                sfd.Filter = "*.txt|";
                sfd.SupportMultiDottedExtensions = false;
                sfd.ShowDialog();
                procFileLog = sfd.FileName;
                File.Create(procFileLog).Close();
                ofd.Title = "Choose file to make memory mapped snapshot";
                ofd.SupportMultiDottedExtensions = false;
                ofd.Multiselect = false;
                ofd.ShowDialog();
                mapFile = ofd.FileName;
            }
            catch(Exception)
            {
                WriteLine("Some causes while get directories! Closing...");
                Environment.Exit(1);
            }
        }           
        /// <summary>
        /// Refresh processes log
        /// </summary>
        static void writeProcLog()
        {
            List<Process> procList = new List<Process>(Process.GetProcesses());
            bool contains;
            File.Delete(procFileLog);
            StreamWriter sw = new StreamWriter(procFileLog);
            while (isWorking)
            {
                try
                {
                    foreach (Process proc in Process.GetProcesses())
                    {
                        contains = false;
                        for (int i = 0; i < procList.Count; i++)
                            if (procList[i].Id == proc.Id)
                            {
                                contains = true;
                                break;
                            }
                        if (!contains)
                        {
                            sw.WriteLine(String.Format("Process: {0}. StartTime: {1}", proc.ProcessName, proc.StartTime));
                            sw.Flush();
                            procList.Add(proc);
                        }
                    }
                }
                catch (Exception)
                {

                }
                Thread.Sleep(SLEEP_TIME);
            }
            sw.Close();
        }

        [STAThread]
        public static void Main(string[] args)
        {
            getPaths();
            try
            {
                getMemoryMappedArea(ref mappedFile, ref maxMessages, ref mapAccessor, ref defaultMemoryMappedFileSize, ref mapFile, ref defaultMemoryTitle);
            }
            catch(Exception ex)
            {
                WriteLine(ex.Message);
                Thread.Sleep(5000);
                isWorking = false;
            }
            procThread = new Thread(new ThreadStart(writeProcLog));
            procThread.IsBackground = true;
            procThread.Start();
            int choice = 0;
            while (isWorking)
            {
                printMenu();
                WriteLine("Your choice: ");
                try
                {
                    choice = Int32.Parse(ReadLine());
                    if (choice < 1 || choice > 9) throw new Exception("Wrong input data!");
                    handleChoice(choice);
                }
                catch(Exception ex)
                {
                    WriteLine(ex.Message);
                }
            }
        }
    }
}
