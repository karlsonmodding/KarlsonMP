using Riptide;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace KarlsonMP
{
    public static class FileHandler
    {
        // handle server-side files (now only maps, but will allow for much more than that)
        static class CurrentFile
        {
            public static string FileName;
            public static bool DownloadingFile = false;
            public static List<byte> data;
            public static ushort CurrentPart;
            public static uint TotalSize;
            public static byte[] Hash;
        }
        public static Dictionary<string, (uint, byte[])> downloadQueue = new Dictionary<string, (uint, byte[])>();
        public static Dictionary<string, byte[]> downloadedFiles = new Dictionary<string, byte[]>();

        public static void ProcessDownloadQueue()
        {
            if (downloadQueue.Count == 0) return;
            var dl = downloadQueue.First();
            // check if file isn't already downloaded
            if (downloadedFiles.ContainsKey(dl.Key) && CheckHash(downloadedFiles[dl.Key]).SequenceEqual(dl.Value.Item2))
            {
                downloadQueue.Remove(dl.Key);
                OnFileReady(dl.Key);
                return;
            }
            if (CurrentFile.DownloadingFile) return; // we are already downloading a file
            downloadQueue.Remove(dl.Key);
            // schedule next file to download
            KMP_Console.Log($"Downloading file {dl.Key} ({dl.Value.Item1} bytes).");
            CurrentFile.FileName = dl.Key;
            CurrentFile.DownloadingFile = true;
            CurrentFile.data = new List<byte>();
            CurrentFile.TotalSize = dl.Value.Item1;
            CurrentFile.CurrentPart = 0;
            CurrentFile.Hash = dl.Value.Item2;
            ClientSend.FileData(dl.Key, 0);
        }

        static byte[] CheckHash(byte[] data)
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(data);
            }
        }

        public static void HandleFileRequest(string fileName, uint fileSize, byte[] hash)
        {
            if (downloadQueue.ContainsKey(fileName))
                downloadQueue.Remove(fileName);
            downloadQueue.Add(fileName, (fileSize, hash));
        }

        public static void HandleFilePart(byte[] fileData)
        {
            CurrentFile.data.AddRange(fileData);
            if (CurrentFile.data.Count == CurrentFile.TotalSize)
            {
                CurrentFile.DownloadingFile = false;
                var bytes = CurrentFile.data.ToArray();
                if (CheckHash(bytes).SequenceEqual(CurrentFile.Hash))
                {
                    KMP_Console.Log($"<color=green>Downloaded file {CurrentFile.FileName}.</color>");
                    if(downloadedFiles.ContainsKey(CurrentFile.FileName))
                        downloadedFiles[CurrentFile.FileName] = bytes;
                    else
                        downloadedFiles.Add(CurrentFile.FileName, bytes);
                    OnFileReady(CurrentFile.FileName);
                    return;
                }
                KMP_Console.Log($"<color=red>Downloaded file {CurrentFile.FileName} but hash is invalid.</color>");
                // invalid hash, re-schedule file
                if(downloadQueue.ContainsKey(CurrentFile.FileName))
                    downloadQueue.Remove(CurrentFile.FileName);
                downloadQueue.Add(CurrentFile.FileName, (CurrentFile.TotalSize, CurrentFile.Hash));
                return;
            }
            KMP_Console.Log($"Download progress <color=yellow>{CurrentFile.data.Count}/{CurrentFile.TotalSize}</color>.");
            // download next part
            ClientSend.FileData(CurrentFile.FileName, ++CurrentFile.CurrentPart);
        }

        // called when a file requested by the server is ready (downloaded or cached)
        public static void OnFileReady(string fileName)
        {
            // now only load map data
            if (fileName == ClientHandle.RequestedMap)
            {
                PlaytimeLogic.PrepareMapChange();
                KME_LevelPlayer.LoadLevel(fileName, downloadedFiles[fileName], ClientHandle.RequestedMapLegacy);
            }
        }
    }
}
