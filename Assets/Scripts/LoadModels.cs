using System;			   // Use DateTime
using System.IO;		   // Use Path, Directory
using System.Collections.Generic;  // Use List
using System.Threading.Tasks;  	   // Use Task
using UnityEngine;
using GLTFast;
using static ModelUtilities;     // use model_bounds()

// TODO: Will need a user interface to choose a directory of models to load
//       so that different demos can be saved on the Quest 2.

public class LoadModels : MonoBehaviour
{
  public float initial_model_size = 1.0f;	// meters
  public float initial_distance = 1.0f;		// meters
  public GameObject example_model;		// Shown if not GLTF files found.
  public ModelUI model_ui;
  public Headset headset;			// Used for initial model positioning
  
  public Models open_models = new Models();

  Dictionary<string, DateTime> latest_files;
  
  //async void Start()
  void Start()
  {
    open_models.model_ui = model_ui;
    
    Debug.Log("persistent data path " + Application.persistentDataPath);
    string[] files = gltf_file_paths();

    // If no files are found then show an example model.
    if (files.Length == 0)
    {
      example_model.SetActive(true);
      Model model = new Model("SARS-CoV-2 Spike", example_model, 1.0f);
      open_models.add(model);
    }

//    foreach (string path in files)
//      await load_gltf_file(path);
  }

  public string[] gltf_file_paths()
  {
    string directory = Application.persistentDataPath;
    string pattern = "*.glb";
    string[] files = Directory.GetFiles(directory, pattern);
    return files;
  }

  async public Task<Model> load_gltf_file(string path)
  {
    var gltfImport = new GltfImport();
    bool success = await gltfImport.Load(path);
    if (!success)
    {
      Debug.Log("gltfImport.Load() failed on file " + path);
      return null;
    }

    string filename = Path.GetFileName(path);
    GameObject model_object = new GameObject(filename);
//    model_object.transform.parent = gameObject.transform;
    var instantiator = new GameObjectInstantiator(gltfImport, model_object.transform);
    success = await gltfImport.InstantiateSceneAsync(instantiator);

    if (success) {
      Debug.Log("Loaded " + path);
      float scaled = center_and_scale_model(model_object);
      // Set parent after center and bounds so parent transform does not effect center and scale.
      model_object.transform.SetParent(gameObject.transform);
      Model m = new Model(path, model_object, scaled);
      open_models.add(m);
      return m;
    } else {
      Debug.Log("gltfImport failed instantiating " + path);
    }
    return null;
  }

  async public Task<Model> load_gltf_bytes(byte[] gltf_data, string model_name)
  {
    var gltfImport = new GltfImport();
    bool success = await gltfImport.Load(gltf_data);
    if (!success)
    {
      Debug.Log("gltfImport.Load() failed on byte array of length " + gltf_data.Length + " " + model_name);
      return null;
    }

    GameObject model_object = new GameObject(model_name);
//    model_object.transform.SetParent(gameObject.transform, false);
    var instantiator = new GameObjectInstantiator(gltfImport, model_object.transform);
    success = await gltfImport.InstantiateSceneAsync(instantiator);

    if (success) {
      float scaled = center_and_scale_model(model_object);
      // Set after center and bounds so parent transform does not effect center and scale.
      model_object.transform.SetParent(gameObject.transform);
      Model m = new Model(model_name, model_object, scaled);
      m.gltf_data = gltf_data;
      return m;
    } else {
      Debug.Log("gltfImport failed instantiating " + model_name);
    }
    return null;
  }

  float center_and_scale_model(GameObject model_object)
  {
    // Scale each scene to a size about 1 meter
    Bounds bounds = model_bounds(model_object);
    Vector3 size = bounds.size;
    float max_size = Mathf.Max(Mathf.Max(size.x, size.y), size.z);
    float scale = initial_model_size/max_size;
    scale_model(model_object, scale);

    // Shift model to center.
    Vector3 shift = -scale * bounds.center;
    model_object.transform.position = initial_model_position() + shift;

    return scale;
  }

  void scale_model(GameObject model_object, float scale)
  {
    // model_object.transform.localScale = new Vector3(scale, scale, scale);

    //
    // Scaling down a vertex colored surface by a factor of ~1.0/30000 causes
    // it to display as all gray/black.  As one scales interactively the colors
    // return when the scale factor applied by transforms is not so extreme.
    // I'm guessing this is a limitation of the numeric precision in the shader,
    // possibly for linearly interpolating colors across a triangle.  The geometry
    // of the surface still renders correctly.  This scale factor happens when
    // looking at electron microscopy tomograms with units in Angstroms.
    // Described in ChimeraX bug report #8799.
    // To work around this limitation we instead scale the mesh vertex
    // coordinates and the game object transform positions.
    //
    foreach (MeshFilter mf in model_object.GetComponentsInChildren<MeshFilter>())
    {
	Mesh mesh = mf.mesh;
	Vector3[] vertices = mesh.vertices;
        for (var i = 0; i < vertices.Length; i++)
            vertices[i] *= scale;
	mesh.vertices = vertices;
	mesh.RecalculateBounds();
    }

    scale_positions(model_object.transform, scale);
  }

