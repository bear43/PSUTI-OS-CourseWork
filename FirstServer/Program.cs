using System;
using System.Text;
using System.Threading;
using System.IO.MemoryMappedFiles;
using Util;
using static Client.Program;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static System.Console;
using System.Diagnostics;

namespace FirstServer
{
    class Program
    {
        static MemoryMappedFile mappedFile = null;
        static MemoryMappedViewAccessor mapAccessor = null;
        static int maxMessages = 8;
        static int defaultMemoryMappedFileSize = Marshal.SizeOf(new Request()) * maxMessages;
        static String mapFile;
        static Thread listenThread = new Thread(new ThreadStart(listenMessages));
        static bool isWorking = true;
        static String handleMessage(String message)
        {
            StringBuilder sb = new StringBuilder();
            PerformanceCounter pc = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            PerformanceCounter pc1 = new PerformanceCounter("Processor", "% Processor Time", "0");
            PerformanceCounter pc2 = new PerformanceCounter("Processor", "% Processor Time", "1");
            PerformanceCounter pc3 = new PerformanceCounter("Processor", "% Processor Time", "2");
            PerformanceCounter pc4 = new PerformanceCounter("Processor", "% Processor Time", "3");
            pc.NextValue();
            pc1.NextValue();
            pc2.NextValue();
            pc3.NextValue();
            pc4.NextValue();
            Thread.Sleep(1000);
            sb.Append("Total CPU usage: " + pc.NextValue().ToString() + "%\n");
            sb.Append("Total #1 core usage: " + pc1.NextValue().ToString() + "%\n");
            sb.Append("Total #2 core usage: " + pc2.NextValue().ToString() + "%\n");
            sb.Append("Total #3 core usage: " + pc3.NextValue().ToString() + "%\n");
            sb.Append("Total #4 core usage: " + pc4.NextValue().ToString() + "%\n");
            return sb.ToString();
        }
        static void listenMessages()
        {
            Request req = new Request();
            int pos;
            while (isWorking)
            {
                pos = Block.getNotHandledPos(out req, defaultMemoryMappedFileSize, maxMessages, mapAccessor, firstServerTitle);
                if (pos != -1)
                {
                    unsafe
                    {
                        string r = new string(req.recepient);
                        if (r.Equals(firstServerTitle))
                        {
                            Block.free(pos, mapAccessor);
                            r = handleMessage(new string(req.message));
                            req.ownerFromString(firstServerTitle);
                            req.messageFromString(r);
                            req.recepientFromString(clientTitle);
                            Block.fill(Block.getFree(defaultMemoryMappedFileSize, maxMessages, mapAccessor), req, mapAccessor);
                            Out.WriteLineAsync("Sended some data");
                        }
                    }
                }
            }
        }
        static void printMenu()
        {
            WriteLine("0. Exit");
            WriteLine("Your choice: ");
            switch (Int32.Parse(ReadLine()))
            {
                case 0:
                    isWorking = false;
                    break;
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            if (!Block.getExistedMemoryMap(ref mappedFile, ref mapAccessor, defaultMemoryTitle))
            {
                WriteLine("Existing memory mapped block not founded");
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = "Choose file to make memory mapped snapshot";
                ofd.SupportMultiDottedExtensions = false;
                ofd.Multiselect = false;
                if (ofd.ShowDialog() == DialogResult.OK)
                    mapFile = ofd.FileName;
                else
                    WriteLine("You cancelled to choose a file");
                Block.getMemoryMappedArea(ref mappedFile, ref maxMessages, ref mapAccessor, ref defaultMemoryMappedFileSize, ref mapFile, ref defaultMemoryTitle);
            }
            else
            {
                WriteLine("Founded existing memory mapped block");
            }
            WriteLine("Starting to listen messages...");
            try
            {
                listenThread.IsBackground = true;
                listenThread.Start();
            }
            catch(Exception ex)
            {
                WriteLine(ex.Message);
            }
            WriteLine("Done!");
            while(isWorking)
            {
                try
                {
                    printMenu();
                }
                catch(Exception ex)
                {
                    WriteLine(ex.Message);
                }
            }
        }
    }
}
