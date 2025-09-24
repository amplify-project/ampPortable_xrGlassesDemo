// 24/09/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using System;
using UnityEditor;
using UnityEngine;
using System.IO;

public class DisplayPNGFromStreamingAssets : MonoBehaviour
{
    public string pngFileName = "example.png"; // Name of the PNG file in the StreamingAssets folder

    void Start()
    {
        // Load the PNG file from the StreamingAssets folder
        string filePath = Path.Combine(Application.streamingAssetsPath, pngFileName);

        if (File.Exists(filePath))
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2); // Create a new Texture2D
            if (texture.LoadImage(fileData)) // Load the image data into the texture
            {
                // Create a Quad in the scene
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.transform.position = Vector3.zero; // Position it at the origin

                // Create a material and assign the texture
                Material material = new Material(Shader.Find("Standard"));
                material.mainTexture = texture;

                // Apply the material to the Quad
                quad.GetComponent<Renderer>().material = material;
            }
            else
            {
                Debug.LogError("Failed to load texture from PNG file.");
            }
        }
        else
        {
            Debug.LogError($"File not found at path: {filePath}");
        }
    }
}
