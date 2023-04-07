using UnityEngine;
using System.Collections;

public class ColliderSound : MonoBehaviour
{
    AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
	audioSource.volume = 0.1f;
    }

    void OnCollisionEnter(Collision collision)
    {
    // Kinematic rigidbodies have relativeVelocity always 0.
    //   float loudness = collision.relativeVelocity.magnitude / 10.0f;
    //	 audioSource.volume = loudness;
    if (collision.collider.tag == "Wand")
        audioSource.Play();
    }
}