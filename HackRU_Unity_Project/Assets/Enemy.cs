using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour {
	public int Health;
    public float AttackInterval = 1;

	public Transform[] Trail;
	public int PlayerIndex;
	private int last = 0;
	private int next = 1;

	public int enemyIndex;
	private float lastTime = 0;
	private float nextTime = 0;
	private bool hasAwoken = false;
	private bool CanMoveToNextPoint = false;

	public float Distance = 0;
	public float Speed;
	public ScoreScript Score;
	private int alpha;

    private bool Dead = false;
    private bool Active = false;
    private float PreviousAttackTime = 0;

    [SerializeField]
    private Animator m_Animator;
	// Use this for initialization
	void Start () {
		Trail [0].position = transform.position;
        //transform.LookAt(Trail[1].position);
		
		CanMoveToNextPoint = false;
	}
	
	// Update is called once per frame
	void Update () {

        m_Animator.SetBool("Dead", Dead);
        if(!Dead)
            transform.LookAt(Trail[next].position);

		PlayerIndex = FollowTrail.Singleton.PlayerIndex;

        if (enemyIndex == FollowTrail.Singleton.ArrivedIndex && !FollowTrail.Singleton.moving && !Dead)
        {
			if(!hasAwoken){
				lastTime = Time.time;
				nextTime = Time.time + Vector3.Distance (Trail [0].position, Trail [1].position) / Speed;
                m_Animator.SetBool("Activated", true);
				hasAwoken = true;
			}
                

			float alpha = Mathf.Clamp01 ((Time.time - lastTime) / (nextTime - lastTime));
			
			transform.position = Vector3.Lerp (Trail [last].position, Trail [next].position, alpha);
			transform.position = new Vector3(this.transform.position.x, 0, this.transform.position.z);
			//transform.rotation = Quaternion.Slerp (Trail [last].rotation, Trail [next].rotation, Mathf.Clamp01(alpha*4));
            
            
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
			
			if (Health <= 0 && !Dead) {
				Score.playerScore+= (int)((1-alpha) * 10000);
                FollowTrail.Singleton.kills++;
                Dead = true;
            }
			
			if(!Dead && alpha >= 1 && Time.time - PreviousAttackTime > AttackInterval){
				Score.playerScore-=5000;
                m_Animator.SetTrigger("Attack");
                PreviousAttackTime = Time.time;
			}
		}
	}

    public void GetHit()
    {
        Health--;
        m_Animator.SetTrigger("GetHit");
    }

	
}
