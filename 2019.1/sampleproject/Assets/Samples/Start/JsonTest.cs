using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine;

using SimpleJSON;

public class SomeData {
    public string origin;
}

public class JsonTest : MonoBehaviour
{
    // Start is called before the first frame update
    IEnumerator Start()
    {
        yield return DoGet();
        yield return DoPost();

        yield return null;
    }

    IEnumerator DoGet() {
        //Simple http GET request, this one returns out IP as an { "origin": "ip" } json
        UnityWebRequest request = UnityWebRequest.Get("http://httpbin.org/ip");
        yield return request.SendWebRequest();

        if ( request.isHttpError || request.isNetworkError ) {
            Debug.LogError("Oops");
        }
        else {
            Debug.Log(request.downloadHandler.text);

            //Parse as searchable JSON object
            var json = JSON.Parse(request.downloadHandler.text);
            Debug.Log(json["origin"]);
            
            //Parse Json into a Unity Object (this only works because our SomeData class has the same layout!)
            SomeData d = JsonUtility.FromJson<SomeData>(request.downloadHandler.text);
            Debug.Log(d.origin);
        }
    }

     IEnumerator DoPost() {
        //Add form data for Post (such as (hashed!) login information)
        WWWForm form = new WWWForm();
        form.AddField("origin", "127.0.0.1");

        //Use post, append form
        UnityWebRequest request = UnityWebRequest.Post("http://httpbin.org/post", form);
        yield return request.SendWebRequest();

        if ( request.isHttpError || request.isNetworkError ) {
            Debug.LogError("Oops");
        }
        else {
            Debug.Log(request.downloadHandler.text);
            
            //Parse as searchable JSON, instead of Unity object (sometimes you don't want Unity objects!)
            var n = JSON.Parse( request.downloadHandler.text );
            Debug.Log(n["form"]);

            //Parse form into Unity object anyway!
            SomeData d = JsonUtility.FromJson<SomeData>(n["form"].ToString());
            Debug.Log(d.origin);
        }
    }

    void OnDrawGizmos() {

        //Debug.Log("spam");
        Vector3 p1, p2, p3, p4;
        p1 = new Vector3( 4, 6, 3);
        p2 = new Vector3( 10, 8, 1);
        p3 = new Vector3( 5, 2, 9);
        p4 = new Vector3( 6, 6, 6);

        Vector3 A = p1 - p2;
        Vector3 B = p3 - p2;
        Vector3 Tn = Vector3.Cross(B.normalized,A.normalized);

        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p1);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(p1,1f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(p2,1f);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(p3,1f);

        Gizmos.color = Color.yellow;
        Vector3 Pn = ( p1 + p2 + p3 ) * .33f;
        Gizmos.DrawLine(Pn, Pn + Tn * 10);
        
        Gizmos.DrawWireSphere(p4, 1f);
        //Debug.Log(Vector3.Dot((p4-p1).normalized, Tn));
    }
}
