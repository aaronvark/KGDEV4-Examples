using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate void BulletCallback( Bullet b, uint networkId, Vector3 position );

public class Bullet : MonoBehaviour
{
    public event BulletCallback positionChanged;
    public event BulletCallback outOfLife;

    public uint playerId;
    public uint networkId;

    bool isServerBullet = false;
    float life = 2f;

    public void SetData( uint playerId, bool isServerBullet = false ) {
        this.isServerBullet = isServerBullet;
        this.playerId = playerId;
        this.networkId = GetComponent<NetworkId>().networkId;
    }

    void Update() {
        //move forward
        transform.Translate(0 , 0, 100 * Time.deltaTime, Space.Self);
        
        if ( isServerBullet ) {
            if ( life >= 0 ) {
                life -= Time.deltaTime;
                if ( life <= 0 ) {
                    if ( outOfLife != null ) {
                        outOfLife(this, networkId, transform.position);
                    }
                }
                 //check collision with players
                if ( positionChanged != null )
                    positionChanged(this, networkId, transform.position);
            }
        }
    }
}
