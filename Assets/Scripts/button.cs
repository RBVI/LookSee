using System;			   	// use Action and Func
using System.Threading.Tasks;  	   	// Use Task
using UnityEngine;
using UnityEngine.EventSystems;  	// use EventSystem
using UnityEngine.Events;		// Use UnityEvent
using UnityEngine.InputSystem;		// use InputAction
using UnityEngine.UI;			// use Toggle
using TMPro; 			   // use TextMeshProUGUI

//
// ButtonSelect is makes user interface buttons that select when a VR hand controller
// is touching the button and runs some code when the VR hand controller trigger is
// pulled.
//
public class ButtonSelect : MonoBehaviour
{
    public Action<string> action;		// Action to perform on button press
    public Func<Task> async_action;		// Async action to perform on button press.
    public InputActionAsset input_actions;	// To get VR controller button events.
    public UnityEvent<string> event_action;	// Settable in Unity editor.
    
    void Start()
    {
      // Make button get trigger press events.
      InputActionMap action_map = input_actions.FindActionMap("Player");
      InputAction click = action_map.FindAction("ClickButton");
      click.performed += ClickButton;
    }

    void OnDestroy()
    {
      // Make button not get trigger press events.
      InputActionMap action_map = input_actions.FindActionMap("Player");
      InputAction click = action_map.FindAction("ClickButton");
      click.performed -= ClickButton;
    }

    void OnTriggerEnter(Collider collider)
    {
      if (collider.tag == "Wand")
        EventSystem.current.SetSelectedGameObject(gameObject);
    }

    void OnTriggerExit(Collider collider)
    {
      if (collider.tag == "Wand")
        EventSystem.current.SetSelectedGameObject(null);
    }

    public async void ClickButton(InputAction.CallbackContext context)
    {
      if (!gameObject.activeInHierarchy)
        return;
	
      if (!context.performed)
        return;

      if (EventSystem.current.currentSelectedGameObject != gameObject)
        return;
      
      string button_name = gameObject.name;
//      GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Button name " + gameObject.name;
      if (event_action != null)
          event_action.Invoke(button_name);
      if (action != null)
          action(button_name);
      if (async_action != null)
          await async_action();

      // Toggle any clicked toggle button.
      Toggle t = gameObject.GetComponent<Toggle>();
      if (t != null)
      {
        bool enable = !t.isOn;
        t.isOn = enable;
      }
    }
}

