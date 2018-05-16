using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Bullet : NetworkBehaviour {

	[SyncVar(hook = "OnLaunchTime")]
	double launchTime = -1;
	[SyncVar(hook = "OnLaunchPos")]
	Vector3 pos;
	[SyncVar(hook = "OnLaunchDir")]
	Vector3 dir;

	//destroy bullets after 5 seconds, if we're the server
	private IEnumerator Start() {
		if (isServer) {
			yield return new WaitForSeconds(5f);
			NetworkServer.Destroy(gameObject);
		}
		else {
			OnLaunchTime(launchTime);
			OnLaunchPos(pos);
			OnLaunchDir(dir);
		}

		//Doesn't work because this isn't a player object!
		//Command/ClientRpc only works for player objects...
		//else if ( !fired ) {
		//	Debug.Log("Data Request");
		//	CmdGetData();
		//}
	}

	//This is a pretty standard way to get confirmation: client spawns, asks server (that always exists at this time) for data, server sends data
	//[Command]
	//void CmdGetData() {
	//	Debug.Log("Got Data Request");
	//	RpcSetData(pos, dir, launchTime);
	//}

	//[ClientRpc]
	//void RpcSetData( Vector3 pos, Vector3 dir, double launchTime ) {
	//	Debug.Log("Got Data!");
	//	if (!fired) {
	//		SetData(pos, dir, launchTime);
	//	}
	//}

	//called by server when spawning, or client when getting synced launch time
	public void SetData(Vector3 pos, Vector3 dir, double launchTime) {
		this.launchTime = launchTime;
		this.pos = pos;
		this.dir = dir;
	}

	//Hooks
	//Important note: pay attention to how each hook function uses the local variables of the other syncvars...
	//We don't know in what order these will arrive, so we just use whatever else we have, and call SetData... (bullets may glitch a little, but things should work fine)
	//This also applies to when we join as a client and receive "latest state", meaning all the variables are correctly synced OnStart

	//hook for launchtime syncvar
	void OnLaunchTime(double launchTime) {
		SetData(pos, dir, launchTime);
	}

	//hook for launchpos syncvar
	void OnLaunchPos(Vector3 pos) {
		SetData(pos, dir, launchTime);
	}

	//hook for launchdir syncvar
	void OnLaunchDir(Vector3 dir) {
		SetData(pos, dir, launchTime);
	}

	//if we're fired, update our position
	void Update() {
		if ( launchTime != -1 ) {
			double flyTime = Network.time - launchTime;
			transform.position = pos + dir * (float)flyTime;
		}
	}
}