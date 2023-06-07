using UnityEngine;
using UnityEngine.InputSystem;			// use InputAction
using UnityEngine.InputSystem.Utilities;	// use device.usages.Contains()
using UnityEngine.InputSystem.XR; 		// use PoseState
using static ModelUtilities;			// use closest_child()
using TMPro;  // Use TextMeshProUGUI

public class PositionModel : MonoBehaviour
{
    public GameObject models;			// Children are movable models.
    public GameObject left_wand, right_wand;    // Used for picking model being pointed at.
    public GameObject table_model;
    public GameObject debug;

    WandModelMover left_wand_mover = new WandModelMover();
    WandModelMover right_wand_mover = new WandModelMover();

    public void Update()
    {
      if (left_wand_mover.button_pressed)
        drag_model(left_wand, left_wand_mover, right_wand_mover);
      if (right_wand_mover.button_pressed)
        drag_model(right_wand, right_wand_mover, left_wand_mover);
    }
    
    public void GrabModel(InputAction.CallbackContext context)
    {
      if (!context.performed)
        return;

      GameObject wand;
      WandModelMover wand_mover, other_wand_mover;
      if (!which_wand(context, out wand, out wand_mover, out other_wand_mover))
        return;

      bool pressed = (context.action.ReadValue<float>() > 0);
      wand_mover.set_button_pressed(pressed, wand, models);

//      debug.GetComponentInChildren<TextMeshProUGUI>().text = "Grab " + context.action.name + " " + context.action.phase;
//     Debug.Log("Grab " + context.action.name + " " + context.action.phase + " " + grab);

      // I tried having grab and release be different Input Actions in Unity 2022.2.5f1 but
      // could never get it to call the release action on button release.  It appeared that
      // press and release bindings had to be part of the same action to get it to work.
    }

    void drag_model(GameObject wand, WandModelMover wand_mover, WandModelMover other_wand_mover)
    {
      PoseState pose = new PoseState();  
      pose.position = wand.transform.position;
      pose.rotation = wand.transform.rotation;
//      debug.GetComponentInChildren<TextMeshProUGUI>().text = "Drag to " + pose.position;
      if (scaling())
        wand_mover.pinch_scale(pose, other_wand_mover);
      else
        wand_mover.move(pose);
    }

    bool scaling()
    {
      // If both wand buttons pressed and dragging same model, then scale.
      return (left_wand_mover.button_pressed &&
              right_wand_mover.button_pressed &&
              left_wand_mover.drag_model == right_wand_mover.drag_model);
    }

    // Scaling model using controll thumbstick.
    public void ScaleModel(InputAction.CallbackContext context)
    {
      GameObject wand;
      WandModelMover wand_mover, other_wand_mover;
      if (!which_wand(context, out wand, out wand_mover, out other_wand_mover))
        return;
      if (wand_mover.pick_model(models, wand))
      {
        Vector2 stick = context.action.ReadValue<Vector2>();
        float factor = Mathf.Exp(-stick.y / 100.0f);
        wand_mover.scale_model(factor);
      }
    }

    public void HideOrShowTable(InputAction.CallbackContext context)
    {
      if (context.performed)
        table_model.SetActive(!table_model.activeSelf);
    }

    bool which_wand(InputAction.CallbackContext context,
	            out GameObject wand,
                    out WandModelMover wand_mover,
		    out WandModelMover other_wand_mover)
    {
      if (is_left_controller(context.control.device))
        {
	  wand = left_wand;
	  wand_mover = left_wand_mover;
	  other_wand_mover = right_wand_mover;
	  return true;
	}
      else if (is_right_controller(context.control.device))
        {
	  wand = right_wand;
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

class WandModelMover
{
    // Moving model with each hand controller
    public GameObject drag_model;
    public bool button_pressed = false;
    public bool have_last_pose = false;
    public PoseState last_wand_pose;

    public void set_button_pressed(bool pressed, GameObject wand, GameObject models)
    {
      if (!button_pressed && pressed)
      {
        pick_model(models, wand);
        have_last_pose = false;
      }
     button_pressed = pressed;
    }

    public bool pick_model(GameObject models, GameObject wand)
    {
      drag_model = closest_child(models, wand.transform.position, wand.transform.up);
      return (drag_model != null);
    }

    public void move(PoseState pose)
    {
      if (!button_pressed || drag_model == null)
        return;

      if (have_last_pose)
      {
        Vector3 scale = Vector3.one;
	Transform t = drag_model.transform;
    	Matrix4x4 new_model_transform = (
	  Matrix4x4.TRS(pose.position, pose.rotation, scale)
	   * Matrix4x4.TRS(last_wand_pose.position, last_wand_pose.rotation, scale).inverse
	   * Matrix4x4.TRS(t.position, t.rotation, scale));
	t.position = new_model_transform.GetPosition();
	t.rotation = new_model_transform.rotation;
      }
      last_wand_pose = pose;
      have_last_pose = true;
    }

    public void pinch_scale(PoseState pose, WandModelMover other_wand_mover)
    {
      if (!button_pressed || drag_model == null)
        return;

      if (have_last_pose && other_wand_mover.have_last_pose)
      {
        Vector3 other_wand_pos = other_wand_mover.last_wand_pose.position;
	float separation = (pose.position - other_wand_pos).magnitude;
	float last_separation = (last_wand_pose.position - other_wand_pos).magnitude;
	if (last_separation > 0)
	{
	  float factor = separation / last_separation;
	  scale_model(factor);
	}
      }

      last_wand_pose = pose;
      have_last_pose = true;
    }

    // Scale about the model center.
    public void scale_model(float factor)
    {
      if (drag_model == null)
        return;
      Vector3 c = model_center(drag_model);
      Transform t = drag_model.transform;
      t.position = c + factor * (t.position - c);
      t.localScale *= factor;
    }

}
