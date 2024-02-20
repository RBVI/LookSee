using UnityEngine;				// use MonoBehavior

public class HandInput : MonoBehaviour
{
   public ModelUI model_ui;
   public OVRSkeleton left_hand_skeleton, right_hand_skeleton;
   public GameObject left_finger_tip, left_finger_approach, right_finger_tip, right_finger_approach;
   float button_push_depth = 0.015f;

   void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.Hands))
	{
	  float offset = 0.20f;  // Place panel 20 cm behind hand
          model_ui.position_ui_panel_at_point(left_finger_tip.transform.position, offset);
	  model_ui.show_ui(!model_ui.ui_shown());
        }

        // Update index finger sphere position
	position_finger_tip(left_finger_tip, left_finger_approach, -button_push_depth, left_hand_skeleton);
        position_finger_tip(right_finger_tip, right_finger_approach, button_push_depth, right_hand_skeleton);
     }

   void position_finger_tip(GameObject finger_tip, GameObject finger_approach, float approach_distance,
                            OVRSkeleton hand_skeleton)
   {
      bool active = (hand_skeleton.IsDataValid && hand_skeleton.IsValidBone(OVRSkeleton.BoneId.Hand_IndexTip));
	if (active)
	{
	  Transform bone_transform = hand_skeleton.Bones[(int)OVRSkeleton.BoneId.Hand_IndexTip].Transform;
	  finger_tip.transform.position = bone_transform.position;
	  finger_tip.transform.rotation = bone_transform.rotation;
	  finger_approach.transform.position = bone_transform.position + approach_distance*bone_transform.right;
        }
        finger_tip.SetActive(active);
    }
}
