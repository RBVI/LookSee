using UnityEngine;				// use MonoBehavior
using UnityEngine.InputSystem;			// use InputDevice
using UnityEngine.InputSystem.Utilities;	// use device.usages.Contains()

public class Wands : MonoBehaviour
{
  public Wand left_wand, right_wand;

  public Wand device_wand(InputDevice device)
  {
    if (device.usages.Contains(UnityEngine.InputSystem.CommonUsages.LeftHand))
      return left_wand;
    else if (device.usages.Contains(UnityEngine.InputSystem.CommonUsages.RightHand))
      return right_wand;
    return null;
  }
}
