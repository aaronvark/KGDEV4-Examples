using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;

//TODO: Network Time sync events?
public enum GameEvent {
    REQUEST_PLAYERINDEX = 0,    //(client -> server), DONE
    ASSIGN_PLAYERINDEX,     //playerIndex (server -> client), DONE
    PLAYER_JOINED,          //playerIndex (server -> client), DONE
    PLAYER_DISCONNECT,      //playerIndex (server -> client), DONE
    PING,
    PONG,                   
    PLAYER_UPDATE,          //numPlayers, [playerIndex, position, rotation] (server -> client), DONE
    //Handled through CLIENT_INPUT_CHANGED
    //SHOOT_REQUEST,          //(client -> server)
    PLAYER_SHOOT,           //networkId (of bullet), player_data (position/rotation) (server -> client)
    TURN_UPDATE,            //previousTurnIndex, currentTurnIndex (playerIndex, server -> client)
    BULLET_UPDATE,          //networkId, bullet_data (server -> client)
    PLAYER_HIT,             //networkId player, networkId bullet, player health (0 == death) (server -> client)
    CLIENT_INPUT_CHANGED,   //changeCount, [button, state] (client -> server)
    RESPAWN_REQUEST,         //(client -> server)
    DESTROY_NETWORKED_OBJECT, //networkId, (server -> client)
}

//Class that contains all the functions that parse game event packets
//Note that these events may be generated elsewhere (see above list for send directions)
public static class GameEvents
{
    public delegate void PacketFunction( object caller, DataStreamReader stream, ref DataStreamReader.Context context, NetworkConnection source );

    public static Dictionary<GameEvent, PacketFunction> EventFunctions = new Dictionary<GameEvent, PacketFunction>()
    {
        { GameEvent.REQUEST_PLAYERINDEX, RequestPlayerIndex },
        { GameEvent.ASSIGN_PLAYERINDEX, AssignPlayerIndex },
        { GameEvent.PLAYER_JOINED, PlayerJoined },
        { GameEvent.CLIENT_INPUT_CHANGED, ClientInputChanged },
        { GameEvent.PLAYER_UPDATE, PlayerUpdate },
        { GameEvent.PING, Ping },
        { GameEvent.PONG, Pong },
        { GameEvent.PLAYER_SHOOT, PlayerShoot },
        { GameEvent.DESTROY_NETWORKED_OBJECT, DestroyNetworkedObject }
    };

    public static void RequestPlayerIndex( object caller, DataStreamReader stream, ref DataStreamReader.Context context, NetworkConnection source  ) {
        //bind this connection to a specific player index
        MyServerBehaviour server = caller as MyServerBehaviour;
        uint eventType = (uint)GameEvent.ASSIGN_PLAYERINDEX;
        uint index = server.AddNextAvailablePlayerIndex(source);

        Debug.Log("Sending player index " + index + " to client");
        
        //Tell source client what their player index is
        using (var writer = new DataStreamWriter(8, Allocator.Temp))
        {
            writer.Write(eventType);
            writer.Write(index);
            source.Send(server.m_Driver, writer);
        }

        //Create remoteclient representation for newly connected player
        //TODO: figure out what the server needs to store here (if anything)
        //server.CreateRemoteClientForIndex(index);
        
        //Send message to all other clients about newly connected player
        using( var writer = new DataStreamWriter(8, Allocator.Temp)) {
            writer.Write((uint)GameEvent.PLAYER_JOINED);
            writer.Write(index);
            server.BroadcastToClientsExcluding(source, writer);
        }

        //Send message per existing client to newly connected player
        server.SendExistingPlayersTo(source);
    }
    
    public static void AssignPlayerIndex( object caller, DataStreamReader stream, ref DataStreamReader.Context context, NetworkConnection source ) {
        //got player index from server
        MyClientBehaviour client = caller as MyClientBehaviour;

        uint index = stream.ReadUInt(ref context);

        Debug.Log("Assigned player index " + index + " to my client instance");

        //TODO: Make this a function?
        client.playerIndex = index;
        client.name = "Client"+index;
    }

