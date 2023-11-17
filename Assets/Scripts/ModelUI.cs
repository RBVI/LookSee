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
    public GameObject models_pane, files_pane, meeting_pane, options_pane;
    public GameObject hide_show_close_prefab;
    public GameObject open_file_prefab;
    public GameObject scroll_files_up_button, scroll_files_down_button;
    public GameObject no_models_message, no_files_message;
    public GameObject options;
    public GameObject error_panel;
    private const int max_files = 6;		// Maximum number of files shown in panel.
    private int top_file = 0;			// Index of top shown file.
    public TextMeshProUGUI error_text;
    public float check_for_files_interval = 5.0f;	// seconds
    public bool open_new = false;
    public Toggle open_new_toggle, show_room_toggle, edge_smoothing_toggle;
    public Toggle keep_aligned_toggle;
    public LoadModels load_models;
    public FileReceiver file_receiver;
    public Meeting meeting;
    public GameObject meeting_buttons, meeting_host_status, meeting_join_status;
    public TextMeshProUGUI host_address_text;     // For reporting meeting IP address.
    public TextMeshProUGUI join_address_text;     // For reporting meeting IP address.
    public GameObject meeting_keypad;		// For entering IP address
    public TMP_InputField ip_address;
    public OVRPassthroughLayer pass_through;
    public GameObject table;
    public Wands wands;				// Used to position UI panel on button click.
    public Headset headset;			// View direction used to position UI panel.
    public float initial_distance = 0.8f;	// How far to place UI in front of eyes, meters.
    private bool initial_position_set = false;
    public LookSeeSettings settings = new LookSeeSettings();
    private bool show_next_ready = false;
    
    void Start()
    {
      settings.load();
      
      // Position and show UI only after VR headset position and view direction are known.
      OVRManager.TrackingAcquired += set_initial_ui_panel_position_delayed;

      // no_files_message.GetComponent<TextMeshProUGUI>().text += " " + Application.persistentDataPath;
      update_files_pane();

      ip_address.text = settings.meeting_last_join_ip_address;
      show_room_toggle.isOn = settings.show_room;
      edge_smoothing_toggle.isOn = settings.edge_smoothing;
      keep_aligned_toggle.isOn = settings.keep_aligned;

      InvokeRepeating("open_new_files", check_for_files_interval, check_for_files_interval);
    }
    
    public void ShowOrHideUI(InputAction.CallbackContext context)
    {
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
      update_models_pane();
      update_files_pane();
    }

    private void update_models_pane()
    {
      // Destroy current buttons.
      foreach (Transform child in models_pane.transform)
	if (child.gameObject.tag == "dynamicUI")
          GameObject.Destroy(child.gameObject);

      // Make new Hide, Show, Close buttons.
      float y = -140f;	// millimeters
      foreach (Model model in load_models.open_models.models)
	{
	  GameObject row = Instantiate(hide_show_close_prefab,
	                               new Vector3(0f,y,0f), Quaternion.identity);
	  row.transform.SetParent(models_pane.transform, false);
  	  // row.transform.SetSiblingIndex(0);  // Stack beneath meeting keypad
  	  // Set callback for "close" button.
	  GameObject cbutton = row.transform.Find("Close button").gameObject;
	  ButtonSelect bs = cbutton.GetComponentInChildren<ButtonSelect>();
          Action<string> close = (string button_name) => { load_models.open_models.remove_model(model); };
	  bs.action = close;
	  // Set callback for "shown" toggle button.
  	  Toggle t = row.GetComponentInChildren<Toggle>();
	  t.isOn = model.model_object.activeSelf;
	  t.onValueChanged.AddListener(delegate { show_or_hide_model(t.isOn, model); });
	  GameObject name = row.transform.Find("Name").gameObject;
	  name.GetComponentInChildren<TextMeshProUGUI>().text = model.name();
	  y += 70f;
	}

        no_models_message.SetActive(load_models.open_models.models.Count == 0);
     }

    private void update_files_pane()
    {
      // Destroy current buttons.
      foreach (Transform child in files_pane.transform)
	if (child.gameObject.tag == "dynamicUI")
          GameObject.Destroy(child.gameObject);

      // Make new Open buttons.
      string[] files = load_models.gltf_file_paths();
      Array.Sort(files);
      if (top_file > 0 && files.Length - top_file < max_files)
        top_file = Math.Max(0, files.Length - max_files);  // Show as many files as possible.
      float y = 160f;	// millimeters
      int files_to_show = Math.Min(files.Length - top_file, max_files);
      if (files_to_show < max_files)
        y -= 60 * (max_files - files_to_show);
      for (int i = 0 ; i < files_to_show ; ++i)
	{
	  string path = files[top_file + i];
	  
	  // Create one row per file with Open and Delete buttons.
	  GameObject row = Instantiate(open_file_prefab,
	                               new Vector3(0f,y,0f), Quaternion.identity);
	  row.transform.SetParent(files_pane.transform, false);
	  // row.transform.SetSiblingIndex(0);  // Stack beneath meeting keypad

	  // Set Open button callback.
	  GameObject open_button = row.transform.Find("Open button").gameObject;
	  ButtonSelect obs = open_button.GetComponent<ButtonSelect>();
	  Func<Task> open_file = async () => await load_models.load_gltf_file(path);
          obs.async_action = open_file;   // Register open file callback function.

	  // Set Delete button callback.
  	  GameObject delete_button = row.transform.Find("Delete button").gameObject;
	  ButtonSelect dbs = delete_button.GetComponent<ButtonSelect>();
          Action<string> delete_file = (string button_name) => { load_models.remove_gltf_file(path);
	  		 	       	       		         update_files_pane(); };
          dbs.action = delete_file;  // Register delete file callback function.

	  // Set file name label.
          string filename = Path.GetFileNameWithoutExtension(path);
	  GameObject name = row.transform.Find("Name").gameObject;
	  name.GetComponentInChildren<TextMeshProUGUI>().text = filename;

	  y -= 60f;
	}

      scroll_files_up_button.SetActive(top_file > 0);
      scroll_files_down_button.SetActive(top_file + max_files < files.Length);
        
      no_files_message.SetActive(files.Length == 0);
    }

  public void scroll_files_down()
  {
    top_file += max_files;
    update_files_pane();
    
  }

  public void scroll_files_up()
  {
    top_file = Math.Max(0, top_file - max_files);
    update_files_pane();
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

  public void show_models_pane()
  {
    hide_panes();
    update_models_pane();
    models_pane.SetActive(true);
  }

  public void show_files_pane()
  {
    hide_panes();
    update_files_pane();
    files_pane.SetActive(true);
  }

  public void show_meeting_pane()
  {
    hide_panes();
    meeting_pane.SetActive(true);
  }

  public void show_options_pane()
  {
    hide_panes();
    options_pane.SetActive(true);
  }

  private void hide_panes()
  {
    models_pane.SetActive(false);
    files_pane.SetActive(false);
    meeting_pane.SetActive(false);
    options_pane.SetActive(false);
  }
  
  public void hide_ui()
  {
    gameObject.SetActive(false);
  }
  
  public void pass_through_toggled(bool pass)
  {
    pass_through.enabled = pass;
    meeting.using_pass_through(pass);
    settings.show_room = pass;
    settings.save();
  }

  public bool using_pass_through()
  {
    return pass_through.enabled;
  }

  public void set_edge_smoothing(bool smooth)
  {
    // OVRManager.cs in Oculus Integration version 53.1 does not update when
    // OVRManager.useRecommendedMSAALevel is turned off.
    // So set it directly in QualitySettings.
    int level = (smooth ? OVRManager.display.recommendedMSAALevel : 0);
    QualitySettings.antiAliasing = level;
    
    settings.edge_smoothing = smooth;
    settings.save();
  }

  public void set_keep_aligned(bool align)
  {
    load_models.open_models.keep_aligned = align;
    settings.keep_aligned = align;
    settings.save();
  }
  
  public void original_alignment(string button_name)
  {
    load_models.open_models.restore_original_alignment();
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

  public void show_or_hide_model(bool show, Model model)
  {
      model.model_object.SetActive(show);
  }

  // Show next using controller thumbstick.
  public void NextModel(InputAction.CallbackContext context)
  {
    if (!context.performed)
      return;

    Vector2 stick = context.action.ReadValue<Vector2>();
    float x = Math.Abs(stick.x), y = Math.Abs(stick.y);
    if (show_next_ready && x > y && x > 0.8)
    {
      if (stick.x > 0)
	  show_next_model();
	else
        show_previous_model();
	show_next_ready = false;
    }
    if (x < .6)
      show_next_ready = true;
  }

  void show_next_model()
  {
    List<Model> modlist = load_models.open_models.models;
    int n = modlist.Count, next = 0;
    if (n == 0)
      return;
    for (int i = 0 ; i < n ; ++i)
      if (modlist[i].shown())
	  next = i+1;
    if (next == n)
      next = 0;
    foreach (Model m in modlist)
      if (m.shown())
	  m.hide();
    modlist[next].show();
    if (models_pane_shown())
      update_models_pane();
  }

  void show_previous_model()
  {
    List<Model> modlist = load_models.open_models.models;
    int n = modlist.Count;
    if (n == 0)
      return;
    int prev = n-1;
    for (int i = n-1 ; i >= 0 ; --i)
      if (modlist[i].shown())
	  prev = i-1;
    if (prev < 0)
      prev = n-1;
    foreach (Model m in modlist)
      if (m.shown())
	  m.hide();
    modlist[prev].show();
    if (models_pane_shown())
      update_models_pane();
  }

  bool models_pane_shown()
  {
    return gameObject.activeSelf && models_pane.activeSelf;
  }

  public void start_meeting(string button_name)
  {
    meeting.start_hosting();
    host_address_text.text = "Meeting at " + meeting.get_local_ip_address();
    meeting_buttons.SetActive(false);
    meeting_host_status.SetActive(true);
    Vector3 dim = OVRManager.boundary.GetDimensions(OVRBoundary.BoundaryType.PlayArea);
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
    left_meeting();
  }

  public void left_meeting()
  {
    meeting_join_status.SetActive(false);
    meeting_host_status.SetActive(false);
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
    meeting_buttons.SetActive(false);
    join_address_text.text = "Connected " + address;
    meeting_join_status.SetActive(true);
    settings.meeting_last_join_ip_address = address;
    settings.save();
  }

  public void report_join_failed(string error_message)
  {
    show_error_message(error_message);
    meeting_buttons.SetActive(true);
    meeting_join_status.SetActive(false);
  }
  
  public void align_meeting(bool align)
  {
    meeting.set_room_coordinates(align);
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
  public List<MeetingCoordinates> meeting_coordinates;
  public bool show_room = true, edge_smoothing = true, keep_aligned = false;

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

  public void save_meeting_coordinates(string room_coordinates_id, Vector3 x1, Vector3 x2)
  {
    int i = meeting_coordinates_index(room_coordinates_id);
    if (i >= 0)
      meeting_coordinates.RemoveAt(i);
    MeetingCoordinates coords = new MeetingCoordinates();
    coords.room_coordinates_id = room_coordinates_id;
    coords.x1 = x1;
    coords.x2 = x2;
    meeting_coordinates.Add(coords);
  }

  public bool find_meeting_coordinates(string room_coordinates_id, ref Vector3 x1, ref Vector3 x2)
  {
    int i = meeting_coordinates_index(room_coordinates_id);
    if (i < 0)
      return false;
    x1 = meeting_coordinates[i].x1;
    x2 = meeting_coordinates[i].x2;
    return true;
  }

  private int meeting_coordinates_index(string room_coordinates_id)
  {
    for (int i = 0 ; i < meeting_coordinates.Count ; ++i)
      if (meeting_coordinates[i].room_coordinates_id == room_coordinates_id)
	return i;
    return -1;
  }

}

[Serializable]
public class MeetingCoordinates
{
  public string room_coordinates_id;	// Unique identifier for room and VR coordinate system.
  public Vector3 x1, x2;		// Points defining the room x-axis and center.
}

