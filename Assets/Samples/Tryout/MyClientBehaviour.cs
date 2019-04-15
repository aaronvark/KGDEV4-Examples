using System.Net;
using UnityEngine;

using Unity.Networking.Transport;
using Unity.Collections;

using NetworkConnection = Unity.Networking.Transport.NetworkConnection; 
using UdpCNetworkDriver = Unity.Networking.Transport.BasicNetworkDriver<Unity.Networking.Transport.IPv4UDPSocket>;


public enum ClientInput {
    LEFT        = 1,
    RIGHT       = 2,
    UP          = 4,
    DOWN        = 8,
    MOUSE_POS   = 16,
    MOUSE_BUT   = 32
}

public delegate void InputCallback( ClientInputState input );

public class ClientInputState {
    //tracks input changes with flags, sent over network to parse optimized packet
    public uint dirtFlags = 0;
    //stores button up/down as flags as well
    private uint buttonState = 0;
    public Vector3 mousePosition = Vector3.zero;

    public event InputCallback onShoot;

    //Used by clients to set individual input values
    public void Set( ClientInput input, object setting ) {
        uint uinput = (uint)input;
        switch( input ) {
            case ClientInput.MOUSE_POS:
                Vector3 p = (Vector3)setting;
                //Debug.Log( "Setting Mouse Pos: "+mousePosition + " / "+p );
                if ( Vector3.Distance( mousePosition, p ) > 0.01f ) {
                    mousePosition = p;
                    dirtFlags |= uinput;
                }
            break;
            default:
                //only apply change is different, and store dirty on flags
                if ( ( buttonState & uinput ) > 0 != (bool) setting ) {
                    if ( (bool) setting ) {
                        //Debug.Log("Down for "+input.ToString());
                        buttonState |= uinput;
                    }
                    else {
                        //Debug.Log("Up for "+input.ToString() + " - " + ( buttonState & uinput ) + " - " + (bool)setting );
                        buttonState &= ~uinput;
                    }
                    dirtFlags |= uinput;
                }
            break;
        }
    }

    public bool GetButton( ClientInput button ) {
        return ( buttonState & (uint)button ) > 0;
    }

    //Used by server to set entire states
    public void SetState( uint buttonState, float x, float y, float z ) {
        //check mouseDown event for fire
        if ( ( this.buttonState & (uint)ClientInput.MOUSE_BUT ) == 0 && ( buttonState & (uint)ClientInput.MOUSE_BUT ) != 0 ) {
            //player shoot event
            if ( onShoot != null ) {
                onShoot(this);
            }
        }

        this.buttonState = buttonState;
        mousePosition.x = x;
        mousePosition.y = y;
        mousePosition.z = z;
        
        //TODO: Figure out how server-side client is going to respond to mouse pressed event (to fire)
    }

    public int GetPacketLength() {
        //TODO: optimize based on dirtFlags
        int size = 0;

        //4 byte for flags
        //4 byte for buttonState
        //3 * 4 bytes for mouse position
        size =  4 + 4 + ( 3 * 4 );

        return size;
    }

    public void WritePacket( DataStreamWriter writer ) {
        //TODO: optimize
        writer.Write(dirtFlags);
        writer.Write(buttonState);
        writer.Write(mousePosition.x);
        writer.Write(mousePosition.y);
        writer.Write(mousePosition.z);
    }
}

public class MyClientBehaviour : MyBaseBehaviour
{
    public UdpCNetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public bool Done;
    public bool connected = false;
    public uint playerIndex;
    public bool isLocalPlayer = false;

    Transform t;
    ClientInputState inputState;
    internal ObjectRepository repository;
    
    void Start () { 
        t = transform;

        if ( isLocalPlayer ) {
            m_Driver = new UdpCNetworkDriver(new INetworkParameter[0]);
            m_Connection = default(NetworkConnection);

            var endpoint = new IPEndPoint(SelectConnection.address, 9000);
            m_Connection = m_Driver.Connect(endpoint);

            inputState = new ClientInputState();

            repository = new ObjectRepository();
        }
    }

    public void OnDestroy() { 
        if ( isLocalPlayer ) {
            m_Driver.Dispose();
        }
    }

