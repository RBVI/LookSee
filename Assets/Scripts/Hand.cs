using UnityEngine;				// use MonoBehavior

public class Hand : MonoBehaviour
{
    public OVRHand hand;
    public GameObject finger_tip, finger_approach;
    public float button_push_depth = 0.015f;	  // Push 1.5 cm to push a button.
    bool last_pinching = false;

    OVRSkeleton hand_skeleton;

    void Start()
    {
	hand_skeleton = hand.GetComponent<OVRSkeleton>();
    }
    
    void Update()
    {
	position_finger_tip_sphere();  // Update index finger tip sphere position
    }

    void position_finger_tip_sphere()
    {
      bool active = (hand_skeleton.IsDataValid && hand_skeleton.IsValidBone(OVRSkeleton.BoneId.Hand_IndexTip));
      if (active)
      {
	  float approach_distance = button_push_depth;
	  if (is_left_hand())
	      approach_distance = -approach_distance;
	Transform bone_transform = hand_skeleton.Bones[(int)OVRSkeleton.BoneId.Hand_IndexTip].Transform;
	finger_tip.transform.position = bone_transform.position;
	finger_approach.transform.position = bone_transform.position + approach_distance*bone_transform.right;
      }
      finger_tip.SetActive(active);
    }

    public bool is_left_hand()
    {
	return hand_skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.HandLeft;
    }

    public bool is_right_hand()
    {
	return hand_skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.HandRight;
    }
    
    public bool finger_tip_position(out Vector3 position)
    {
      bool active = (hand_skeleton.IsDataValid && hand_skeleton.IsValidBone(OVRSkeleton.BoneId.Hand_IndexTip));
      if (active)
      {
	  Transform bone_transform = hand_skeleton.Bones[(int)OVRSkeleton.BoneId.Hand_IndexTip].Transform;
	  position = bone_transform.position;
      }
      else
	  position = Vector3.zero;
      return active;
    }

    public bool is_pinching()
    {
	return hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
    }

    public bool pinched()
    {
	bool pinching = is_pinching();
	bool pinch = (pinching && !last_pinching);
	last_pinching = pinching;
	return pinch;
    }
    
    public Transform hand_pose()
    {
	/*
	if (hand.IsPointerPoseValid)
	    return hand.PointerPose;
	*/
	// Use skeleton transform since hand.PointerPose seems to be updated only ever few frames
	// in Quest runtime v62.
	if (hand_skeleton.IsDataValid && hand_skeleton.IsValidBone(OVRSkeleton.BoneId.Hand_WristRoot))
	    return hand_skeleton.Bones[(int)OVRSkeleton.BoneId.Hand_WristRoot].Transform;
	return null;
    }
}
