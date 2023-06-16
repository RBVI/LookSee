using UnityEngine;
using UnityEngine.InputSystem;	   // use InputAction
using UnityEngine.UI;		   // use Toggle
using System;			   // use DateTime
using System.Collections.Generic;  // use List
using System.IO;		   // use Path
using System.Text;                 // use Encoding
using System.Threading.Tasks;  	   // Use Task
using TMPro; 			   // use TextMeshProUGUI

public class ModelUI : MonoBehaviour
{
    public GameObject hide_show_close_prefab;
    public GameObject open_file_prefab;
    public GameObject no_files_message;
    public GameObject options;
    public GameObject error_panel;
    public TextMeshProUGUI error_text;
    public float check_for_files_interval = 5.0f;	// seconds
    public bool open_new = false;
    public Toggle open_new_toggle;
    public LoadModels load_models;
    public FileReceiver file_receiver;
    public Meeting meeting;
    public GameObject meeting_buttons, meeting_host_status, meeting_join_status;
    public GameObject align_meeting_toggle;
    public GameObject meeting_keypad;		// For entering IP address
    public TMP_InputField ip_address;
    public OVRPassthroughLayer pass_through;
    public GameObject table;
    public Wands wands;				// Used to position UI panel on button click.
    public Headset headset;			// View direction used to position UI panel.
    public float initial_distance = 0.8f;	// How far to place UI in front of eyes, meters.
    private bool initial_position_set = false;
    public LookSeeSettings settings = new LookSeeSettings();
    
    void Start()
    {
      settings.load();
      
      // Position and show UI only after VR headset position and view direction are known.
      OVRManager.TrackingAcquired += set_initial_ui_panel_position_delayed;

      no_files_message.GetComponent<TextMeshProUGUI>().text += " " + Application.persistentDataPath;
      update_ui_controls();

      ip_address.text = settings.meeting_last_join_ip_address;

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
  	  row.transform.SetSiblingIndex(0);  // Stack beneath meeting keypad
	  foreach(ButtonSelect bs in row.GetComponentsInChildren<ButtonSelect>())
	  {
            Action<string> open_show_hide = (string button_name) => {
	       if (button_name == "Hide button")
	          model.model_object.SetActive(false);
	       else if (button_name == "Show button")
	          model.model_object.SetActive(true);
  	       else if (button_name == "Close button")
                  load_models.open_models.remove_model(model);
	       };
	    bs.action = open_show_hide;
	  }
          string filename = Path.GetFileName(model.path);
	  GameObject name = row.transform.Find("Name").gameObject;
	  name.GetComponentInChildren<TextMeshProUGUI>().text = filename;
	  y += 0.07f;
	}

//      options.transform.localPosition = new Vector3(0f,y,0f);
//      y -= 0.1f;