    public void ApplyClientUpdate( ClientUpdate data, uint index ) {
        if ( index == playerIndex ) {
            transform.position = data.position;
            transform.rotation = Quaternion.Euler(data.euler);
        }
        else if ( remoteClients.ContainsKey(index) ) {
            remoteClients[index].ApplyClientUpdate(data, index);
        }
        else {
            Debug.LogError("Received ClientUpdate for a remoteClient that we haven't spawned");
        }
    }

    void Update() { 
        if ( !isLocalPlayer ) return;

        m_Driver.ScheduleUpdate().Complete();

        HandleConnection();
        HandleNetworkEvents();

        HandleInput();
    }

    void HandleConnection() {
        if (!m_Connection.IsCreated)
        {
            if (!Done)
                Debug.Log("Something went wrong during connect");
            return;
        }
        else if (!connected) {
            if ( Time.frameCount % 180 == 0 ) { //give it a few seconds...
                Debug.Log("No connect event, retrying");
                //We haven't received a connected event, retry connection
                var endpoint = new IPEndPoint(IPAddress.Loopback, 9000);
                m_Connection = m_Driver.Connect(endpoint);
            }
        }
    }

    void HandleNetworkEvents() {
        DataStreamReader stream;
        NetworkEvent.Type cmd;
        //while ( true ) {
        //    cmd = m_Connection.PopEvent(m_Driver, out stream);
        //    if ( cmd == NetworkEvent.Type.Empty )
        //        break;
        //    //do shit
        //}

        while ((cmd = m_Connection.PopEvent(m_Driver, out stream)) != 
            NetworkEvent.Type.Empty)
        {
            switch( cmd ) {
                case NetworkEvent.Type.Connect:
                    HandleConnectEvent(stream);
                break;
                case NetworkEvent.Type.Data:
                    HandleDataEvent(stream);
                break;
                case NetworkEvent.Type.Disconnect:
                    HandleDisconnectEvent(stream);
                break;
            }
        }
    }

    void HandleInput() {
        //WSAD, Left Mouse Button
        inputState.Set(ClientInput.LEFT, Input.GetKey(KeyCode.A));
        inputState.Set(ClientInput.RIGHT, Input.GetKey(KeyCode.D));
        inputState.Set(ClientInput.UP, Input.GetKey(KeyCode.W));
        inputState.Set(ClientInput.DOWN, Input.GetKey(KeyCode.S));
        inputState.Set(ClientInput.MOUSE_BUT, Input.GetMouseButton(0));

        //Mouse World Position
        inputState.Set(ClientInput.MOUSE_POS, Camera.main.ScreenPointToRay(Input.mousePosition).origin);

        if ( inputState.dirtFlags > 0 ) {
            //send packet to server
            Debug.Log("Sending input change to server "+playerIndex);
            int inputPacketLength = inputState.GetPacketLength();

            using (var writer = new DataStreamWriter(8 + inputPacketLength, Allocator.Temp))
            {
                writer.Write((uint)GameEvent.CLIENT_INPUT_CHANGED);
                writer.Write(playerIndex);
                inputState.WritePacket(writer);
                m_Driver.Send(m_Connection, writer);
            }

            inputState.dirtFlags = 0;
        }
    }

    public void ApplyPositionDirection( Vector3 position, Vector3 euler ) {
        t.position = position;
        t.rotation = Quaternion.Euler(euler);
    }

    void HandleConnectEvent( DataStreamReader stream )  {
        Debug.Log("We are now connected to the server");
        connected = true;
        
        //Send some kind of confirmation menu
        var eventType = (uint)GameEvent.REQUEST_PLAYERINDEX;
        using (var writer = new DataStreamWriter(4, Allocator.Temp))
        {
            writer.Write(eventType);
            m_Connection.Send(m_Driver, writer);
        }
    }

    void HandleDataEvent( DataStreamReader stream )  {
        var readerCtx = default(DataStreamReader.Context);
        GameEvent eventType = (GameEvent)stream.ReadUInt(ref readerCtx);

        Debug.Log("Got the value = " + eventType.ToString() + " back from the server");

        GameEvents.EventFunctions[eventType]( this, stream, ref readerCtx, m_Connection );
    }

    void HandleDisconnectEvent( DataStreamReader stream )  {
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }
}
