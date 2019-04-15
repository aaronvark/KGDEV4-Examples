using System.Net;
using UnityEngine;

using Unity.Networking.Transport;
using Unity.Collections;
using System.Collections.Generic;
using System.Collections;

using NetworkConnection = Unity.Networking.Transport.NetworkConnection; 
using UdpCNetworkDriver = Unity.Networking.Transport.BasicNetworkDriver<Unity.Networking.Transport.IPv4UDPSocket>;

public class PlayerData {
    public Vector3 position = Vector3.zero;
    public Vector3 euler = Vector3.zero;
    public int health = 0;
    public ClientInputState inputState = new ClientInputState();

    private ITurnManager turnManager;
    private uint playerId; 

    public PlayerData( ITurnManager turnManager, uint playerId ) {
        this.turnManager = turnManager;
        this.playerId = playerId;

        inputState.onShoot += OnShoot;
    }

    void OnShoot( ClientInputState input ) {
        Debug.Log("Got Shoot Request");
        if ( turnManager.CurrentTurn() == playerId ) {
            turnManager.ConsumeTurn();
        }
    }

    public void HandleInput() {
        float dist = Time.deltaTime * 5;
        if ( inputState.GetButton(ClientInput.LEFT)) {
            position.x -= dist;
        }
        if ( inputState.GetButton(ClientInput.RIGHT)) {
            position.x += dist;
        }
        if ( inputState.GetButton(ClientInput.UP)) {
            position.z += dist;
        }
        if ( inputState.GetButton(ClientInput.DOWN)) {
            position.z -= dist;
        }

        Vector3 mousePos = inputState.mousePosition;
        mousePos.y = position.y;
        Vector3 dir = mousePos - position;
        euler = Quaternion.LookRotation(dir, Vector3.up).eulerAngles;
    }
}

public interface ITurnManager {
    uint CurrentTurn();
    void ConsumeTurn();
}

//TODO: Bundle events per frame to clients, send as bulk packet (is this already handled by the driver?)
public class MyServerBehaviour : MyBaseBehaviour, ITurnManager
{
    public UdpCNetworkDriver m_Driver;
    private NativeList<NetworkConnection> m_Connections;

    private Dictionary<uint, NetworkConnection> playerIndices = new Dictionary<uint, NetworkConnection>();
    private Dictionary<uint, PlayerData> playerData = new Dictionary<uint, PlayerData>();
    

    #region ITurnManager
    uint currentTurnIndex = 0;

    ObjectRepository repository;

    public uint CurrentTurn() {
        return currentTurnIndex;
    }

    public void ConsumeTurn() {
        //NetworkId spawn a bullet
        GameObject bullet;
        uint id = NetworkId.Spawn(Resources.Load("ServerBullet"), ref repository);
        if ( NetworkId.Find(id, out bullet, ref repository) ) {
            //set playerId on bullet script, set position and direction of player that's shooting
            Bullet b = bullet.GetComponent<Bullet>();
            b.SetData(currentTurnIndex, true);
            b.positionChanged += CheckCollisionWithPlayers;
            b.outOfLife += DestroyNetworkedBullet;
            b.transform.rotation = Quaternion.Euler(playerData[currentTurnIndex].euler);
            b.transform.position = playerData[currentTurnIndex].position + b.transform.forward;

            Debug.Log("PLAYER SHOOTING!");

            //Send PLAYER_SHOOT event
            //event, playerId, networkId, position (3), rotation(3)
            //TODO: Add networkTime
            using (var writer = new DataStreamWriter(12 + (6*4), Allocator.Temp))
            {
                writer.Write((uint)GameEvent.PLAYER_SHOOT);
                writer.Write((uint)currentTurnIndex);
                writer.Write(id);
                
                writer.Write(b.transform.position.x);
                writer.Write(b.transform.position.y);
                writer.Write(b.transform.position.z);

                writer.Write(b.transform.eulerAngles.x);
                writer.Write(b.transform.eulerAngles.y);
                writer.Write(b.transform.eulerAngles.z);

                for( int i = 0; i < m_Connections.Length; ++i ) {
                    m_Connections[i].Send(m_Driver, writer);
                }
            }
        }
        else {
            Debug.LogError("Couldn't find object we just spawned!");
        }

        //increment turn
        NextTurn();
    }

