using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private string collectText = "Push the cart and collect the gems!";
    private string refillText = "Come to me and refill the cart with gems!";
    private string gameOver = "Game Over! Total Gems Collected: ";
    public static GameManager instance;  // Singleton instance
    public int gemCount = 0;
    public int roundCount = 0;
    private int maxRounds = 3;
    public GameObject textbox;
    public GameObject cart;
    public GameObject npcLHand;
    public GameObject npcRHand;
    public GameObject npcHead;
    public GameObject npc;
    public bool refill = false;
    public bool calibrated = false;

    // Create aruco id map
    public Dictionary<int, GameObject> arucoIdMap = new Dictionary<int, GameObject>();

    // Ensure this object is not destroyed when changing scenes
    void Awake()
    {
        // If an instance already exists and it's not this one, destroy this one
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(gameObject);  // Persist the GameManager across scenes
        }
    }

    // Method to increase the gem count
    public void AddGem()
    {
        gemCount++;
        Debug.Log("Gems Collected: " + gemCount);
    }


    // Refill the cart
    public void initiateRefill()
    {
        npc.active = true;
        // Check if this was the last round
        if (roundCount+1 >= maxRounds)
        {
            Debug.Log("Game Over! Total Gems Collected: " + gemCount);
            textbox.GetComponent<TextMeshPro>().text = gameOver + gemCount;
            // Quit the application
        }
        else
        {
            Debug.Log("Refill initiated");
            textbox.GetComponent<TextMeshPro>().text = refillText;
            refill = true;
        }
    }


    // End refill
    public void endRefill()
    {
        npc.active = false;
        textbox.GetComponent<TextMeshPro>().text = collectText;
        roundCount++;
        refill = false;
        Debug.Log("Cart Refilled! Round: " + roundCount);
    }

    // Update NPC body parts 500: LHand, 501: RHand, 502: Head
    public void updateNPC(int id, string x, string y, string z)
    {
        if(id == 501)
        {
            //npcRHand.transform.GetChild(0).position = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
            npcRHand.transform.position = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
        }
        else if (id == 500)
        {
            npcLHand.transform.position = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
        }
        else if (id == 502)
        {
            npcHead.transform.position = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
        }
    }

    // Update cart position on x and z axis only
    public void updateCart(string x, string y, string z)
    {
        cart.transform.position = new Vector3(float.Parse(x), cart.transform.position.y, float.Parse(z));
    }
}
