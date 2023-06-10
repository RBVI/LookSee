using UnityEngine;				// use MonoBehavior

public class Headset : MonoBehaviour
{
  public GameObject eye_center;

  public Vector3 eye_position()
  {
    return eye_center.transform.position;
  }

  public Vector3 view_direction()
  {
    return eye_center.transform.rotation * Vector3.forward;
  }
}
