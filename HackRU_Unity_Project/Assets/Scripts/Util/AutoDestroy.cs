using UnityEngine;
using System.Collections;

public class AutoDestroy : MonoBehaviour {

    public float length;
    private float startTime;

	// Use this for initialization
	void Start () {
        startTime = Time.time;
	}
	
	// Update is called once per frame
	void Update () {
        if (Time.time - startTime > length)
            Destroy(gameObject);
	}
}
