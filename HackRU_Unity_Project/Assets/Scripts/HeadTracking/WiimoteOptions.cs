using UnityEngine;
using System.Collections;
using WiimoteApi;

public class WiimoteOptions : MonoBehaviour {

    public static Wiimote HeadTrackingRemote
    {
        get { return _HeadTrackingRemote;  }
    }
    private static Wiimote _HeadTrackingRemote;

    public static Wiimote GunRemote
    {
        get { return _GunRemote; }
    }
    private static Wiimote _GunRemote;

    public static bool WaitingForGunUpdate {
        get { return _WaitingForGunUpdate; }
    }
    public static bool _WaitingForGunUpdate = true;

    public PerspectiveShifter perspective;

    public static float WiimoteReadInterval = 1f / 30f;
    private static float LastWiimoteReadTime = 0;

    void Start()
    {
        //WiimoteManager.Debug_Messages = true;
    }

    void Update()
    {
        if (Time.time - LastWiimoteReadTime > WiimoteReadInterval)
        {
            foreach (Wiimote remote in WiimoteManager.Wiimotes)
            {
                int ret;
                do
                {
                    ret = WiimoteManager.ReadWiimoteData(remote);
                } while (ret > 0);
            }
            LastWiimoteReadTime = Time.time;
        }
        

        if (WaitingForGunUpdate && WiimoteManager.Wiimotes.Count >= 2)
        {
            for (int x = 0; x < WiimoteManager.Wiimotes.Count; x++ )
            {
                if (WiimoteManager.Wiimotes[x].b)
                {
                    int other = 1;
                    if(x == 1) other = 0;
                    _GunRemote = WiimoteManager.Wiimotes[x];
                    _HeadTrackingRemote = WiimoteManager.Wiimotes[other];

                    x = WiimoteManager.Wiimotes.Count;
                    _WaitingForGunUpdate = false;

                    WiimoteManager.SendPlayerLED(_GunRemote, true, true, true, false);
                    WiimoteManager.SendPlayerLED(_HeadTrackingRemote, true, false, false, true);

                    WiimoteManager.SetupIRCamera(_GunRemote, WiimoteManager.IRDataType.BASIC);
                    WiimoteManager.SetupIRCamera(_HeadTrackingRemote, WiimoteManager.IRDataType.EXTENDED);
                }
            }
        }

        if (!WaitingForGunUpdate)
        {
            if (GunRemote.a && perspective != null)
                perspective.Calibrate();

            if (GunRemote.one && GunRemote.two && GunRemote.home)
            {
                Application.LoadLevel("MainMenu");
            }
        }
    }

    public static void RedefineRemoteIDs()
    {
        _WaitingForGunUpdate = true;
    }
}
