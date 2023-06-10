using UnityEngine;				// use MonoBehavior

public class Wand : MonoBehaviour
{
  public Vector3 tip_position()
  {
    // Wand is cylinder along y-axis.
    return transform.TransformPoint(new Vector3(0,1,0));
  }
}
