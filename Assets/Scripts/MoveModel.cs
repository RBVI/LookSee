using UnityEngine;				// use Vector3, Transform
using UnityEngine.InputSystem.XR; 		// use PoseState
using static ModelUtilities;			// use closest_model(), model_center()

namespace MoveModel
{

public class ModelMover
{
    // Moving model with each hand controller
    public Transform drag_transform;
    public bool gripped = false;
    public bool have_last_pose = false;
    public PoseState last_hand_pose;

    public void grip_model(bool grip, Vector3 pick_direction, Vector3 pick_origin, Models models)
    {
      if (!gripped && grip)
      {
        pick_model(models, pick_direction, pick_origin);
        have_last_pose = false;
      }
     gripped = grip;
    }

    public bool pick_model(Models models, Vector3 pick_direction, Vector3 pick_origin)
    {
      if (models.keep_aligned)
      {
        GameObject p = models.parent();
	if (p != null)
	  drag_transform = p.transform;
      }
      else
      {
        Model m = closest_model(models, pick_origin, pick_direction);
	if (m != null)
	  drag_transform = m.model_object.transform;
      }

      return (drag_transform != null);
    }

    public void move(Transform transform)
    {
      if (!gripped || drag_transform == null)
        return;

      PoseState pose = new PoseState();  
      pose.position = transform.position;
      pose.rotation = transform.rotation;

      if (have_last_pose)
      {
        Vector3 scale = Vector3.one;
	Transform t = drag_transform;
    	Matrix4x4 new_model_transform = (
	  Matrix4x4.TRS(pose.position, pose.rotation, scale)
	   * Matrix4x4.TRS(last_hand_pose.position, last_hand_pose.rotation, scale).inverse
	   * Matrix4x4.TRS(t.position, t.rotation, scale));
	t.position = new_model_transform.GetPosition();
	t.rotation = new_model_transform.rotation;
      }
      last_hand_pose = pose;
      have_last_pose = true;
    }

    public void pinch_scale(Transform transform, ModelMover other_hand_mover)
    {
      if (!gripped || drag_transform == null)
        return;

      PoseState pose = new PoseState();  
      pose.position = transform.position;
      pose.rotation = transform.rotation;

      if (have_last_pose && other_hand_mover.have_last_pose)
      {
        Vector3 other_hand_pos = other_hand_mover.last_hand_pose.position;
	float separation = (pose.position - other_hand_pos).magnitude;
	float last_separation = (last_hand_pose.position - other_hand_pos).magnitude;
	if (last_separation > 0)
	{
	  float factor = separation / last_separation;
	  scale_model(factor);
	}
      }

      last_hand_pose = pose;
      have_last_pose = true;
    }

    // Scale about the model center.
    public void scale_model(float factor)
    {
      if (drag_transform == null)
        return;
      Vector3 c = model_center(drag_transform.gameObject);
      Transform t = drag_transform;
      t.position = c + factor * (t.position - c);
      t.localScale *= factor;
    }

}

}