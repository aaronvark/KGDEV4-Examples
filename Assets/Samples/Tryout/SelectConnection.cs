using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SelectConnection : MonoBehaviour
{
    public Object serverPrefab, localClientPrefab, remoteClientPrefab;
    public InputField input;
    public static System.Net.IPAddress address;

    public void Server() {
        //create server prefab
        DontDestroyOnLoad( Instantiate(serverPrefab) as GameObject );
        //create local client prefab
        DontDestroyOnLoad( Instantiate(localClientPrefab) as GameObject );

        address = System.Net.IPAddress.Loopback;

        LoadScene();
    }

    public void Client() {
        //create local client prefab
        DontDestroyOnLoad( Instantiate(localClientPrefab) as GameObject );
        
        if ( string.IsNullOrEmpty( input.text ) ) {
            address = System.Net.IPAddress.Loopback;
            LoadScene();
            return;
        }

        if ( System.Net.IPAddress.TryParse( input.text, out address ) ) {
            LoadScene();
        }
        else {
            Debug.LogError("Invalid Server IP");
        }
    }

    void LoadScene() {
        SceneManager.LoadScene("MyGameScene");
    }
}
