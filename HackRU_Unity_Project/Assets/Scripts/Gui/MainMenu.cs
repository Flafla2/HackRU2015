using UnityEngine;
using System.Collections;
using WiimoteApi;
using UnityEngine.EventSystems;

public class MainMenu : MonoBehaviour {

    [SerializeField]
    private Animator m_MenuAnimator;
    [SerializeField]
    private float m_UpdateRate = 1;
    [SerializeField]
    private NunchuckInputModule m_InputModule;

    private float LastWiimoteUpdate;
    private bool HasWiimotes;
    private bool HasSetupGun;

    void Start()
    {
        WiimoteManager.FindWiimote(true);
        LastWiimoteUpdate = Time.time;
    }

	// Update is called once per frame
	void Update () {
        if (Time.time - LastWiimoteUpdate > m_UpdateRate)
        {
            WiimoteManager.FindWiimote(false);
            LastWiimoteUpdate = Time.time;
        }

        HasWiimotes = WiimoteManager.Wiimotes.Count >= 2;
        HasSetupGun = HasWiimotes && !WiimoteOptions.WaitingForGunUpdate;

        if (!HasWiimotes)
            WiimoteOptions.RedefineRemoteIDs();

        m_MenuAnimator.SetBool("HasWiimotes", HasWiimotes);
        m_MenuAnimator.SetBool("HasSetupGun", HasSetupGun);

        if (HasSetupGun)
            m_InputModule.wiimote = WiimoteOptions.GunRemote;
	}
}