    void NextTurn() {
        currentTurnIndex = ++currentTurnIndex % (uint)playerIndices.Count;
    }

    #endregion

    void CheckCollisionWithPlayers( Bullet b, uint networkId, Vector3 position ) {
        foreach( KeyValuePair<uint, PlayerData> pair in playerData ) {
            //don't shoot yourself
            if ( pair.Key == b.playerId )
                continue;

            if ( Vector3.Distance( pair.Value.position, position ) < 1f ) {
                //Player Hit
                Debug.Log("Player Hit");
                pair.Value.health -= 1;
                DestroyNetworkedBullet(b, networkId, position);
                return;
            }            
        }
    }

    void DestroyNetworkedBullet( Bullet b, uint networkId, Vector3 position ) {
        NetworkId.DestroyNetworked(networkId, ref repository);
        SendNetworkedDestructionEventFor(networkId);
    }

    private float networkTime;

    // Start is called before the first frame update
    void Start() {
        m_Driver = new UdpCNetworkDriver(new INetworkParameter[0]);
        if (m_Driver.Bind(new IPEndPoint(IPAddress.Any, 9000)) != 0)
            Debug.Log("Failed to bind to port 9000");
        else
            m_Driver.Listen();

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);

        //NetworkId.objectDestroyed += SendNetworkedDestructionEventFor;

        repository = new ObjectRepository();

