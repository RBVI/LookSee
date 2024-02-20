using UnityEngine;
using MoveModel;				// Use ModelMover

public class MoveModelWithHand : MonoBehaviour
{
    public LoadModels models;
    public Headset headset;
    public OVRHand left_hand, right_hand;

    ModelMover left_hand_mover = new ModelMover();
    ModelMover right_hand_mover = new ModelMover();

    public void Update()
    {
      update_hand(left_hand, left_hand_mover, right_hand, right_hand_mover);
      update_hand(right_hand, right_hand_mover, left_hand, left_hand_mover);
    }

    bool update_hand(OVRHand hand, ModelMover hand_mover, OVRHand other_hand, ModelMover other_hand_mover)
    {
      if (!hand.IsPointerPoseValid)
        return false;

      Transform hand_transform = hand.PointerPose;
      bool pinch = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
      if (pinch != hand_mover.gripped)
      {
        Vector3 pick_origin = headset.eye_position();
        Vector3 pick_direction = hand_transform.position - pick_origin;
        hand_mover.grip_model(pinch, pick_direction, pick_origin, models.open_models);
      }

      if (hand_mover.gripped)
        drag_model(hand_transform, hand_mover, other_hand_mover);

      return true;
    }

    void drag_model(Transform transform, ModelMover model_mover, ModelMover other_model_mover)
    {
      if (scaling())
        model_mover.pinch_scale(transform, other_model_mover);
      else
        model_mover.move(transform);
    }

    bool scaling()
    {
      // If both wand buttons pressed and dragging same model, then scale.
      return (left_hand_mover.gripped &&
              right_hand_mover.gripped &&
              left_hand_mover.drag_transform == right_hand_mover.drag_transform);
    }
}
