using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomansRconClient2
{
    class RconPacket
    {
        public int id;
        public int type;
        public string body;
        public byte[] rawBody;

        public const bool IS_NOT_LITTLE_ENDIAN = false;

        //Constructors
        public RconPacket()
        {

        }

        public RconPacket(int _id, RconPacketType _type, string _body)
        {
            id = _id;
            body = _body;
            type = (int)_type;
        }

        public RconPacket(int _id, int _type, string _body)
        {
            id = _id;
            body = _body;
            type = _type;
        }

        public byte[] ToBytes()
        {
            //Create a buffer to store data based on the data passed. The packet consists of 3 32-bit little endian signed ints, the ASCII encoded body (with terminated null) and a byte of padding.
            byte[] buf = new byte[4 + 4 + 4 + body.Length + 2]; //Three ints, two 0x00 bytes, plus body.
            using(MemoryStream ms = new MemoryStream(buf))
            {
                //Write size. This is the size of the remainder of the packet, not including this.
                WriteIntToStream(ms, buf.Length - 4);
                //Write ID
                WriteIntToStream(ms, id);
                //Write type
                WriteIntToStream(ms, type);
                //Now, write ASCII string. This isn't null terminated.
                WriteBytesToStream(ms, Encoding.ASCII.GetBytes(body));
                //Now, write two 0x00 bytes. One to terminate the string and another as padding.
                WriteByteToStream(ms, 0x00);
                WriteByteToStream(ms, 0x00);
            }
            //Return buffer
            return buf;
        }

        public static RconPacket ToPacket(byte[] data)
        {
            //Do the opposite as the above.
            RconPacket packet = new RconPacket();
            using (MemoryStream ms = new MemoryStream(data))
            {
                //First, read in the size.
                int length = ReadIntFromStream(ms);
                //Read id
                packet.id = ReadIntFromStream(ms);
                //Read type
                packet.type = ReadIntFromStream(ms);
                //Now, read body.
                int bodySize = length - 4 - 4 - 2; //Remove last two bytes of padding and the id and type.
                packet.rawBody = ReadBytesFromStream(ms, bodySize);
                packet.body = Encoding.ASCII.GetString(packet.rawBody);
                //Read last two bytes of padding
                ReadBytesFromStream(ms, 2);
            }
            return packet;
        }

        //Private helper functions

        private static void WriteIntToStream(MemoryStream ms, int input)
        {
            byte[] data = BitConverter.GetBytes(input);
            //Reverse this data based on the system's little endian status
            if (BitConverter.IsLittleEndian == IS_NOT_LITTLE_ENDIAN)
                Array.Reverse(data);
            //Now, write to stream.
            WriteBytesToStream(ms, data);
        }

        private static void WriteBytesToStream(MemoryStream ms, byte[] input)
        {
            ms.Write(input, 0, input.Length);
        }

        private static void WriteByteToStream(MemoryStream ms, byte input)
        {
            ms.Write(new byte[1] {input }, 0, 1);
        }

        private static byte[] ReadBytesFromStream(MemoryStream ms, int length)
        {
            byte[] buf = new byte[length];
            ms.Read(buf, 0, length);
            return buf;
        }

        private static int ReadIntFromStream(MemoryStream ms)
        {
            //Read in the bytes from the stream.
            byte[] buf = ReadBytesFromStream(ms, 4);
            //Now, reverse it if need be
            if (BitConverter.IsLittleEndian == IS_NOT_LITTLE_ENDIAN)
                Array.Reverse(buf);
            //Convert it and return
            return BitConverter.ToInt32(buf, 0);
        }
    }

    public enum RconPacketType
    {
        SERVERDATA_RESPONSE_VALUE = 0,
        SERVERDATA_EXECCOMMAND_OR_SERVERDATA_AUTH_RESPONSE = 2,
        SERVERDATA_AUTH = 3
    }
}