      // Make new Open buttons.
      string[] files = load_models.gltf_file_paths();
//      y = -0.08f;
      y = -0.14f;
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
	  row.transform.SetSiblingIndex(0);  // Stack beneath meeting keypad
	  ButtonSelect bs = row.GetComponentInChildren<ButtonSelect>();
	  // Register open file callback function.
	  Func<Task> open_file = async () => await load_models.load_gltf_file(path);
          bs.async_action = open_file;
	  // Set user interface button text
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
    Vector3 look = tip - headset.eye_position();
    look.y = 0;
    transform.rotation = Quaternion.FromToRotation(new Vector3(0,0,1), look);
  }

  void set_initial_ui_panel_position_delayed()
  {
    if (initial_position_set)
      return;
    Invoke("set_initial_ui_panel_position", 0.5f);
  }

  void set_initial_ui_panel_position()
  {
    Vector3 eye_pos = headset.eye_position();
    Vector3 look = headset.view_direction();
    Vector3 view_dir = new Vector3(look.x, 0, look.z);  // Remove vertical component.
    Vector3 center = eye_pos + initial_distance * view_dir.normalized;
    transform.position = center;
    transform.rotation = Quaternion.FromToRotation(Vector3.forward, view_dir);
    initial_position_set = true;
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

  public void start_meeting(string button_name)
  {
    meeting.start_hosting();
    meeting_buttons.SetActive(false);
    meeting_host_status.SetActive(true);
    Vector3 dim = OVRManager.boundary.GetDimensions(OVRBoundary.BoundaryType.PlayArea);
    GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Boundary size " + dim.x + " " + dim.z;
  }

  public void end_meeting(string button_name)
  {
    meeting.stop_hosting();
    meeting_host_status.SetActive(false);
    meeting_buttons.SetActive(true);
  }

  public void join_meeting(string button_name)
  {
    meeting_keypad.SetActive(true);
  }

  public void leave_meeting(string button_name)
  {
    meeting.leave_meeting();
    meeting_join_status.SetActive(false);
    meeting_buttons.SetActive(true);
  }

  public void meeting_address_keypad(string button_name)
  {
    string addr = ip_address.text;
    if (button_name == "Join")
    {
      meeting_keypad.SetActive(false);
      meeting.join_meeting(addr);
    }
    else if (button_name == "Cancel")
      meeting_keypad.SetActive(false);
    else if (button_name == "b")
    {
      if (addr.Length > 0)
        ip_address.text = addr.Substring(0,addr.Length-1);
    }
    else
      ip_address.text += button_name;
  }

  public void report_join_success(string address)
  {
    settings.meeting_last_join_ip_address = address;
    meeting_buttons.SetActive(false);
    meeting_join_status.SetActive(true);
    GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Connected";
  }

  public void report_join_failed(string error_message)
  {
    show_error_message(error_message);
    GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = error_message;
  }
  
  public void align_meeting(bool align)
  {
    meeting.enable_hand_alignment(align);
  }

  public void show_error_message(string message)
  {
    error_text.text = message;
    position_error_panel();
    error_panel.SetActive(true);
  }

  void position_error_panel()
  {
    Vector3 eye_pos = headset.eye_position();
    Vector3 look = headset.view_direction();
    Vector3 view_dir = new Vector3(look.x, 0, look.z);  // Remove vertical component.
    Vector3 center = eye_pos + initial_distance * view_dir.normalized;
    Transform transform = error_panel.transform;
    transform.position = center;
    transform.rotation = Quaternion.FromToRotation(Vector3.forward, view_dir);
  }
  
  public void dismiss_error_panel()
  {
    error_panel.SetActive(false);
  }
  
  public void show_table(bool show)
  {
    table.SetActive(show);
  }
}

[Serializable]
public class LookSeeSettings
{
  public string meeting_last_join_ip_address;
  public List<MeetingCoordinateAlignment> meeting_alignments;

  private string settings_path()
  {
    return Path.Join(Application.persistentDataPath, "settings.json");
  }

  public bool load()
  {
    string path = settings_path();
    if (File.Exists(path))
    {
      byte[] json_bytes = File.ReadAllBytes(path);
      string json = Encoding.UTF8.GetString(json_bytes);
      JsonUtility.FromJsonOverwrite(json, this);
      return true;
    }
    return false;
  }

  public void save()
  {
    string path = settings_path();
    string json = JsonUtility.ToJson(this);
    byte[] json_bytes = System.Text.Encoding.UTF8.GetBytes(json);
    File.WriteAllBytes(path, json_bytes);
  }

  public void save_meeting_alignment(string room_id1, string room_id2, Matrix4x4 m)
  {
    int i = meeting_alignment_index(room_id1, room_id2);
    if (i >= 0)
      meeting_alignments.RemoveAt(i);
    MeetingCoordinateAlignment align = new MeetingCoordinateAlignment();
    align.room_id1 = room_id1;
    align.room_id2 = room_id2;
    align.alignment = m;
    meeting_alignments.Add(align);
  }

  public bool find_meeting_alignment(string room_id1, string room_id2, ref Matrix4x4 m)
  {
    int i = meeting_alignment_index(room_id1, room_id2);
    if (i < 0)
      return false;
    m = meeting_alignments[i].alignment;
    return true;
  }

  private int meeting_alignment_index(string room_id1, string room_id2)
  {
    for (int i = 0 ; i < meeting_alignments.Count ; ++i)
      if (meeting_alignments[i].room_id1 == room_id1 &&
          meeting_alignments[i].room_id2 == room_id2)
	return i;
    return -1;
  }

}

[Serializable]
public class MeetingCoordinateAlignment
{
  public string room_id1, room_id2;
  public Matrix4x4 alignment;
}

