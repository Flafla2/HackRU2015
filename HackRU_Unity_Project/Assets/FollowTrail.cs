using UnityEngine;
using System.Collections;

public class FollowTrail : MonoBehaviour {
	public Transform[] Trail;
	public int[] Skeletons;
	public int kills = 0;
	public bool isDead = false;
	private int last = 0;
	private int next = 1;
	private float lastTime = 0;
	private float nextTime = 0;
	public int PlayerIndex = 0;
	public bool CanMoveToNextPoint = false,moving=false;
	public float Speed;


	// Use this for initialization
	void Start () {
		transform.position = Trail [0].position;
		transform.rotation = Trail [0].rotation;

		CanMoveToNextPoint = (Trail[1].gameObject.tag.Contains("Intermediary")?true:false);

		lastTime = Time.time;
		nextTime = Time.time + Vector3.Distance (Trail [0].position, Trail [1].position) / Speed;
	}
	
	// Update is called once per frame
	void Update () {
		if (!isDead) {
			Vector3 lastPos = this.transform.position;
			float alpha = Mathf.Clamp01 ((Time.time - lastTime) / (nextTime - lastTime));
			transform.position = Vector3.Lerp (Trail [last].position, Trail [next].position, alpha);
			transform.rotation = Quaternion.Slerp (Trail [last].rotation, Trail [next].rotation, alpha);
			if (CanMoveToNextPoint) {



				if (alpha >= 1 && CanMoveToNextPoint) {
					if (!Trail [(next + 1) % Trail.Length].tag.Contains ("Intermediary")) {
						CanMoveToNextPoint = false;
					}


					last = next;
					next = (next + 1) % Trail.Length;

					lastTime = Time.time;
					nextTime = Time.time + Vector3.Distance (Trail [last].position, Trail [next].position) / Speed;


				}
			}
			if (this.transform.position == lastPos) {
				moving = false;
			} else {
				moving = true;
			}
			if (Input.GetKeyDown (KeyCode.Space) || kills >= Skeletons[PlayerIndex]) {
				PlayerIndex ++;
				kills = 0;
				CanMoveToNextPoint = true;
			}
		}
	}
}
