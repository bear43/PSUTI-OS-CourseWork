using System;
using static System.Console;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Util
{
    /// <summary>
    /// Request structure
    /// </summary>
    public unsafe struct Request
    {
        private const int OWNER_SIZE = 32;
        private const int MESSAGE_SIZE = 512;
        private const int RECEPIENT_SIZE = 32;
        /// <summary>
        /// Who sended the message
        /// </summary>
        public fixed char owner[OWNER_SIZE];
        /// <summary>
        /// The message
        /// </summary>
        public fixed char message[MESSAGE_SIZE];
        /// <summary>
        /// Who must to recive message
        /// </summary>
        public fixed char recepient[RECEPIENT_SIZE];
        /// <summary>
        /// Gets string from char*
        /// </summary>
        /// <param name="pchar">Pointer to str(Char array)</param>
        /// <returns>String representation of char array</returns>
        public String fromPCharToString(char* pchar)
        {
            return new string(pchar);
        }
        /// <summary>
        /// Copy some string to memory array
        /// </summary>
        /// <param name="dst">Memory array to copy(destination)</param>
        /// <param name="str">Source string to copy</param>
        public void fromStringToPChar(char* dst, String str)
        {
            Marshal.Copy(str.ToCharArray(), 0, (IntPtr)dst, str.Length);
        }
        /// <summary>
        /// From String to char*
        /// </summary>
        /// <param name="owner">Data for field owner</param>
        public void ownerFromString(String owner)
        {
            clearOwner();
            fixed (char* own = this.owner)
                Marshal.Copy(owner.ToCharArray(), 0, (IntPtr)own, owner.Length);
        }
        public String getOwner()
        {
            fixed(char* own = owner)
                return new string(own);
        }
        public void messageFromString(String message)
        {
            clearMessage();
            fixed (char* msg = this.message)
                Marshal.Copy(message.ToCharArray(), 0, (IntPtr)msg, message.Length);
        }
        public String getMessage()
        {
            fixed (char* msg = message)
                return new string(msg);
        }
        public void recepientFromString(String recepient)
        {
            clearRecepient();
            fixed (char* rcp = this.recepient)
                Marshal.Copy(recepient.ToCharArray(), 0, (IntPtr)rcp, recepient.Length);
        }
        public String getRecepient()
        {
            fixed (char* rcp = recepient)
                return new string(rcp);
        }
        /// <summary>
        /// Clear some field by copying empty array of chars to dst
        /// </summary>
        /// <param name="field">Destination</param>
        /// <param name="size">Cout of elem(size of char array)</param>
        public static void clearField(char* field, int size)
        {
            Marshal.Copy(new char[size], 0, (IntPtr)field, size);
        }
        public void clearOwner()
        {
            fixed(char* owner = this.owner)
                clearField(owner, OWNER_SIZE);
        }
        public void clearMessage()
        {
            fixed (char* msg = message)
                clearField(msg, MESSAGE_SIZE);
        }
        public void clearRecepient()
        {
            fixed (char* recepient = this.recepient)
                clearField(recepient, RECEPIENT_SIZE);
        }
    }

    public class Block
    {
        /// <summary>
        /// Signature to free block. It must be contained in Request.owner
        /// </summary>
        public static String freeBlockSign = "FrEE";

        /// <summary>
        /// Get area to memory mapped file
        /// </summary>
        /// <param name="mappedFile">MemoryMappedFile object</param>
        /// <param name="maxMessages">How many messages can be saved in queue</param>
        /// <param name="mapAccessor">MemoryMappedFileViewAccessor object</param>
        /// <param name="defaultMemoryMappedFileSize">Size of memory mapped block</param>
        /// <param name="mapFile">File being get memory mapped</param>
        /// <param name="defaultMemoryTitle">Memory block name</param>
        public static void getMemoryMappedArea(ref MemoryMappedFile mappedFile, ref int maxMessages, ref MemoryMappedViewAccessor mapAccessor, ref int defaultMemoryMappedFileSize, ref String mapFile, ref String defaultMemoryTitle)
        {
            int structSize = Marshal.SizeOf(new Request());
            defaultMemoryMappedFileSize = structSize*maxMessages;//Now equals to real size of memory mapped file
            try
            {
                if (File.Exists(mapFile))
                {
                    FileInfo fi = new FileInfo(mapFile);
                    if (fi.Length >= defaultMemoryMappedFileSize)
                    {
                        mappedFile = MemoryMappedFile.CreateFromFile(mapFile, FileMode.Open, defaultMemoryTitle);
                        mapAccessor = mappedFile.CreateViewAccessor();
                        WriteLine("Create memory mapped area from file: " + mapFile);
                        maxMessages = (int)(fi.Length / structSize);
                        defaultMemoryMappedFileSize = maxMessages * structSize;
                        WriteLine("File size: " + fi.Length);
                    }
                    else
                    {
                        WriteLine("Size of map file \"" + mapFile + "\" doesn't allow to share memory by its!\nSharing mapped file in memory block");
                        mappedFile = MemoryMappedFile.CreateOrOpen(defaultMemoryTitle, defaultMemoryMappedFileSize);
                        mapAccessor = mappedFile.CreateViewAccessor();
                    }
                }
                else
                {
                    WriteLine("Mapped file doesn't exist! Sharing memory block");
                    mappedFile = MemoryMappedFile.CreateOrOpen(defaultMemoryTitle, defaultMemoryMappedFileSize);
                    mapAccessor = mappedFile.CreateViewAccessor();
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex.Message);
                mappedFile = MemoryMappedFile.CreateNew(defaultMemoryTitle, defaultMemoryMappedFileSize);
                mapAccessor = mappedFile.CreateViewAccessor();
            }
            WriteLine("Block size: " + defaultMemoryMappedFileSize/maxMessages);
            for (int i = 0; i < defaultMemoryMappedFileSize; i += defaultMemoryMappedFileSize/maxMessages)
            {
                free(i, mapAccessor);
                WriteLine("Cleared " + ((i/ (defaultMemoryMappedFileSize / maxMessages)) + 1) + " block");
            }
            WriteLine("Allowed to save " + maxMessages + " messages");
        }
        /// <summary>
        /// Fill req with free block data with getting position by getFreeBlock
        /// </summary>
        /// <param name="req">Object to fill</param>
        public static void getFreeRequest(out Request req, int defaultMemoryMappedFileSize, int maxMessages, MemoryMappedViewAccessor mapAccessor)
        {
            mapAccessor.Read(getFree(defaultMemoryMappedFileSize, maxMessages, mapAccessor), out req);
        }
        /// <summary>
        /// Get position of free block in memory mapped area
        /// </summary>
        /// <returns></returns>
        public static unsafe int getFree(int defaultMemoryMappedFileSize, int maxMessages, MemoryMappedViewAccessor mapAccessor)
        {
            int structSize = defaultMemoryMappedFileSize / maxMessages;
            Request req;
            String own;
            for (int pos = 0; pos < defaultMemoryMappedFileSize; pos += structSize)
            {
                mapAccessor.Read(pos, out req);
                own = new string(req.owner);
                if (own.Equals(freeBlockSign))
                    return pos;
            }
            return -1;
        }
        public static unsafe int getNotHandledPos(out Request block, int defaultMemoryMappedFileSize, int maxMessages, MemoryMappedViewAccessor mapAccessor, String recepient)
        {
            int structSize = defaultMemoryMappedFileSize / maxMessages;
            String own;
            for (int pos = 0; pos < defaultMemoryMappedFileSize; pos += structSize)
            {
                mapAccessor.Read(pos, out block);
                fixed(char* recep = block.recepient)
                    own = new string(recep);
                if (own.Equals(recepient))
                    return pos;
            }
            block = new Request();
            return -1;
        }
        /// <summary>
        /// Fills memory mapped block
        /// </summary>
        /// <param name="position">pos to fill</param>
        /// <param name="req">data to fill</param>
        public static unsafe void fill(int position, Request req, MemoryMappedViewAccessor mapAccessor)
        {
            if(position !=-1) mapAccessor.Write(position, ref req);
        }
        /// <summary>
        /// Free block in memory mapped file
        /// </summary>
        /// <param name="block">Free some block</param>
        public static unsafe void free(int pos, MemoryMappedViewAccessor mapAccessor)
        {
            Request block = new Request();
            freeEx(ref block);
            if(pos != -1) mapAccessor.Write(pos, ref block);
        }
        /// <summary>
        /// Freeing block, but not in memory mapped
        /// </summary>
        /// <param name="block"></param>
        public static unsafe void freeEx(ref Request block)
        {
            fixed (char* recep = block.recepient, owner = block.owner, msg = block.message)
            {
                block.clearRecepient();
                block.clearOwner();
                block.clearMessage();
                Marshal.Copy(freeBlockSign.ToCharArray(), 0, (IntPtr)recep, freeBlockSign.Length);
                Marshal.Copy(freeBlockSign.ToCharArray(), 0, (IntPtr)owner, freeBlockSign.Length);
            }
        }
        /// <summary>
        /// Get existing memory mapped file by its title
        /// </summary>
        /// <param name="mappedFile">Object to fill</param>
        /// <param name="mapAccessor">Object to fill</param>
        /// <param name="mapTitle">Title of memory block</param>
        /// <returns></returns>
        public static bool getExistedMemoryMap(ref MemoryMappedFile mappedFile, ref MemoryMappedViewAccessor mapAccessor, String mapTitle)
        {
            mappedFile = null;
            try
            {
                mappedFile = MemoryMappedFile.OpenExisting(mapTitle);
                if (mappedFile != null)
                {
                    mapAccessor = mappedFile.CreateViewAccessor();
                    return true;
                }
            }
            catch(Exception)
            {
                return false;
            }
            return false;
        }
        /// <summary>
        /// Clearing message queue
        /// </summary>
        /// <param name="mapAccessor">Access to memory block</param>
        /// <param name="totalBytes">Total all of blocks size</param>
        /// <param name="blockSize">One block size</param>
        public static void clearQueue(MemoryMappedViewAccessor mapAccessor, int totalBytes, int blockSize)
        {
            for(int i = 0; i < totalBytes; i += blockSize)
                free(i, mapAccessor);
        }
    }

}