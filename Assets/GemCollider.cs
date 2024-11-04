using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GemCollider : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Collision detected " + other.name);
        if (other.gameObject.tag == "Minecart")
        {
            gameObject.GetComponent<MeshRenderer>().enabled = false;
            GameManager.instance.AddGem();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Collision ended " + other.name);
        if (other.gameObject.tag == "Minecart")
        {
            gameObject.GetComponent<MeshRenderer>().enabled = true;
        }
    }
}
