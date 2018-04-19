using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MyNetworkManager : NetworkManager {

	public override void OnServerDisconnect(NetworkConnection connection) {
		base.OnServerDisconnect(connection);
		TurnManager.OnClientDisconnect();
	}
}
