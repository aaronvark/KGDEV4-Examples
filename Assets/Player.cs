using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Player : NetworkBehaviour
{
	internal int playerId = -1;

	public Object bulletPrefab;

	private void Start() {
		if ( isServer ) {
			playerId = TurnManager.Register(this);
		}
	}

	// Update is called once per frame
	void Update () {
		if (isLocalPlayer) {
			//handle input, store positional information of input
			float h = Input.GetAxis("Horizontal") * Time.deltaTime * 10;
			float v = Input.GetAxis("Vertical") * Time.deltaTime * 10;

			transform.Translate(h, 0, v);

			if ( Input.GetMouseButtonDown(0)) {
				Vector3 pos = transform.position;
				Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
				//Debug.Log(mousePos);
				mousePos.y = 0;
				Vector3 dir = mousePos - pos;

				CmdFireBullet(pos, dir);
			}
		}
	}

	[Command]
	void CmdFireBullet( Vector3 position, Vector3 direction ) {
		if ( TurnManager.IsTurn( playerId ) ) {
			//Fire bullet!
			Bullet b = (Instantiate(bulletPrefab, position, Quaternion.identity) as GameObject).GetComponent<Bullet>();
			b.transform.forward = direction;
			b.SetData(b.transform.position, b.transform.forward, Network.time);

			//create bullet on clients
			NetworkServer.Spawn(b.gameObject);

			TurnManager.NextTurn();
		}
	}
}
