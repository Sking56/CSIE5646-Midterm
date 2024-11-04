using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MinecartCollider : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Collision detected");
        if (collision != null)
        {
            //On collision with gem, add points to gem count
            if (collision.gameObject.tag == "Gem")
            {
                GameManager.instance.AddGem();

                // Set gem to inactive
                collision.gameObject.SetActive(false);
            }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        // Do nothing
        Debug.Log(gameObject.name + " is still colliding with " + collision.gameObject.name);
    }

    private void OnCollisionExit(Collision collision)
    {
        Debug.Log("Collision exit detected");
        // On collision exit, set gem to active
        if (collision.gameObject.tag == "Gem")
        {
            collision.gameObject.SetActive(true);
        }
    }
}