        StartCoroutine(Clock());
    }

    void OnDestroy() {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    // Update is called once per frame
    void Update() {
        m_Driver.ScheduleUpdate().Complete();

        CleanUpStaleConnections();
        AcceptNewConnections();
        HandleEvents();

        SimulatePlayers();
        BroadcastPlayerData();
    }

    IEnumerator Clock() {
        while( true ) {
            networkTime = Time.time;

            if ( Time.frameCount % 60 == 0 ) {
                //send PING event to all clients
                for( int i = 0; i < m_Connections.Length; ++i ) {
                    using (var writer = new DataStreamWriter(8, Allocator.Temp))
                    {
                        writer.Write((uint)GameEvent.PING);
                        writer.Write(networkTime);
                        m_Driver.Send(m_Connections[i], writer);
                    }
                }
            }

            yield return null;
        }
    }

    void SendNetworkedDestructionEventFor( uint networkId ) {
        using (var writer = new DataStreamWriter(8, Allocator.Temp))
        {
            writer.Write((uint)GameEvent.DESTROY_NETWORKED_OBJECT);
            writer.Write(networkId);
            for( int i = 0; i < m_Connections.Length; ++i ) {
                m_Connections[i].Send(m_Driver, writer);
            }
        }
    }

    void SimulatePlayers() {
        foreach( KeyValuePair<uint, PlayerData> pair in playerData ) {
            pair.Value.HandleInput();
        }
    }
    
    void BroadcastPlayerData() {
            //send simulated player data to all connected clients
        //(eventType + numPlayers) + numPlayers[playerIndex + position + rotation(euler)]
        int byteCount = 8 + playerData.Count * ( 4 + ( 3 * 4 ) + ( 3 * 4 ) );
        using (var writer = new DataStreamWriter(byteCount, Allocator.Temp))
        {
            writer.Write((uint)GameEvent.PLAYER_UPDATE);
            writer.Write((uint)playerData.Count);
            foreach( KeyValuePair<uint, PlayerData> pair in playerData ) {
                writer.Write(pair.Key);
                writer.Write(pair.Value.position.x);
                writer.Write(pair.Value.position.y);
                writer.Write(pair.Value.position.z);
                writer.Write(pair.Value.euler.x);
                writer.Write(pair.Value.euler.y);
                writer.Write(pair.Value.euler.z);
            }
            for( int i = 0; i < m_Connections.Length; ++i ) {
                m_Connections[i].Send(m_Driver, writer);
            }
        }
    }

    void CleanUpStaleConnections() {
        // Clean up connections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {
                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }
    }

    void AcceptNewConnections() {
        // Accept new connections
        NetworkConnection c;
        while ((c = m_Driver.Accept()) != default(NetworkConnection))
        {
            m_Connections.Add(c);
            Debug.Log("Accepted a connection");
        }
    }

    void HandleEvents() {
        //Handle events
        DataStreamReader stream;
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
                continue;

            NetworkEvent.Type cmd;
            while ((cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream)) !=
                NetworkEvent.Type.Empty)
            {
                //handle event
                switch( cmd ) {
                    case NetworkEvent.Type.Data:
                        HandleData(stream, i);
                    break;
                    case NetworkEvent.Type.Disconnect:
                        HandleDisconnect(i);
                    break;
                }
            }
        }
    }

    void HandleData( DataStreamReader stream, int connectionIndex ) {
        var readerCtx = default(DataStreamReader.Context);

        //Get Data Type (what kind of update is this?)
        GameEvent eventType = (GameEvent)stream.ReadUInt(ref readerCtx);
        Debug.Log("Got " + eventType.ToString() + " from Client " + connectionIndex);

        GameEvents.EventFunctions[eventType]( this, stream, ref readerCtx, m_Connections[connectionIndex] );
    }

    void HandleDisconnect( int connectionIndex ) {
        uint disconnectedPlayerIndex = 99;

        foreach( KeyValuePair<uint,NetworkConnection> pair in playerIndices) {
            if ( m_Connections[connectionIndex] == pair.Value ) {
                playerIndices.Remove(pair.Key);
                playerData.Remove(pair.Key);
                disconnectedPlayerIndex = pair.Key;
                break;
            }
        }

        //tell other clients that this client was disconnected
        for( int i = 0; i < m_Connections.Length; ++i ) {
            if ( i == connectionIndex ) 
                continue;

            using (var writer = new DataStreamWriter(8, Allocator.Temp))
            {
                writer.Write((uint)GameEvent.PLAYER_DISCONNECT);
                writer.Write(disconnectedPlayerIndex);
                m_Connections[i].Send(m_Driver, writer);
            }
        }

        m_Connections[connectionIndex] = default(NetworkConnection);
    }

    internal uint AddNextAvailablePlayerIndex( NetworkConnection c ) {
        uint i = 0;
        while( playerIndices.ContainsKey(i) ) i++;

        playerIndices[i] = c;

        //initialize PlayerData for this player
        playerData[i] = new PlayerData(this, i);
        
        return i;
    }

    internal void BroadcastToClientsExcluding( NetworkConnection source, DataStreamWriter writer ) {
        for( int i = 0; i < m_Connections.Length; ++i ) {
            //skip broadcast for source client
            if ( m_Connections[i] == source )
                continue;

            m_Connections[i].Send(m_Driver, writer);
        }
    }

    internal void SendExistingPlayersTo( NetworkConnection source ) {
        for( int i = 0; i < m_Connections.Length; ++i ) {
            //skip message for source client
            if ( m_Connections[i] == source )
                continue;

            int index = GetIndexForConnection(m_Connections[i]);
            if ( index != -1 ) {
                using( var writer = new DataStreamWriter(8, Allocator.Temp)) {
                    writer.Write((uint)GameEvent.PLAYER_JOINED);
                    writer.Write((uint)index);
                    source.Send(m_Driver, writer);
                }
            }
        }
    }

    internal void UpdateClientInput( uint playerIndex, uint buttonState, float x, float y, float z ) {
        playerData[playerIndex].inputState.SetState( buttonState, x, y, z );
    }

    //return -1 if not found
    int GetIndexForConnection( NetworkConnection c ) {
        foreach( KeyValuePair<uint,NetworkConnection> pair in playerIndices ) {
            if ( c == pair.Value ) {
                return (int)pair.Key;
            }
        }
        return -1;
    }
}
