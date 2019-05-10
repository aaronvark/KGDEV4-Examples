using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;

public enum GameEvent {
    HELLO_SERVER,
    HELLO_CLIENT
}

public delegate void ReadPacketFunction( DataStreamReader stream, ref DataStreamReader.Context context, ref List<object> data );
public delegate void ReadCallback( object[] data );
public delegate DataStreamWriter WritePacketFunction();

//WORK IN PROGRESS
//BIG TODO: Write Function request data callbacks
public static class PacketFunctions
{
    static uint sendValue = 0;

    public static Dictionary<GameEvent, ReadPacketFunction> readFunctions = new Dictionary<GameEvent, ReadPacketFunction> {
        { GameEvent.HELLO_CLIENT, ReadHelloClientPacket },
        { GameEvent.HELLO_SERVER, ReadHelloServerPacket },
    };
    
    public static Dictionary<GameEvent, WritePacketFunction> writeFunctions = new Dictionary<GameEvent, WritePacketFunction> {
        { GameEvent.HELLO_CLIENT, WriteHelloClientPacket },
        { GameEvent.HELLO_SERVER, WriteHelloServerPacket },
    };

    public static void ReadPacket( DataStreamReader stream, ref DataStreamReader.Context context, ReadCallback callback = null ) {
        GameEvent evt = (GameEvent)stream.ReadUInt(ref context);

        List<object> data = new List<object>();
        readFunctions[evt](stream, ref context, ref data);

        if ( callback != null ) {
            callback(data.ToArray());
        }
    }

    public static DataStreamWriter WritePacket( GameEvent evt ) {
        return writeFunctions[evt]();
    }

    #region HELLO_CLIENT

    public static DataStreamWriter WriteHelloClientPacket() {
        string msg = "Hello!";
        int packetSize = 8 + ( msg.Length + 1 ) * 4 ; //game event + value = 2 * 4 bytes (uint)
        DataStreamWriter writer = new DataStreamWriter(packetSize, Allocator.Temp);
        writer.Write((uint)GameEvent.HELLO_CLIENT);
        writer.Write(++sendValue);

        //write string length (uint)
        //write individual characters (uint)
        
        
        writer.Write((uint)msg.Length);
        for( int i = 0; i < msg.Length; ++i ) {
            writer.Write((uint)msg[i]);
        }

        //Debug.Log("Sending Hello Client: "+sendValue);
        return writer;
    }

    public static void ReadHelloClientPacket( DataStreamReader stream, ref DataStreamReader.Context context, ref List<object> data ) {
        uint value = stream.ReadUInt(ref context);
        //Debug.Log("Received Hello Client: "+value);
        data.Add(value);
       
        string msg = "";
        uint strLen = stream.ReadUInt(ref context);
        int i = 0;
        while( i < strLen ) {
            msg += (char)stream.ReadUInt(ref context);
            i++;
        }

        data.Add(msg);
    }

    #endregion

    #region HELLO_SERVER

    public static DataStreamWriter WriteHelloServerPacket() {
        int packetSize = 8; //game event + value = 2 * 4 bytes (uint)
        DataStreamWriter writer = new DataStreamWriter(packetSize, Allocator.Temp);
        writer.Write((uint)GameEvent.HELLO_SERVER);
        uint sendValue = (uint)Random.Range(uint.MinValue,uint.MaxValue);
        writer.Write(sendValue);

        Debug.Log("Sending Hello Server: "+sendValue);
        return writer;
    }

    public static void ReadHelloServerPacket( DataStreamReader stream, ref DataStreamReader.Context context, ref List<object> data ) {
        uint value = stream.ReadUInt(ref context);
        Debug.Log("Received Hello Server: "+value);
        data.Add(value);
    }

    #endregion
}
