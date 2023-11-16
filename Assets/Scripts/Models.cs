using UnityEngine;

class ModelUtilities
{
  public static Bounds model_bounds(GameObject model)
  {
    // This gives the bounds in world space which will include any
    // transforms the parent of model applies.
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
    
  public static Model closest_model(Models models, Vector3 origin, Vector3 direction)
  {
    float amin = 180.0f;
    Model closest = null;
    foreach (Model model in models.models)
      if (model.model_object.activeSelf)
      {
	Bounds b = model_bounds(model.model_object);
	float a = Vector3.Angle(b.center - origin, direction);
	if (a < amin)
	{
	  amin = a;
	  closest = model;
	}
      }
    return closest;
  }
}