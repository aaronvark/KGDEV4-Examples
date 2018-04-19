using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Server-only manager class to control who's turn it is
/// </summary>
public class TurnManager : MonoBehaviour {

	//this increments as long as new players arrive
	static int playerId = 0;
	//singleton for ease of access, but unsafe because it shouldn't be lazy-spawned
	static TurnManager instance;

	//list variables, so we can have any order of active players, also keeping in mind they might D/C and reconnect
	int currentPlayer = 0;
	List<int> activePlayerIds = new List<int>();

	void Awake() {
		instance = this;
	}

	//called from custom networkmanager
	public static void OnClientDisconnect() {
		instance.StartCoroutine(instance.CheckPlayerIds());
	}

	//basically rebuilds all active playerId's from spawned player objects
	//not super efficient, but shouldn't happen too often
	IEnumerator CheckPlayerIds() {
		//if we do this right away, the disconnected player is still present
		yield return new WaitForSeconds(.25f);

		int currentId = activePlayerIds[currentPlayer];

		Debug.Log("RECREATING LIST");
		//rebuild active player list
		Player[] players = FindObjectsOfType<Player>();

		activePlayerIds.Clear();
		foreach (Player p in players) {
			activePlayerIds.Add(p.playerId);
		}

		if ( activePlayerIds.Contains(currentId)) {
			currentPlayer = activePlayerIds.IndexOf(currentId);
		}
		else {
			currentPlayer = 0;
		}
	}

	/// <summary>
	/// Player objects register themselves when they are spawned (server-only)
	/// </summary>
	/// <param name="p"></param>
	/// <returns></returns>
	public static int Register( Player p ) {
		instance.activePlayerIds.Add(playerId);
		Debug.Log("Registered player " + playerId);
		return playerId++;
	}

	/// <summary>
	/// Used by players to determine if it is their turn, only called from server-side objects
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public static bool IsTurn( int id ) {
		if ( instance.currentPlayer >= instance.activePlayerIds.Count ) {
			instance.currentPlayer = 0;
			instance.CheckPlayerIds();
		}
		return instance.activePlayerIds[instance.currentPlayer] == id;
	}

	/// <summary>
	/// Moves the turn to the next player in the list
	/// TODO: Tell clients who's turn it is!
	/// </summary>
	public static void NextTurn() {
		if (++instance.currentPlayer >= instance.activePlayerIds.Count) {
			instance.currentPlayer = 0;
		}
	}
}
