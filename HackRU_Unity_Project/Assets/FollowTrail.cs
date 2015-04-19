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
	public int PlayerIndex = 1;
    public int ArrivedIndex = 0;
	public bool moving=false;
	public float Speed;

    public static FollowTrail Singleton;

	// Use this for initialization
	void Start () {
        if (Singleton == null)
            Singleton = this;
        else
            Destroy(gameObject);

		transform.position = Trail [0].position;
		transform.rotation = Trail [0].rotation;

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

			if (this.transform.position == lastPos) {
				moving = false;
			} else {
				moving = true;
			}

			if (alpha >= 1 && (Input.GetKeyDown (KeyCode.Space) || kills >= Skeletons[PlayerIndex])) {
               // Debug.Log((PlayerIndex+1) + " " + (Skeletons[PlayerIndex]));
				PlayerIndex ++;
				kills = 0;

                last = next;
                next = (next + 1) % Trail.Length;

                lastTime = Time.time;
                nextTime = Time.time + Vector3.Distance(Trail[last].position, Trail[next].position) / Speed;
			} else if(alpha >= 1)
                ArrivedIndex = PlayerIndex;
		}
	}
}