  void scale_positions(Transform transform, float scale)
  {
    transform.localPosition *= scale;
    foreach (Transform child_transform in transform)
      scale_positions(child_transform, scale);
  }

  Vector3 initial_model_position()
  {
    Vector3 look = headset.view_direction();
    Vector3 view_dir = new Vector3(look.x, 0, look.z);  // Remove vertical component.
    Vector3 center = headset.eye_position() + initial_distance * view_dir.normalized;
    return center;
  }

  public void record_files()
  {
    if (latest_files == null)
      latest_files = new Dictionary<string, DateTime>();
    else
      latest_files.Clear();

    string[] files = gltf_file_paths();
    foreach (string path in files)
      latest_files.Add(path, File.GetLastWriteTime(path));
  }
  
  // Must call record_files() before calling this routine.
  async public Task<int> open_new_files()
  {
    string[] files = gltf_file_paths();
    int num_opened = 0;
    foreach (string path in files)
    {
      if (latest_files.ContainsKey(path) &&
          File.GetLastWriteTime(path) == latest_files[path])
	continue;

      Model m = open_models.have_model(path);
      Model mnew = await load_gltf_file(path);
      if (mnew != null && m != null)
        {
          // Put new model in same position and scale as old one.
	  Transform tc = m.model_object.transform;
	  Transform t = mnew.model_object.transform;
	  t.localScale = tc.localScale;
	  t.localRotation = tc.localRotation;
	  t.localPosition = tc.localPosition;
	  open_models.remove_model(m);
	}
      num_opened += 1;
    }

    if (num_opened > 0)
      record_files();

    return num_opened;
  }

  public bool remove_gltf_file(string path)
  {
    if (!File.Exists(path))
      return false;
    File.Delete(path);
    return true;
  }

  public Transform scene_transform()
  {
    return gameObject.transform;
  }
  
}

public class Models
{
  public List<Model> models = new List<Model>();
  public bool keep_aligned = false;
  public ModelUI model_ui = null;  // Called to update UI when model added or removed.
  
  public void add(Model model)
  {
    models.Add(model);
    if (keep_aligned)
      align_model(model);
    update_model_ui();
  }

  void align_model(Model model)
  {
    Model align_to = align_to_model();
    if (align_to == null)
      return;

    Transform a = align_to.model_object.transform;
    Transform t = model.model_object.transform;
    t.localPosition = a.localPosition;
    t.localRotation = a.localRotation;
    t.localScale = (align_to.scale / model.scale) * a.localScale;
  }

  public void restore_original_alignment(Model hold_still = null)
  {
    if (models.Count == 0)
      return; 

    if (hold_still == null)
      hold_still = align_to_model();

    Transform h = hold_still.model_object.transform;

    foreach (Model m in models)
    {
      Transform t = m.model_object.transform;
      t.localPosition = h.localPosition;
      t.localRotation = h.localRotation;
      t.localScale = (hold_still.scale / m.scale) * h.localScale;
    }
  }

  private Model align_to_model()
  {
    // Model that other models are aligned when keep_aligned is true.
    Model m = first_shown_model();
    if (m == null && models.Count > 0)
      m = models[0];
    return m;
  }

  private Model first_shown_model()
  {
    foreach (Model m in models)
    if (m.shown())
      return m;
    return null;
  }
  
  public int count()
  {
    return models.Count;
  }

  public GameObject parent()
  {
    if (models.Count == 0)
      return null;
    return models[0].model_object.transform.parent.gameObject;
  }
  
  public Model have_model(string path)
  {
    foreach (var m in models)
      if (m.path == path)
        return m;
    return null;
  }
  
  public Model model_for_game_object(GameObject go)
  {
    foreach (var m in models)
      if (m.model_object == go)
        return m;
    return null;
  }

  public void remove_model(Model m)
  {
    models.Remove(m);
    GameObject.Destroy(m.model_object);
    m.model_object = null;
    update_model_ui();
  }

  public void remove_game_object(GameObject go)
  {
    remove_model(model_for_game_object(go));
  }

  void update_model_ui()
  {
    if (model_ui != null)
      model_ui.update_ui_controls();
  }
}

public class Model
{
  public string path;
  public byte [] gltf_data;
  public DateTime last_modified;
  public GameObject model_object;
  public float scale;

  public Model(string path, GameObject model_object, float scale)
  {
    this.path = path;
    this.last_modified = File.GetLastWriteTime(path);
    this.model_object = model_object;
    this.scale = scale;		// Original GLTF data was scaled by this factor.
  }

  public bool shown()
  {
    return model_object.activeSelf;
  }

  public void show()
  {
    model_object.SetActive(true);
  }

  public void hide()
  {
    model_object.SetActive(false);
  }
    
  public bool has_gltf_data()
  {
    return gltf_data != null || File.Exists(path);
  }

  public bool file_changed()
  {
    return File.GetLastWriteTime(path) > last_modified;
  }

  public string name()
  {
    return Path.GetFileNameWithoutExtension(path);
  }
}
