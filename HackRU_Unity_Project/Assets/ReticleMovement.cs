using UnityEngine;
using System.Collections;
using WiimoteApi;

public class ReticleMovement : MonoBehaviour {
	public Camera Cam;
    public Transform EnemyParticlePrefab,OtherParticlePrefab;
	
	RectTransform rt;

    private bool bdown = false;

	// Use this for initialization
	void Start () {
		rt = this.GetComponent<RectTransform> ();
	}
	
	// Update is called once per frame
	void Update () {
        if (!WiimoteOptions.WaitingForGunUpdate)
        {
            float[] pos = WiimoteOptions.GunRemote.GetPointingPosition();
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, new Vector2(pos[0] * Screen.width, pos[1] * Screen.height), 0.5f);
        } else
            rt.anchoredPosition = new Vector2(Input.mousePosition.x, Input.mousePosition.y);

        if (Input.GetKeyDown(KeyCode.Mouse0) || (!WiimoteOptions.WaitingForGunUpdate && !bdown && WiimoteOptions.GunRemote.b))
        {
            RaycastHit hit;
            Vector2 screenPos = Input.mousePosition;
            if (!WiimoteOptions.WaitingForGunUpdate)
            {
                float[] pos = WiimoteOptions.GunRemote.GetPointingPosition();
                screenPos = new Vector2(pos[0] * Screen.width, pos[1] * Screen.height);
            }

            if (Physics.Raycast(Cam.ScreenPointToRay(screenPos), out hit, Mathf.Infinity, 1 << 8))
            {
                hit.collider.GetComponentInParent<Enemy>().GetHit();
                Transform t = Instantiate(EnemyParticlePrefab) as Transform;
                t.position = hit.point;
                t.rotation = Quaternion.LookRotation(hit.normal);
                t.position += hit.normal * 0.1f;
            }

            if (Physics.Raycast(Cam.ScreenPointToRay(screenPos), out hit, Mathf.Infinity, ~(1 << 8)))
            {
                Transform t = Instantiate(OtherParticlePrefab) as Transform;
                t.position = hit.point;
                t.rotation = Quaternion.LookRotation(hit.normal);
                t.position += hit.normal * 0.1f;
            }
        }
        if (!WiimoteOptions.WaitingForGunUpdate)
            bdown = WiimoteOptions.GunRemote.b;
	}

	void Shoot(){
		
	}
}
