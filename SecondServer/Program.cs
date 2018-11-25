using System;
using System.Text;
using System.Threading;
using System.IO.MemoryMappedFiles;
using Util;
using static Client.Program;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static System.Console;

namespace SecondServer
{
    class Program
    {
        [DllImport("kernel32.dll")]
        public static extern void GlobalMemoryStatus(ref MEMORYSTATUS lpBuffer);
        [DllImport("msvcrt.dll", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr MemSet(IntPtr dest, int c, IntPtr count);
        public struct MEMORYSTATUS
        {
            /// <summary>
            /// Размер структуры
            /// </summary>
            public UInt32 dwLength;
            /// <summary>
            /// процент занятой памяти
            /// </summary>
            public UInt32 dwMemoryLoad;
            /// <summary>
            /// общее кол-во физической(оперативной) памяти
            /// </summary>
            public UInt32 dwTotalPhys;
            /// <summary>
            /// свободное кол-во физической(оперативной) памяти
            /// </summary>
            public UInt32 dwAvailPhys;
            /// <summary>
            /// предел памяти для системы или текущего процесса
            /// </summary>
            public UInt32 dwTotalPageFile;
            /// <summary>
            /// Максимальный объем памяти,который текущий процесс может передать в байтах.
            /// </summary>
            public UInt32 dwAvailPageFile;
            /// <summary>
            /// общее количество виртуальной памяти(файл подкачки)
            /// </summary>
            public UInt32 dwTotalVirtual;
            /// <summary>
            /// свободное количество виртуальной памяти(файл подкачки)
            /// </summary>
            public UInt32 dwAvailVirtual;
            /// <summary>
            /// Зарезервировано. Постоянно 0.
            /// </summary>
            public UInt32 dwAvailExtendedVirtual;
        }
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
            MEMORYSTATUS mem = new MEMORYSTATUS();
            GlobalMemoryStatus(ref mem);
            sb.Append("Total virtual memory: ").Append(mem.dwTotalVirtual).Append("\n").
                Append("Availiable virtual memory: ").Append(mem.dwAvailVirtual).Append("\n");
            return sb.ToString();
        }
        static void listenMessages()
        {
            Request req = new Request();
            int pos;
            while (isWorking)
            {
                pos = Block.getNotHandledPos(out req, defaultMemoryMappedFileSize, maxMessages, mapAccessor, secondServerTitle);
                if (pos != -1)
                {
                    unsafe
                    {
                        string r = new string(req.recepient);
                        if (r.Equals(secondServerTitle))
                        {
                            Block.free(pos, mapAccessor);
                            r = handleMessage(new string(req.message));
                            req.ownerFromString(secondServerTitle);
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
            catch (Exception ex)
            {
                WriteLine(ex.Message);
            }
            WriteLine("Done!");
            while (isWorking)
            {
                try
                {
                    printMenu();
                }
                catch (Exception ex)
                {
                    WriteLine(ex.Message);
                }
            }
        }
    }
}
