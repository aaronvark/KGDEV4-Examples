using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate void NetworkIdEvent( uint id );

public class ObjectRepository {
    public Dictionary<uint,GameObject> networkedObjects = new Dictionary<uint, GameObject>();
    public uint currentId = 0;
}

public class NetworkId : MonoBehaviour
{
    public static uint Spawn( Object prefab, ref ObjectRepository repo, uint existingId = 0 ) {
        GameObject g = Instantiate( prefab ) as GameObject;
        NetworkId netId = g.AddComponent<NetworkId>();
        if ( existingId == 0 ) {
            netId.networkId = GenerateNetworkId(ref repo);
        }
        else {
            netId.networkId = existingId;
        }

        repo.networkedObjects[netId.networkId] = g;

        return netId.networkId;
    }

    public static void DestroyNetworked( uint id, ref ObjectRepository repo ) {
        if ( repo.networkedObjects.ContainsKey(id) ) {
            GameObject g = repo.networkedObjects[id];
            Destroy(g);
            repo.networkedObjects.Remove(id);
        }
        else {
            Debug.Log("NOT FOUND");
        }
    }

    static uint GenerateNetworkId(ref ObjectRepository repo) {
        //TODO: make this safe, because now clients could potentially call this as well
        return ++repo.currentId;
    }

    public static bool Find( uint id, out GameObject g, ref ObjectRepository repo ) {
        if ( repo.networkedObjects.ContainsKey(id) ) {
            g = repo.networkedObjects[id];
            return true;
        }

        g = null;
        return false;
    }

    public uint networkId = 0;
}
