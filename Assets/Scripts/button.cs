using UnityEngine;
using UnityEngine.EventSystems;  // use EventSystem
using System;			   // use DateTime
using System.Collections;
using TMPro;  // Use TextMeshProUGUI
using UnityEngine.InputSystem;			// use InputAction
using UnityEngine.UI;		// use Toggle

public class ButtonSelect : MonoBehaviour
{
    public InputActionAsset input_actions;
    public GameObject model;
    public string file_path;
    public ModelUI ui;
    LoadModels load_models;
    
    void Start()
    {
      // Make button get trigger press events.
      InputActionMap action_map = input_actions.FindActionMap("Player");
      InputAction click = action_map.FindAction("ClickButton");
      click.performed += ClickButton;
      load_models = GameObject.Find("Models").GetComponent<LoadModels>();
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
      {
        EventSystem.current.SetSelectedGameObject(gameObject);
//        gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "enter";
      }
    }

    void OnTriggerExit(Collider collider)
    {
      if (collider.tag == "Wand")
      {
        EventSystem.current.SetSelectedGameObject(null);
//        gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "exit";
      }
    }

    public async void ClickButton(InputAction.CallbackContext context)
    {
      if (!context.performed)
        return;

      if (EventSystem.current.currentSelectedGameObject != gameObject)
        return;

//      Debug.Log("clicked button " + gameObject.name);

      string button_name = gameObject.name;
      if (button_name == "Hide button" && model != null)
        model.SetActive(false);
      if (button_name == "Show button" && model != null)
        model.SetActive(true);
      if (button_name == "Close button" && model != null)
      {
        load_models.open_models.remove_game_object(model);
	model = null;
      }
      if (button_name == "Open button" && file_path != null)
      {
        await load_models.load_gltf_file(file_path);
      }
      if (button_name == "Show room")
      {
	Toggle t = gameObject.GetComponent<Toggle>();
        bool show = !t.isOn;
	t.isOn = show;
        GameObject camera = GameObject.Find("OVRCameraRig");
	camera.GetComponent<OVRPassthroughLayer>().enabled = show;
      }
      if (button_name == "Show table")
      {
      	Toggle t = gameObject.GetComponent<Toggle>();
        bool show = !t.isOn;
	t.isOn = show;
	model.SetActive(show);  // model is the Table GameObject
      }
      if (button_name == "Open new files")
      {
       	Toggle t = gameObject.GetComponent<Toggle>();
        bool open = !t.isOn;
	t.isOn = open;
	ui.open_new = open;
	if (open)
	  load_models.record_files();
      }
    }
}

