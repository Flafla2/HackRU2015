using UnityEngine;
using System.Collections;

public class DancingEnemy : MonoBehaviour {
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
		
		//transform.LookAt(Trail[1].
		m_Animator.SetBool("Dancing", true);
	}
	
	// Update is called once per frame
	void Update () {
		
		

	}

	
	
}
