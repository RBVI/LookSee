using UnityEngine;
using UnityEngine.EventSystems; 	 	// use EventSystem
using UnityEngine.InputSystem;			// use InputAction
using UnityEngine.InputSystem.Utilities;	// use device.usages.Contains()
using MoveModel;				// Use ModelMover
using TMPro;					// Use TextMeshProUGUI

public class MoveModelWithController : MonoBehaviour
{
    public LoadModels models;
    public Wands wands;			    // Used for picking model being pointed at.
    public GameObject debug;

    ModelMover left_wand_mover = new ModelMover();
    ModelMover right_wand_mover = new ModelMover();

    public void Update()
    {
      if (left_wand_mover.gripped)
        drag_model(wands.left_wand.transform, left_wand_mover, right_wand_mover);
      if (right_wand_mover.gripped)
        drag_model(wands.right_wand.transform, right_wand_mover, left_wand_mover);
    }
    
    public void GrabModel(InputAction.CallbackContext context)
    {
      if (!context.performed)
        return;

      bool pressed = (context.action.ReadValue<float>() > 0);
      if (pressed &&
          EventSystem.current.currentSelectedGameObject != null &&
          EventSystem.current.currentSelectedGameObject.activeInHierarchy)
        return;  // Don't drag if a button is selected and UI is shown.
	
      Wand wand;
      ModelMover wand_mover, other_wand_mover;
      if (!which_wand(context, out wand, out wand_mover, out other_wand_mover))
        return;

      Vector3 pick_direction = wand.transform.up, pick_origin = wand.transform.position;
      if (pressed)
	  wand_mover.grip_model(pick_direction, pick_origin, models.open_models);
      else
	  wand_mover.ungrip_model();

//      debug.GetComponentInChildren<TextMeshProUGUI>().text = "Grab " + context.action.name + " " + context.action.phase;
//     Debug.Log("Grab " + context.action.name + " " + context.action.phase + " " + grab);

      // I tried having grab and release be different Input Actions in Unity 2022.2.5f1 but
      // could never get it to call the release action on button release.  It appeared that
      // press and release bindings had to be part of the same action to get it to work.
    }

    void drag_model(Transform transform, ModelMover wand_mover, ModelMover other_wand_mover)
    {
//      debug.GetComponentInChildren<TextMeshProUGUI>().text = "Drag to " + transform.position;
      if (scaling())
        wand_mover.pinch_scale(transform, other_wand_mover);
      else
        wand_mover.move(transform);
    }

    bool scaling()
    {
      // If both wand buttons pressed and dragging same model, then scale.
      return (left_wand_mover.gripped &&
              right_wand_mover.gripped &&
              left_wand_mover.drag_transform == right_wand_mover.drag_transform);
    }

    // Scaling model using controller thumbstick.
    public void ScaleModel(InputAction.CallbackContext context)
    {
      Wand wand;
      ModelMover wand_mover, other_wand_mover;
      if (!which_wand(context, out wand, out wand_mover, out other_wand_mover))
        return;
      if (wand_mover.pick_model(models.open_models, wand.transform.up, wand.transform.position))
      {
        Vector2 stick = context.action.ReadValue<Vector2>();
	if (Mathf.Abs(stick.y) > Mathf.Abs(stick.x))
	{
          float factor = Mathf.Exp(-stick.y / 100.0f);
          wand_mover.scale_model(factor);
	}
      }
    }
    
    bool which_wand(InputAction.CallbackContext context,
	            out Wand wand,
                    out ModelMover wand_mover,
		    out ModelMover other_wand_mover)
    {
      if (is_left_controller(context.control.device))
        {
	  wand = wands.left_wand;
	  wand_mover = left_wand_mover;
	  other_wand_mover = right_wand_mover;
	  return true;
	}
      else if (is_right_controller(context.control.device))
        {
	  wand = wands.right_wand;
	  wand_mover = right_wand_mover;
	  other_wand_mover = left_wand_mover;
	  return true;
	}

      wand = null;
      wand_mover = other_wand_mover = null;
      return false;
    }

    bool is_left_controller(InputDevice device)
    {
	return device.usages.Contains(UnityEngine.InputSystem.CommonUsages.LeftHand);
    }

    bool is_right_controller(InputDevice device)
    {
	return device.usages.Contains(UnityEngine.InputSystem.CommonUsages.RightHand);
    }
}
