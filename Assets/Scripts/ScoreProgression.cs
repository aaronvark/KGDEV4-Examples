using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

//internal score class that we can convert to JSON, and store JSON we receive in
[System.Serializable]
class ScoreData
{
    public string name;
    public int score;
}

//delegate for server communication
public delegate void JsonReply(string json);

public class ScoreProgression : MonoBehaviour {

	//Use consts for the functions and variable names we query our JSON and servers with (will prevent annoying typo's etc)
	const string GET_SCORES = "getScores";
	const string POST_SCORES = "postScores";
	const string LOGIN = "login";
	const string NAME = "name";
	const string SCORE = "score";
	const string SESSION_ID = "sessionID";

	//This is something you'll get back from the login system (do you work with SessionID's?)
	const string sessionID = "12345";
	const string playerName = "someName";
	const int scoreForPlayer = 12345;

	//Formatted JSON that contains a JArray of name & score value pairs
	const string testJSON =
		"[" +
		"{ \"name\": \"name1\", \"score\": 12345 }," +
		"{ \"name\": \"name2\", \"score\": 12346 }," +
		"{ \"name\": \"name3\", \"score\": 12347 }," +
		"{ \"name\": \"name4\", \"score\": 12348 }," +
		"{ \"name\": \"name5\", \"score\": 12349 }" +
		"]";

	const string testJSON2 = "{ \"sessionID\": \"8723842434\" }";

	// Use this for initialization
	void Start() {
		//example of using the generalized ServerFunc Coroutine
		string username = "awesomeName";
		string password = "AwesomePasswordYouShouldNeverStoreInAVariableLikeThis";

		//You might want to use your own hashing function, to get consistent results across platforms
		StartCoroutine(ServerFunc(LOGIN, HandleLogin, "userhash", username.GetHashCode().ToString(), "passhash", password.GetHashCode().ToString()));

		StartCoroutine(ServerFunc(GET_SCORES, ParseScores));
		StartCoroutine(ServerFunc(POST_SCORES, null, NAME, playerName, SCORE, scoreForPlayer.ToString()));

		ParseScores(testJSON);
		HandleLogin(testJSON2);
	}


	IEnumerator ServerFunc(string request, JsonReply callback, params string[] postArgs) {
		//First we create a form to add information to
		List<IMultipartFormSection> formData = new List<IMultipartFormSection>();

		//These two pieces are pretty standard (but will depend on your php code!)
		//	First is the sessionID we probably received during our login process, and we send it with each request so php knows who's talking
		//	Then we also add a "request", which tells the php script what we're trying to do
		//		(You could just have different php scripts, and use a different URL for each type of request)
		formData.Add(new MultipartFormDataSection("sessionID=" + sessionID + "&request=" + request));

		//This is a handy way you can add arguments (in pairs of 2, "&name=value") to the webrequest
		for (int i = 0; i < postArgs.Length; i += 2) {
			formData.Add(new MultipartFormDataSection("&" + postArgs[i] + "=" + postArgs[i + 1]));
		}

		//We're using Post by default, because we're always sending sessionID and request
		UnityWebRequest www = UnityWebRequest.Post("http://www.my-server.com/myform", formData);

		//This blocks
		yield return www.SendWebRequest();

		if (www.isNetworkError || www.isHttpError) {
			Debug.Log(www.error);
		}
		else {
			//If we're expecting something, we can add a 
			if (callback != null) {
				callback(www.downloadHandler.text);
			}
		}

		yield return null;
	}

	void ParseScores(string scoresJSON) {
		JArray arr = JArray.Parse(scoresJSON);

		foreach (var child in arr.Children()) {
			print(child[NAME]);
			print(child[SCORE]);
		}
	}

	void HandleLogin(string passJSON) {
		JObject jObj = JObject.Parse(passJSON);
		string sessionID = jObj[SESSION_ID].Value<string>();
		print("Got sessionID: "+sessionID);
	}
}