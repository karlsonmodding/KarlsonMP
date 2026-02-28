using ServerKMP;
using ServerKMP.GamemodeApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ServerKMP
{
    public static class FileUploader
    {
        // right now we only handle the map
        public static byte[] mapData = Array.Empty<byte>();
        public static byte[] GetMapPart(ushort part)
        {
            if (mapData.Length == 0)
                return Array.Empty<byte>();
            var dataLeft = mapData.Skip(part * 1024);
            if (dataLeft.Count() >= 1024)
                return dataLeft.Take(1024).ToArray();
            return dataLeft.ToArray();
        }

        public static byte[] CheckHash(byte[] data)
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(data);
            }
        }

        /// <summary>
        /// Broadcast upload request of the current map to all clients
        /// </summary>
        public static void SendMapUploadRequest()
        {
            new MessageServerToClient.MessageFileRequest(MapManager.currentMap!.name, (uint)mapData.Length, CheckHash(mapData)).SendToAll();
        }

        /// <summary>
        /// Send upload request of the current map to a client
        /// </summary>
        /// <param name="client">the client to send it to</param>
        public static void SendMapUploadRequest(ushort client)
        {
            new MessageServerToClient.MessageFileRequest(MapManager.currentMap!.name, (uint)mapData.Length, CheckHash(mapData)).Send(client);
        }
    }
}
