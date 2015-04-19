using UnityEngine;
using System.Collections;
using WiimoteApi;

public class PerspectiveShifter : MonoBehaviour {

    private Vector2 offset = new Vector2(0, 0);
    public float sensitivity = 3;
    public Camera camera;
    public bool cameraAboveScreen = false;
    private float headDist = 1;
    private float defaultHeadDist = 0;

    public float dotDistanceInM = 0.155575f;
    public float screenHeightInM = 0.2667f;
    public const float radiansPerPixel = (float)(Mathf.PI / 6) / 1024.0f; //30 degree field of view with a 1024x768 camera
    public float movementScaling = 1f;
    //public float zScaling = 0.5f;
    public bool disableZCalib = true;

    private float relativeVerticalAngle = 0;
    private float cameraVerticalAngle = 0;

    private Wiimote wiimote;

    void Update()
    {
        if (WiimoteOptions.WaitingForGunUpdate) return;

        wiimote = WiimoteOptions.HeadTrackingRemote;

        InterperetWiimoteData(wiimote);

        camera.transform.localPosition += ((new Vector3(offset.x, -offset.y, (headDist - defaultHeadDist)) * -1) - camera.transform.localPosition) * 0.4f;

        float nearPlane = camera.nearClipPlane;
        float screenAspect = camera.aspect;
        camera.projectionMatrix = PerspectiveOffCenter(
            nearPlane * (-.5f * screenAspect + offset.x) / headDist,
            nearPlane * (.5f * screenAspect + offset.x) / headDist,
            nearPlane * (-.5f - offset.y) / headDist,
            nearPlane * (.5f - offset.y) / headDist,
            nearPlane, camera.farClipPlane);
    }

    public void InterperetWiimoteData(Wiimote remote)
    {
        float[,] ir = remote.GetProbableSensorBarIR();

        if (ir[0, 0] == -1)
            return;
       
        float dx = ir[0, 0] - ir[1, 0];
        float dy = ir[0, 1] - ir[1, 1];
        float pointDist = (float)Mathf.Sqrt(dx * dx + dy * dy);

        float angle = radiansPerPixel * pointDist / 2;
        //in units of screen hieght since the box is a unit cube and box hieght is 1
        headDist = movementScaling * (float)((dotDistanceInM / 2) / Mathf.Tan(angle)) / screenHeightInM;
        //Debug.Log(headDist + "");

        float avgX = (ir[0,0] + ir[1,0]) / 2.0f - 512;
        float avgY = (ir[0,1] + ir[1,1]) / 2.0f - 384;

        float rotation = Mathf.Atan2(wiimote.accel[2], wiimote.accel[0]) - (float)(Mathf.PI / 2.0f);
        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);
        avgX = avgX * cos - avgY * sin + 512;
        avgY = avgX * sin - avgY * cos + 384;


        //should  calaculate based on distance

        offset.x = (float)(movementScaling * Mathf.Sin(radiansPerPixel * (avgX - 512)) * headDist);

        relativeVerticalAngle = (avgY - 384) * radiansPerPixel;//relative angle to camera axis

        offset.y = (cameraAboveScreen ? -.5f : .5f) + (float)(movementScaling * Mathf.Sin(relativeVerticalAngle + cameraVerticalAngle) * headDist);
    }

    public void SetVanishingPoint (Camera cam, Vector2 perspectiveOffset) {
	    Matrix4x4 m = cam.projectionMatrix;
	    float w = 2*cam.nearClipPlane/m.m00;
        float h = 2 * cam.nearClipPlane / m.m11;

        float left = -w / 2 - perspectiveOffset.x;
        float right = left + w;
        float bottom = -h / 2 - perspectiveOffset.y;
        float top = bottom + h;
 
	    cam.projectionMatrix = PerspectiveOffCenter(left, right, bottom, top, cam.nearClipPlane, cam.farClipPlane);
    }
 

    // from http://wiki.unity3d.com/index.php/OffsetVanishingPoint
	public static Matrix4x4 PerspectiveOffCenter (float left, float right, float bottom, float top, float near, float far )
    {
	    float x =  (2.0f * near)		/ (right - left);
	    float y =  (2.0f * near)		/ (top - bottom);
	    float a =  (right + left)		/ (right - left);
	    float b =  (top + bottom)		/ (top - bottom);
	    float c = -(far + near)		    / (far - near);
	    float d = -(2.0f * far * near)  / (far - near);
	    float e = -1.0f;
 
	    Matrix4x4 m = new Matrix4x4();
	    m[0,0] =   x;  m[0,1] =   0;  m[0,2] = a;   m[0,3] =   0;
	    m[1,0] =   0;  m[1,1] =   y;  m[1,2] = b;   m[1,3] =   0;
	    m[2,0] =   0;  m[2,1] =   0;  m[2,2] = c;   m[2,3] =   d;
	    m[3,0] =   0;  m[3,1] =   0;  m[3,2] = e;   m[3,3] =   0;
	    return m;
    }

    public void Calibrate()
    {
        double angle = Mathf.Acos(.5f / headDist) - Mathf.PI / 2;
        cameraVerticalAngle = (float)((angle - relativeVerticalAngle));

        if (disableZCalib)
            defaultHeadDist = 0;
        else
            defaultHeadDist = headDist;
    }
}
