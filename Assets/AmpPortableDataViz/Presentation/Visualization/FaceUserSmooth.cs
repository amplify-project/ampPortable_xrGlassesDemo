using System;
using UnityEditor;
using UnityEngine;

public class FaceUserSmooth : MonoBehaviour
{
    public Transform userTransform; // Reference to the user's Transform (e.g., the camera or XR rig)
    public float rotationSpeed = 5f; // Speed of the rotation

    void Update()
    {
        if (userTransform != null)
        {
            // Calculate the direction to the user
            Vector3 direction = userTransform.position - transform.position;
            direction.y = 0; // Optional: Keep the ImageBoard upright by ignoring the y-axis

            // Calculate the target rotation
            Quaternion targetRotation = Quaternion.LookRotation(-direction);

            // Smoothly interpolate the rotation using Quaternion.Lerp
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }
}
