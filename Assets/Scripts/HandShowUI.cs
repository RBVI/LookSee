using UnityEngine;				// use MonoBehavior

//
// Make left hand menu button pinch show or hide user interface panels.
//
public class HandShowUI : MonoBehaviour
{
   public ModelUI model_ui;
   public Hand hand;
   public float ui_offset = 0.20f;		  // Place panel 20 cm behind hand

   void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.Hands))
	{
          if (!model_ui.ui_shown())
	  {
	      Vector3 position;
	      if (hand.finger_tip_position(out position))
		  model_ui.position_ui_panel_at_point(position, ui_offset);
	  }
	  model_ui.show_ui(!model_ui.ui_shown());
	}
    }
}
