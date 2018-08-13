using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RomansRconClient2
{
    public class RconClient
    {
        //This is used as a callback internally
        private delegate void InternalPacketCallback(RconPacket packet);
        private delegate void GotPacketCallback(List<RconPacket> packets);
        public delegate void ReadyCallback(RconClient context, bool authGood);
        public delegate void GotStringCallback(string response);

        //Variables
        private Socket sock;
        private byte[] receiveBuffer = new byte[4]; //4096 is the max size a packet will ever be, but we're waiting for the integer size to be recieved. 
        private int currentId; //This is incremented for each packet sent. It's used to know which packet came back first.

        private IAsyncResult pendingAsync = null;

        //This dictonary holds callbacks for each message ID.
        Dictionary<int, InternalPacketCallback> internalCallbacks = new Dictionary<int, InternalPacketCallback>();

        //Constructors
        public RconClient(IPEndPoint endpoint, string password, ReadyCallback onReady)
        {
            //First, we're going to make a socket to the server.
            sock = new Socket(SocketType.Stream, ProtocolType.Tcp);
            //Connect
            sock.Connect(endpoint);
            //Begin waiting for a packet.
            PrivateBeginWaitingForPacket();
            //Authenticate with the server
            PrivateGetPackets(new RconPacket(0, RconPacketType.SERVERDATA_AUTH, password),new GotPacketCallback((List<RconPacket> packets) =>
            {
                onReady(this,true);
            }));
        }

        public void Dispose()
        {
            
            sock.Disconnect(true);
            if(pendingAsync!=null)
            {
                sock.EndReceive(pendingAsync);
            }
            
            sock.Close();
            sock.Dispose();
            sock = null;
            
        }

        //Public functions
        public void BeginGetResponse(string cmd, GotStringCallback callback)
        {
            PrivateGetPackets(new RconPacket(0, RconPacketType.SERVERDATA_EXECCOMMAND_OR_SERVERDATA_AUTH_RESPONSE, cmd), new GotPacketCallback((List<RconPacket> packets) =>
            {
                //Add all of the packets together
                string response = "";
                for(int i = 0; i<packets.Count; i++)
                {
                    response += packets[i].body;
                }
                callback(response);
            }));
        }

        public string GetResponse(string cmd)
        {
            string response = "";
            bool hasResponse = false;
            BeginGetResponse(cmd, new GotStringCallback((string result) =>
            {
                response = result;
                hasResponse = true;
            }));
            while (hasResponse == false) ;
            return response;
        }

        //Private helper methods
        private void PrivateGetPackets(RconPacket input, GotPacketCallback callback)
        {
            //Get ID and incrememnt it
            int id = currentId++;
            int testId = currentId++;//This id will be used to check when we're done.
            input.id = id;
            //Register a callback for this and the next one.
            int packetsGot = 0; //How many packets were recievied for a single ID.
            List<RconPacket> returnedPackets = new List<RconPacket>();
            var innerCallback = (new InternalPacketCallback((RconPacket p) =>
            {
                //When a packet is received, this is called.
                //A request to the server will be sent using the same ID. When that packet is recieved, we know we are done. This is for getting multiple packets for one request if it is too long.
                RconPacketType type = (RconPacketType)p.type;
                bool isEnd = testId == p.id;
                //Check if the raw contents match the end of packet results




                if (packetsGot == 0)
                {
                    //Send a request to check if we are done.
                    sock.Send(new RconPacket(testId, RconPacketType.SERVERDATA_RESPONSE_VALUE, "").ToBytes());
                }

                if (isEnd)
                {
                    //This is the end. Return all of our packets.
                    callback(returnedPackets);
                    //Also remove this callback from the dictonary to save ram
                    internalCallbacks.Remove(id);
                    internalCallbacks.Remove(testId);
                }
                else
                {
                    //Add this because it's not the ending packet.
                    returnedPackets.Add(p);
                }

                packetsGot++;
            }));
            internalCallbacks.Add(id, innerCallback);
            internalCallbacks.Add(testId, innerCallback);
            //Send packet to the server.
            byte[] data = input.ToBytes();
            //Send
            sock.Send(data);
        }

        //Reading private functions
        private static int ReadIntFromStream(byte[] buf)
        {
            //Now, reverse it if need be
            if (BitConverter.IsLittleEndian == RconPacket.IS_NOT_LITTLE_ENDIAN)
                Array.Reverse(buf);
            //Convert it and return
            return BitConverter.ToInt32(buf, 0);
        }


        //Functions that are used to wait for incoming responses.
        private void PrivateBeginWaitingForPacket()
        {
            pendingAsync = sock.BeginReceive(receiveBuffer, 0, 4, SocketFlags.None, new AsyncCallback(PrivateGotPacket), null);
        }

        private void PrivateGotPacket(System.IAsyncResult result)
        {
            //Check if disposed
            if (sock == null)
                return;
            //Get the data out of this.
            sock.EndReceive(result);
            pendingAsync = null;
            //We'll now read in the size of the packet based on the "receiveBuffer" int sent.
            int size = ReadIntFromStream(receiveBuffer);
            //We'll now read in the remainder of the packet.
            byte[] buffer = new byte[size + 4];
            //Copy the current receiveBuffer to this buffer. This'll allow us to have the length.
            receiveBuffer.CopyTo(buffer, 0);
            //Now, read in the remainder of the packet from the socket.
            sock.Receive(buffer, 4, size, SocketFlags.None);
            //We'll now begin listening again.
            PrivateBeginWaitingForPacket();
            //Now, we'll convert this to a real packet we can understand.
            RconPacket packet = null;
            try
            {
                packet = RconPacket.ToPacket(buffer);
                //If the callback list contains this, call back. Ignore Ark's "Keep Alive" packets.
                if (internalCallbacks.ContainsKey(packet.id) && packet.body != "Keep Alive")
                {
                    internalCallbacks[packet.id](packet);
                }
            }
            catch (Exception ex)
            {
                //There was an error.
                throw ex;
            }
        }
    }
}