    public static void PlayerJoined( object caller, DataStreamReader stream, ref DataStreamReader.Context context, NetworkConnection source ) {
        //got remote player joined from server
        MyClientBehaviour client = caller as MyClientBehaviour;

        uint index = stream.ReadUInt(ref context);

        client.CreateRemoteClientForIndex(index);
    }

    public static void ClientInputChanged( object caller, DataStreamReader stream, ref DataStreamReader.Context context, NetworkConnection source ) {
        MyServerBehaviour server = caller as MyServerBehaviour;
        uint playerIndex = stream.ReadUInt(ref context);
        uint dirtFlags = stream.ReadUInt(ref context);
        uint buttonState = stream.ReadUInt(ref context);
        float mouseX = stream.ReadFloat(ref context);
        float mouseY = stream.ReadFloat(ref context);
        float mouseZ = stream.ReadFloat(ref context);

        server.UpdateClientInput( playerIndex, buttonState, mouseX, mouseY, mouseZ );

        Debug.Log( "Got Client Input for "+playerIndex );
    }

    public static void PlayerUpdate( object caller, DataStreamReader stream, ref DataStreamReader.Context context, NetworkConnection source ) {
        //Debug.Log("Got Player Update from server");

        MyClientBehaviour client = caller as MyClientBehaviour;
        uint numPlayers = stream.ReadUInt(ref context);

        for( int i = 0; i < numPlayers; ++i ) {
            uint playerIndex = stream.ReadUInt(ref context);
            float posX = stream.ReadFloat(ref context);
            float posY = stream.ReadFloat(ref context);
            float posZ = stream.ReadFloat(ref context);
            float rotX = stream.ReadFloat(ref context);
            float rotY = stream.ReadFloat(ref context);
            float rotZ = stream.ReadFloat(ref context);

            ClientUpdate clientUpdate;
            clientUpdate.position.x = posX;
            clientUpdate.position.y = posY;
            clientUpdate.position.z = posZ;
            clientUpdate.euler.x = rotX;
            clientUpdate.euler.y = rotY;
            clientUpdate.euler.z = rotZ;

            client.ApplyClientUpdate(clientUpdate, playerIndex);
        }
    }

    public static void Ping( object caller, DataStreamReader stream, ref DataStreamReader.Context context, NetworkConnection source ) {
        MyClientBehaviour client = caller as MyClientBehaviour;

        //TODO: get network time, correct offsets, send pong
    }

    public static void Pong( object caller, DataStreamReader stream, ref DataStreamReader.Context context, NetworkConnection source ) {
        MyServerBehaviour server = caller as MyServerBehaviour;
        
        //calculate client lag from half-time of total event
    }

    public static void PlayerShoot( object caller, DataStreamReader stream, ref DataStreamReader.Context context, NetworkConnection source ) {
        MyClientBehaviour client = caller as MyClientBehaviour;

        //playerId and bullet id
        uint playerId = stream.ReadUInt(ref context);
        uint networkId = stream.ReadUInt(ref context);

        //position
        float posX = stream.ReadFloat(ref context);
        float posY = stream.ReadFloat(ref context);
        float posZ = stream.ReadFloat(ref context);
        //rotation
        float rotX = stream.ReadFloat(ref context);
        float rotY = stream.ReadFloat(ref context);
        float rotZ = stream.ReadFloat(ref context);

        //spawn a bullet with the received networkId and position/direction
        GameObject bullet;
        uint id = NetworkId.Spawn(Resources.Load("ClientBullet"), ref client.repository, networkId );
        if ( NetworkId.Find(id, out bullet, ref client.repository) ) {
            //set playerId on bullet script, set position and direction of player that's shooting
            bullet.transform.rotation = Quaternion.Euler(rotX, rotY, rotZ);
            bullet.transform.position = new Vector3(posX, posY, posZ);
        }
    }
    public static void DestroyNetworkedObject( object caller, DataStreamReader stream, ref DataStreamReader.Context context, NetworkConnection source ) {
        MyClientBehaviour client = caller as MyClientBehaviour;
        uint networkId = stream.ReadUInt(ref context);
        NetworkId.DestroyNetworked(networkId, ref client.repository);
    }
}
