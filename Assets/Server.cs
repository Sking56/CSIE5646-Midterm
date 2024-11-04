/*
Reference
Implementing a Basic TCP Server in Unity: A Step-by-Step Guide
By RabeeQiblawi Nov 20, 2023
https://medium.com/@rabeeqiblawi/implementing-a-basic-tcp-server-in-unity-a-step-by-step-guide-449d8504d1c5
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using TMPro;
using Oculus.Interaction.DebugTree;

public class TCP : MonoBehaviour
{
    const string hostIP = "127.0.0.1"; // Select your IP
    const int port = 8080; // Select your port
    TcpListener server = null;
    TcpClient client = null;
    NetworkStream stream = null;
    Thread thread;

    public Transform LHand;
    public Transform RHand;
    public Transform Head;

    public GameObject RealSenseAnchor;
    public GameObject spatialAnchor;
    private SpatialAnchors spatialAnchors;
    private RealSenseAnchor realSenseAnchor;


    // Define your own message
    [Serializable]
    public class Message
    {
        public string ids;
        public string xs;
        public string ys;
        public string zs;
    }

    private float timer = 0;
    private static object Lock = new object();
    private List<Message> MessageQue = new List<Message>();


    private void Start()
    {
        thread = new Thread(new ThreadStart(SetupServer));
        thread.Start();
        spatialAnchors = spatialAnchor.GetComponent<SpatialAnchors>();
        realSenseAnchor = RealSenseAnchor.GetComponent<RealSenseAnchor>();
    }

    private void Update()
    {
        // Send message to client every 2 second
        if (Time.time > timer)
        {
            //Message msg = new Message();
            string ids = "";
            string xs = "";
            string ys = "";
            string zs = "";
            foreach (GameObject anchor in spatialAnchors.allAnchors)
            {
                string id_text = anchor.GetComponentInChildren<Canvas>().gameObject.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text;
                ids += id_text.Split(" ")[1] + " ";
                xs += anchor.transform.GetChild(0).GetChild(0).position.x.ToString() + " ";
                ys += anchor.transform.GetChild(0).GetChild(0).position.y.ToString() + " ";
                zs += anchor.transform.GetChild(0).GetChild(0).position.z.ToString() + " ";
            }

            // Check if not calibrated yet
            if (ids.Trim().Split(" ").Length == 4)
            {
                Message msg = new Message();
                msg.ids = ids.Trim();
                msg.xs = xs.Trim();
                msg.ys = ys.Trim();
                msg.zs = zs.Trim();
                SendMessageToClient(msg);
            }

            timer = Time.time + 1f;
        }
        // Process message que
        lock (Lock)
        {
            foreach (Message msg in MessageQue)
            {
                // Unity only allow main thread to modify GameObjects.
                // Spawn, Move, Rotate GameObjects here. 
                //Debug.Log("Received Str: " + msg.some_string + " Int: " + msg.some_int + " Float: " + msg.some_float);
                //Receive message with list of ids, xs, ys, zs, rotations from detected anchors on realsense camera
                //Create Anchor GameObjects with the received data
                string[] ids = msg.ids.Trim().Split(" ");
                string[] xs = msg.xs.Trim().Split(" ");
                string[] ys = msg.ys.Trim().Split(" ");
                string[] zs = msg.zs.Trim().Split(" ");

                bool refillSeen = false;
                for (int i = 0; i < ids.Length; i++)
                {
                    int id = -1;
                    if (int.TryParse(ids[i], out id))
                    {
                        if (id >= 500 && id <= 502 && GameManager.instance.refill)
                        {
                            GameManager.instance.updateNPC(id, xs[i], ys[i], zs[i]);
                        }
                        else if (id >= 1 && id <= 4 && !GameManager.instance.calibrated && !realSenseAnchor.anchorMap.ContainsKey(id))
                        {
                            realSenseAnchor.CreateRealSenseAnchor(id, new Vector3(float.Parse(xs[i]), float.Parse(ys[i]), float.Parse(zs[i])));
                        }
                        else if (id == 5)
                        {
                            refillSeen = true;
                            if (!GameManager.instance.refill)
                            {
                                GameManager.instance.initiateRefill();
                            }
                        }
                        else if (id == 230 || id == 100 || id == 10 || id == 125)
                        {
                            // Update cart position
                            GameManager.instance.updateCart(xs[i], ys[i], zs[i]);
                        }
                        else
                        {
                            Debug.Log("Unknown id: " + id);
                        }
                    }
                    else
                    {
                        Debug.Log("Invalid id: " + ids[i]);
                    }
                }
                // Check if calibration is complete
                if (realSenseAnchor.anchorMap.Count == 4 && !GameManager.instance.calibrated)
                {
                    GameManager.instance.calibrated = true;
                }

                // Check if refill is seen, if not seen and currently in refill end refill
                if (!refillSeen && GameManager.instance.refill)
                {
                    GameManager.instance.endRefill();
                }
            }
            MessageQue.Clear();
        }
    }

    private void SetupServer()
    {
        try
        {
            IPAddress localAddr = IPAddress.Parse(hostIP);
            server = new TcpListener(localAddr, port);
            server.Start();

            byte[] buffer = new byte[1024];
            string data = null;

            while (true)
            {
                Debug.Log("Waiting for connection...");
                client = server.AcceptTcpClient();
                Debug.Log("Connected!");

                data = null;
                stream = client.GetStream();

                // Receive message from client    
                int i;
                while ((i = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    data = Encoding.UTF8.GetString(buffer, 0, i);
                    Message message = Decode(data);
                    Debug.Log(message.ToString());
                    lock(Lock)
                    {
                        MessageQue.Add(message);
                    }
                }

                client.Close();
            }
        }
        catch (SocketException e)
        {
            Debug.Log("SocketException: " + e);
        }
        finally
        {
            server.Stop();
        }
    }

    private void OnApplicationQuit()
    {
        stream.Close();
        client.Close();
        server.Stop();
        thread.Abort();
    }

    public void SendMessageToClient(Message message)
    {
        byte[] msg = Encoding.UTF8.GetBytes(Encode(message));
        stream.Write(msg, 0, msg.Length);
        Debug.Log("Sent: " + message);
    }

    // Encode message from struct to Json String
    public string Encode(Message message)
    {
        return JsonUtility.ToJson(message, true);
    }

    // Decode messaage from Json String to struct
    public Message Decode(string json_string)
    {
        Message msg = JsonUtility.FromJson<Message>(json_string);
        return msg;
    }

    //public void Move(Message message)
    //{   
    //    LHand.localPosition = new Vector3(message.LHand_x,message.LHand_y,message.LHand_z);
    //    RHand.localPosition = new Vector3(message.RHand_x,message.RHand_y,message.RHand_z);
    //    Head.localPosition = new Vector3(message.Head_x,message.Head_y,message.Head_z);

    //    Debug.Log("Left Hand: " + LHand.position.ToString());
    //    Debug.Log("Right Hand: " + RHand.position.ToString());
    //    Debug.Log("Head: " + Head.position.ToString());
    //}
}
