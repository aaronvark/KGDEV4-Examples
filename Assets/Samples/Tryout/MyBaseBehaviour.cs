using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ClientUpdate {
    public Vector3 position;
    public Vector3 euler;
}

public class MyBaseBehaviour : MonoBehaviour
{
    protected Dictionary<uint, MyClientBehaviour> remoteClients = new Dictionary<uint, MyClientBehaviour>();

    public void CreateRemoteClientForIndex( uint index ) {
        Debug.Log("CreateRemoteClientForIndex "+index);
        
        //instantiate remoteClient prefab for index if not already existing 
        if ( remoteClients.ContainsKey( index ) ) {
            Debug.Log("Client Index already exists! "+index );
            return;
        }
        
        Object prefab = Resources.Load("RemoteClient");

        GameObject clientObject = Instantiate(prefab) as GameObject;
        clientObject.name = "RemoteClient"+index;
        MyClientBehaviour remoteClient = clientObject.GetComponent<MyClientBehaviour>();

        remoteClient.playerIndex = index;

        remoteClients[index] = remoteClient;
    }
}
