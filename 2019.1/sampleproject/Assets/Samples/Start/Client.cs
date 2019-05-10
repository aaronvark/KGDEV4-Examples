using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine.UI;

public class Client : MonoBehaviour
{
    public UdpNetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public Text debugTextField, serverIP;
    public bool Done;
    private NetworkPipeline reliableUdpPipe;
    //private NetworkPipeline unrealiableSimulatorPipe;
    string lastIP = "";

    void Start () { 
        
        m_Driver = new UdpNetworkDriver(new ReliableUtility.Parameters { WindowSize = 32 } );
        
        reliableUdpPipe = m_Driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));//, typeof(SimulatorPipelineStage));
        //unrealiableSimulatorPipe = m_Driver.CreatePipeline(typeof(SimulatorPipelineStage));

        m_Connection = default(NetworkConnection);
    }

    public void ConnectToServer() {
        debugTextField.text = "";

        string ip = serverIP.text;
        if ( !m_Connection.IsCreated || lastIP != ip ) {
            if ( m_Connection.IsCreated ) {
                m_Connection.Disconnect(m_Driver);
                m_Connection = default(NetworkConnection);
            }

            lastIP = ip;
            if ( ip == "" ) ip = "127.0.0.1";
            var endpoint = NetworkEndPoint.Parse(ip, 9000);
            m_Connection = m_Driver.Connect(endpoint);
        }
        else {
            //send message for new hello
            using ( var writer = PacketFunctions.WritePacket(GameEvent.HELLO_SERVER)) {
                //m_Connection.Send(m_Driver, writer);
                m_Connection.Send(m_Driver, reliableUdpPipe, writer); 
            }
        }
    }

    public void SendChatMessage() {
        
    }

    public void OnDestroy() { 
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);

        m_Driver.Dispose();
    }

    void Update() { 
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            if (!Done)
                Debug.Log("Something went wrong during connect");
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        while ((cmd = m_Connection.PopEvent(m_Driver, out stream)) != 
            NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                Debug.Log("We are now connected to the server");

                using ( var writer = PacketFunctions.WritePacket(GameEvent.HELLO_SERVER)) {
                    //m_Connection.Send(m_Driver, writer);
                    m_Connection.Send(m_Driver, reliableUdpPipe, writer); 
                }
                
                /*
                var value = 1;
                using (var writer = new DataStreamWriter(4, Allocator.Temp))
                {
                    writer.Write(value);
                    m_Connection.Send(m_Driver, writer);
                }
                */
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                //read received packet
                DataStreamReader.Context readerCtx = default(DataStreamReader.Context);
                PacketFunctions.ReadPacket(stream, ref readerCtx, PushToScreen);

                /*
                // var readerCtx = default(DataStreamReader.Context);
                uint value = stream.ReadUInt(ref readerCtx);
                Debug.Log("Got the value = " + value + " back from the server");
                Done = true;
                m_Connection.Disconnect(m_Driver);
                m_Connection = default(NetworkConnection);
                */
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("Client got disconnected from server");
                m_Connection = default(NetworkConnection);
            }
        }
    }

    void PushToScreen( object[] data ) {
        uint value = (uint)data[0];
        debugTextField.text += ", "+value.ToString();
        Debug.Log((string)data[1]);
    }
}
