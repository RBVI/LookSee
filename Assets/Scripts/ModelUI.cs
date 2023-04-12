using UnityEngine;
using UnityEngine.InputSystem;	   // use InputAction
using UnityEngine.UI;		   // use Toggle
using System;			   // use DateTime
using System.IO;		   // use Path
using System.Threading.Tasks;  	   // Use Task
using TMPro; 			   // use TextMeshProUGUI

public class ModelUI : MonoBehaviour
{
    public GameObject hide_show_close_prefab;
    public GameObject open_file_prefab;
    public GameObject no_files_message;
    public GameObject options;
    public float check_for_files_interval = 5.0f;	// seconds
    public bool open_new = false;
    public Toggle open_new_toggle;
    public LoadModels load_models;
    public FileReceiver file_receiver;
    public OVRPassthroughLayer pass_through;
    public GameObject table;
    public Wands wands;				// Used to position UI panel on button click.
    
    void Start()
    {
      no_files_message.GetComponent<TextMeshProUGUI>().text += " " + Application.persistentDataPath;
      update_ui_controls();

      InvokeRepeating("open_new_files", check_for_files_interval, check_for_files_interval);
    }
    
    public void ShowOrHideUI(InputAction.CallbackContext context)
    {
//      GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text += " " + context.phase + " " + context.ReadValueAsButton();
      if (!context.performed)
        return;
      bool show = !gameObject.activeSelf;
      if (show)
      {
        update_ui_controls();
	position_ui_panel(context.control.device);
      }
      gameObject.SetActive(show);
    }

    public void update_ui_controls()
    {
      // Destroy current buttons.
      foreach (Transform child in transform)
	if (child.gameObject.tag == "dynamicUI")
          GameObject.Destroy(child.gameObject);

      // Make new Hide, Show, Close buttons.
      float y = 0.10f;	// meters
      foreach (Model model in load_models.open_models.models)
	{
	  GameObject row = Instantiate(hide_show_close_prefab,
	                               new Vector3(0f,y,0f), Quaternion.identity);
	  row.transform.SetParent(gameObject.transform, false);
	  foreach(ButtonSelect bs in row.GetComponentsInChildren<ButtonSelect>())
	    bs.model = model.model_object;
          string filename = Path.GetFileName(model.path);
	  GameObject name = row.transform.Find("Name").gameObject;
	  name.GetComponentInChildren<TextMeshProUGUI>().text = filename;
	  y += 0.07f;
	}

//      options.transform.localPosition = new Vector3(0f,y,0f);
//      y -= 0.1f;

      // Make new Open buttons.
      string[] files = load_models.gltf_file_paths();
      y = -0.08f;
      int h = (files.Length + 1) / 2, count = 0;
      foreach (string path in files)
	{
	  // Position buttons in 2 columns.
	  float x = (count >= h ? 0.4f : 0f);
	  if (count == h)
	    y += h * 0.06f;
          count += 1;
	  GameObject row = Instantiate(open_file_prefab,
	                               new Vector3(x,y,0f), Quaternion.identity);
	  row.transform.SetParent(gameObject.transform, false);
	  ButtonSelect bs = row.GetComponentInChildren<ButtonSelect>();
	  bs.file_path = path;
          string filename = Path.GetFileNameWithoutExtension(path);
	  GameObject name = row.transform.Find("Name").gameObject;
	  name.GetComponentInChildren<TextMeshProUGUI>().text = filename;
	  y -= 0.06f;
	}

      if (files.Length == 0)
        {
	  y -= 0.1f;
	  no_files_message.transform.localPosition = new Vector3(0f,y,0f);
	  no_files_message.SetActive(true);
	}
      else
	  no_files_message.SetActive(false);
    }

  void position_ui_panel(InputDevice device)
  {
    Wand wand = wands.device_wand(device);
    if (wand == null)
      return;
    Vector3 tip = wand.tip_position();
    transform.position = tip;
    Vector3 look = tip - wand.eye_position();
    look.y = 0;
    transform.rotation = Quaternion.FromToRotation(new Vector3(0,0,1), look);
  }

  public void pass_through_toggled(bool pass)
  {
    pass_through.enabled = pass;
  }
  
  public void open_new_toggled(bool open)
  {
    open_new = open;
    if (open)
      load_models.record_files();
  }
  
  async void open_new_files()
  {
    if (!open_new)
      return;

    int num_opened = await load_models.open_new_files();
    if (num_opened > 0 && gameObject.activeSelf)
      update_ui_controls();
  }

  public void receive_files(bool receive)
  {
    if (receive)
    {
	open_new_toggle.isOn = true;
        file_receiver.StartListening();
    }
    else
        file_receiver.StopListening();
  }

  public void show_table(bool show)
  {
    table.SetActive(show);
  }
}

