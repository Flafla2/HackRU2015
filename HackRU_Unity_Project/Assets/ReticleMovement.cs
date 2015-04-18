using UnityEngine;
using System.Collections;

public class ReticleMovement : MonoBehaviour {
	public Camera Cam;
	
	RectTransform rt;

	// Use this for initialization
	void Start () {
		rt = this.GetComponent<RectTransform> ();
	}
	
	// Update is called once per frame
	void Update () {
		rt.anchoredPosition = new Vector2 (Input.mousePosition.x, Input.mousePosition.y);
		if (Input.GetKeyDown(KeyCode.Mouse0) ){
			RaycastHit hit;
			if(Physics.Raycast(Cam.ScreenPointToRay(Input.mousePosition), out hit,8)){
				hit.collider.gameObject.GetComponent<Enemy>().Health--;
			}
		}
	}

	void Shoot(){
		
	}
}
