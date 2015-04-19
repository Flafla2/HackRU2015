using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour {
	public int Health;
	public Transform[] Trail;
	public int PlayerIndex;
	public FollowTrail FollowTrail;
	private int last = 0;
	private int next = 1;
	public int enemyIndex;
	private float lastTime = 0;
	private float nextTime = 0;
	private bool hasAwoken = false;
	public bool CanMoveToNextPoint = false;
	public float Distance = 0;
	public float Speed;
	public ScoreScript Score;
	int alpha;
	// Use this for initialization
	void Start () {
		//transform.position = Trail [0].position;
		//transform.rotation = Trail [0].rotation;

		Trail [0].position = transform.position;
		
		CanMoveToNextPoint = false;
		
	
	}
	
	// Update is called once per frame
	void Update () {
		if (!GetComponent<Animation> ().isPlaying) {
			GetComponent<Animation> ().Play();
		}
	
		PlayerIndex = FollowTrail.PlayerIndex;
		if (enemyIndex == PlayerIndex && FollowTrail.moving==false) {
			if(hasAwoken==false){
				lastTime = Time.time;
				nextTime = Time.time + Vector3.Distance (Trail [0].position, Trail [1].position) / Speed;
				hasAwoken = true;
			}
			CanMoveToNextPoint = true;
			float alpha = Mathf.Clamp01 ((Time.time - lastTime) / (nextTime - lastTime));
			print (alpha);
			transform.position = Vector3.Lerp (Trail [last].position, Trail [next].position, alpha);
			transform.position = new Vector3(this.transform.position.x, 0, this.transform.position.z);
			transform.rotation = Quaternion.Slerp (Trail [last].rotation, Trail [next].rotation, alpha);
			if (CanMoveToNextPoint) {
				
				if (alpha >= 1) {
					if(!Trail[(next+1)%Trail.Length].tag.Contains("Intermediary")){
						CanMoveToNextPoint = false;
					}
					last = next;
					next = (next + 1) % Trail.Length;
					lastTime = Time.time;
					nextTime = Time.time + Vector3.Distance (Trail [last].position, Trail [next].position) / Speed;		
				}
			}
			
			if (Health <= 0) {
				
				GetComponent<Animation>().clip = GetComponent<Animation>()["die"].clip;
				
				print(GetComponent<Animation>().clip.name);
				this.gameObject.SetActive(false);
				Score.playerScore+= (int)((1-alpha) * 10000);
				print((int)((1-alpha) * 10000));
				FollowTrail.kills++;
				Destroy (this);
			}
			
			
			if(last == Trail.Length-1){
				print ("moist");
				Score.playerScore-=5000;
				this.gameObject.SetActive(false);
				FollowTrail.kills++;
				Destroy (this);
			}
		}

	}

	
}
