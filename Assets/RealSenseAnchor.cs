using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static OVRInput;
using TMPro;

public class RealSenseAnchor : MonoBehaviour
{
    //Specify controller to create Spatial Anchors
    [SerializeField] private Controller controller;
    [SerializeField] private float speed = 0.1f; // Set moving speed
    private int count = 0;
    // Spatial Anchor Prefab
    public GameObject anchorPrefab;

    //List of anchors
    public List<GameObject> realSenseAnchors = new List<GameObject>();

    //Map of anchor id to anchor object
    public Dictionary<int, GameObject> anchorMap = new Dictionary<int, GameObject>();

    private Canvas canvas;
    private bool move = false;

    // Update is called once per frame
    void Update()
    {
        //If user presses the A button on the right controller, toggle the movement of the anchors
        if (OVRInput.GetDown(OVRInput.Button.One, controller))
        {
            move = !move;
            print("Move: " + move);
        }

        //If move is enabled, then move the realsense anchors with the right thumbstick
        if (move)
        {
            Vector2 axis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, controller);
            // Move the anchor parent object on xy plane.
            transform.Translate(new Vector3(axis.x, 0, axis.y) * speed * Time.deltaTime, Space.World);
        }

        // Delete anchors if b is pressed
        if (OVRInput.GetDown(OVRInput.Button.Two, controller))
        {
            foreach (GameObject anchor in realSenseAnchors)
            {
                Destroy(anchor);
            }
            realSenseAnchors.Clear();
            anchorMap.Clear();
            count = 0;
        }
    }

    //Create anchor based on the specified coordinates with no rotation
    public void CreateRealSenseAnchor(int id, Vector3 position)
    {
        // Create anchor at specified position and rotation under the parent object
        GameObject anchor = Instantiate(anchorPrefab, position, Quaternion.identity, transform);
        anchor.transform.position = position;
        anchor.transform.rotation = Quaternion.identity;

        // Make the anchor become a Meta Quest Spatial Anchor
        //anchor.AddComponent<OVRSpatialAnchor>();

        //Add the anchor to the list of realsense anchors and the map of anchor id to anchor object
        anchorMap.Add(id, anchor);
        realSenseAnchors.Add(anchor);
    }
}