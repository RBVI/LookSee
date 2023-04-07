using UnityEngine;

class ModelUtilities
{
  public static Bounds model_bounds(GameObject model)
  {
    Bounds bounds = new Bounds (Vector3.zero, Vector3.zero);
    Renderer[] renderers = model.GetComponentsInChildren<Renderer> ();
    foreach (Renderer renderer in renderers)
      {
        Bounds rbounds = renderer.bounds;
	if (rbounds.size.magnitude > 0)
	{
	  if (bounds.size.magnitude == 0)
	    {
	      bounds.center = rbounds.center;
	      bounds.size = rbounds.size;
	    }
	  else
	    {
	      bounds.Encapsulate (rbounds);
	    }
	}
      }
    return bounds;
  }

  public static Vector3 model_center(GameObject model)
  {
    return model_bounds(model).center;
  }
    
  public static GameObject closest_child(GameObject parent, Vector3 origin, Vector3 direction)
  {
	float amin = 90.0f;
	GameObject gmin = null;
	foreach (Transform transform in parent.transform)
	{
	  GameObject o = transform.gameObject;
	  Bounds b = model_bounds(o);
	  float a = Vector3.Angle(b.center - origin, direction);
	  if (a < amin)
	  {
	    amin = a;
	    gmin = o;
	  }
        }
 	return gmin;
  }
}