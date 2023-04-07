using UnityEngine;				// use MonoBehavior

public class Wand : MonoBehaviour
{
  public GameObject eye_center;

  public Vector3 tip_position()
  {
    // Wand is cylinder along y-axis.
    return transform.TransformPoint(new Vector3(0,1,0));
  }

  public Vector3 eye_position()
  {
    return eye_center.transform.position;
  }
}
