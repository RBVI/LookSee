using UnityEngine;
using MoveModel;				// Use ModelMover

public class MoveModelWithHand : MonoBehaviour
{
    public LoadModels models;
    public Headset headset;
    public Hand left_hand, right_hand;
    public Meeting meeting;  // Used for placing meeting coordinate alignment markers.

    ModelMover left_hand_mover = new ModelMover();
    ModelMover right_hand_mover = new ModelMover();

    public void Update()
    {
      if (meeting.setting_room_coordinates())
      {
	place_meeting_alignment_marker(left_hand);
	place_meeting_alignment_marker(right_hand);
      }
      else
      {
        move_model_with_hand(left_hand, left_hand_mover, right_hand, right_hand_mover);
        move_model_with_hand(right_hand, right_hand_mover, left_hand, left_hand_mover);
      }
    }

    bool move_model_with_hand(Hand hand, ModelMover hand_mover, Hand other_hand, ModelMover other_hand_mover)
    {
      Transform hand_transform = hand.pointer_pose();
      if (hand_transform == null)
	  return false;

      bool pinching = hand.is_pinching();
      if (pinching && !hand_mover.gripped)
      {
        Vector3 pick_origin = headset.eye_position();
        Vector3 pick_direction = hand_transform.position - pick_origin;
        hand_mover.grip_model(pick_direction, pick_origin, models.open_models);
      }
      else if (!pinching && hand_mover.gripped)
	hand_mover.ungrip_model();
      
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

    bool place_meeting_alignment_marker(Hand hand)
    {
      if (!hand.pinched())
	return false;

      Vector3 position;
      if (hand.finger_tip_position(out position))
      {
	  int marker_number = (hand.is_right_hand() ? 1 : 2);
	  meeting.drop_coordinate_marker(marker_number, position);
	  return true;
      }

      return false;
    }
}
