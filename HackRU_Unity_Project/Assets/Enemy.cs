using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour {
	public int Health;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
		if (Health <= 0) {
			this.gameObject.SetActive(false);
			Destroy (this);

		}
	}

	
}
