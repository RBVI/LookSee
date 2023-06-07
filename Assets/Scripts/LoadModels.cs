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
  public float initial_model_size = 0.5f;  // meters
  public Vector3 initial_model_center = new Vector3(0, 1, 0);
  public GameObject example_model;		// Shown if not GLTF files found.
  public ModelUI model_ui;
  
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
      open_models.add("SARS-CoV-2 Spike", example_model);
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
    model_object.transform.parent = gameObject.transform;
    var instantiator = new GameObjectInstantiator(gltfImport, model_object.transform);
    success = await gltfImport.InstantiateSceneAsync(instantiator);

    if (success) {
      Debug.Log("Loaded " + path);
      center_and_scale_model(model_object);
      Model m = open_models.add(path, model_object);
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
    model_object.transform.parent = gameObject.transform;
    var instantiator = new GameObjectInstantiator(gltfImport, model_object.transform);
    success = await gltfImport.InstantiateSceneAsync(instantiator);

    if (success) {
      center_and_scale_model(model_object);
      Model m = open_models.add(model_name, model_object);
      return m;
    } else {
      Debug.Log("gltfImport failed instantiating " + model_name);
    }
    return null;
  }

  void center_and_scale_model(GameObject model_object)
  {
    // Scale each scene to a size about 1 meter
    Bounds bounds = model_bounds(model_object);
    Vector3 size = bounds.size;
    float max_size = Mathf.Max(Mathf.Max(size.x, size.y), size.z);
    float scale = initial_model_size/max_size;
    scale_model(model_object, scale);

    // Shift model to center.
    Vector3 shift = -scale * bounds.center;
    model_object.transform.position = next_model_center() + shift;
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

  Vector3 next_model_center()
  {
    int n = open_models.count() + 1;
    int i, j;
    square_spiral_position_ij(n, out i, out j);
    float x = initial_model_size * i, y = initial_model_size * j;
    Vector3 center = initial_model_center + new Vector3(x,0,y);
    return center;
  }

  // Report i,j coordinates for square counterclockwise spiral.
  // n = 1 is center position.
  void square_spiral_position_ij(int n, out int i, out int j)
  {
    int r = Mathf.CeilToInt((Mathf.Sqrt(n) - 1) / 2);  // Ring
    int n2 = (r > 0 ? (2*r-1)*(2*r-1) : 0);
    int o = n - n2 - 1;  // Position along ring
    if (o <= r)
      { i = r; j = o; }        // right side
    else if (o <= 3*r)
      { i = 2*r - o; j = r; }  // top side
    else if (o <= 5*r)
      { i = -r; j = 4*r - o; } // left side
    else if (o <= 7*r)
      { i = o - 6*r; j = -r; } // bottom side
    else
      { i = r; j = o - 8*r; }  // right side
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
	  t.rotation = tc.rotation;
	  t.position = tc.position;
	  open_models.remove_model(m);
	}
      num_opened += 1;
    }

    if (num_opened > 0)
      record_files();

    return num_opened;
  }

}

public class Models
{
  public List<Model> models = new List<Model>();
  public ModelUI model_ui = null;  // Called to update UI when model added or removed.
  
  public Model add(string path, GameObject model_object)
  {
    Model model = new Model(path, model_object);
    models.Add(model);
    update_model_ui();
    return model;
  }

  public int count()
  {
    return models.Count;
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
  public DateTime last_modified;
  public GameObject model_object;

  public Model(string path, GameObject model_object)
  {
    this.path = path;
    this.last_modified = File.GetLastWriteTime(path);
    this.model_object = model_object;
  }

  public bool file_changed()
  {
    return File.GetLastWriteTime(path) > last_modified;
  }
}
