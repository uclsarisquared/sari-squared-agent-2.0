using System;
using System.Collections.Generic;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AI;
using Newtonsoft.Json.Linq;
public class SocketIOServer : MonoBehaviour
{

    // navmesh
    [SerializeField] private Transform movePositionTransform;

    // SocketIO server IP and port
    public string serverIP = "localhost";
    public int serverPort = 6060;

    public SocketIOUnity socket;

    public GameObject agentCamera;
    private GameObject rightHandItem;
    private bool rightHandUsed;
    private NavMeshAgent _agent;
    private LayerMask interactableLayerMask;
    private bool shouldCaptureNextFrame;

    void Update()
    {
        // checks if there is a destination
        if (_agent.hasPath)
        {
            if (movePositionTransform != null)
            {
                movePositionTransform.position = _agent.destination;
            }
            shouldCaptureNextFrame = true;
        }
        else
        {
            
        }
        // This runs every frame. We wait for the signal from the socket.
        if (shouldCaptureNextFrame)
        {
            // We immediately set it to false so we don't capture 100 times a second
            shouldCaptureNextFrame = false;
            
            // We still want to wait for the end of the frame for a clean image
            StartCoroutine(CaptureManual());
        }
    }

    private System.Collections.IEnumerator WaitForArrival()
{
    // Wait a frame for the agent to start calculating the path
    yield return null; 

    // Loop while the agent is still moving or calculating
    while (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance)
    {
        yield return null; 
    }

    // Once arrived, trigger the capture
    ServerLog("Agent arrived at destination. Capturing frame.");
    shouldCaptureNextFrame = true;

    Vector3 currentEuler = agentCamera.transform.eulerAngles;
    agentCamera.transform.rotation = Quaternion.Euler(currentEuler.x, 0f, 0f);
    // remove the path so that the agent doesn't keep trying to move to the destination and can receive new commands
    _agent.ResetPath();
    // then send to the server that we have successfully arrived at the destination and captured the frame
    var response = new Dictionary<string, bool> {{"success", true}};
    socket.Emit("UNITY_RESPONSE", response);
}
    private System.Collections.IEnumerator CaptureManual()
    {
        yield return new WaitForEndOfFrame();

        Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
        if (tex != null)
        {
            byte[] imgBytes = tex.EncodeToJPG();
            string path = "C:/Sari/sari-squared-agent-2.0/currentview/current_view.jpg";
            System.IO.File.WriteAllBytes(path, imgBytes);
            DestroyImmediate(tex);
            Debug.Log("SUCCESS: Manual Capture saved to disk.");
        }
    }

    void Start()
        {
            // 3. Initialize Agent
            _agent = agentCamera.GetComponent<NavMeshAgent>();
            shouldCaptureNextFrame = true;
            rightHandUsed = false;
            // Only trigger items in the "Interactable" layer
            interactableLayerMask = LayerMask.GetMask("SariInteractable");
            SetupSocket();
        }

        void SetupSocket()
        {
            var uri = new Uri($"http://{serverIP}:{serverPort}");
            socket = new SocketIOUnity(uri, new SocketIOOptions
            {
                Query = new Dictionary<string, string> { {"token", "UNITY" } },
                EIO = EngineIO.V4,
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
            });

            socket.JsonSerializer = new NewtonsoftJsonSerializer();
            socket.unityThreadScope = SocketIOUnity.UnityThreadScope.Update;

            // --- NAVIGATION COMMANDS ---

            socket.OnUnityThread("MOVE_FWD", (data) =>
            {
                float amount = data.GetValue<float>();
                // Use agent.Move to respect NavMesh boundaries
                ServerLog("recv MOVE_FWD: " + amount);
                _agent.Move(agentCamera.transform.forward * amount);
                shouldCaptureNextFrame = true;
            });

            socket.OnUnityThread("MOVE_BCK", (data) =>
            {
                float amount = data.GetValue<float>();
                ServerLog("recv MOVE_BCK: " + amount);
                _agent.Move(-agentCamera.transform.forward * amount);
                shouldCaptureNextFrame = true;
            });

            socket.OnUnityThread("MOVE_LFT", (data) =>
            {
                float amount = data.GetValue<float>();
                _agent.Move(-agentCamera.transform.right * amount);
                shouldCaptureNextFrame = true;
            });

            socket.OnUnityThread("MOVE_RGT", (data) =>
            {
                float amount = data.GetValue<float>();
                _agent.Move(agentCamera.transform.right * amount);
                shouldCaptureNextFrame = true;
            });

            // Rotation doesn't affect NavMesh positioning, so transform.Rotate is fine
            socket.OnUnityThread("TURN_LFT", (data) =>
            {
                float angle = data.GetValue<float>();
                agentCamera.transform.Rotate(Vector3.up, -angle);
                shouldCaptureNextFrame = true;
            });

            socket.OnUnityThread("TURN_RGT", (data) =>
            {
                float angle = data.GetValue<float>();
                agentCamera.transform.Rotate(Vector3.up, angle);
                shouldCaptureNextFrame = true;
            });

            socket.OnUnityThread("LOOK_UP", (data) =>
            {
                float angle = data.GetValue<float>();
                agentCamera.transform.Rotate(Vector3.right, -angle);
                shouldCaptureNextFrame = true;
            });

            socket.OnUnityThread("LOOK_DOWN", (data) =>
            {
                float angle = data.GetValue<float>();
                agentCamera.transform.Rotate(Vector3.right, angle);
                shouldCaptureNextFrame = true;
            });

            socket.OnUnityThread("MOVE_TO_ITEM", (data) =>
            {
                // expecting integer for placeholder
                var item = data.GetValue<int>();
                
                ServerLog($"recv MOVE_TO_ITEM: {item}");

                // Tell the NavMesh to find a path to these coordinates
                if (item == 1)
                {
                    _agent.SetDestination(new Vector3(-2.84716f, 0.03333334f, 2.270404f));
                } else if (item == 2)
                {
                    _agent.SetDestination(new Vector3(-3.245211f,0f,-1.046027f));
                }
                else if (item == 3)
                {
                    _agent.SetDestination(new Vector3(-4.659885f,0f,-4.23723f));
                } else
                {
                    ServerLog($"Unknown item {item} in MOVE_TO_ITEM command");
                    var response = new Dictionary<string, bool> {{"success", false}};
                    socket.Emit("UNITY_RESPONSE", response);
                }
                // asynchronously wait for the agent to arrive at the destination before capturing the frame
                
                // We don't capture the frame immediately because the agent needs time to travel.
                StartCoroutine(WaitForArrival());
                
            });
            socket.OnUnityThread("PICK_ITEM", (data) =>
            {
                JArray jsonData = data.GetValue<JArray>();

                int x = jsonData[0].Value<int>()*1920/100;
                int y = 1080 - jsonData[1].Value<int>()*1080/100;
                ServerLog($"recv PICK_ITEM at pixel: ({x}, {y})");
                if (PickupAtPixel(x, y))
                {
                    // echo back success if we were able to pick up an item
                    var response = new Dictionary<string, bool> {{"success", true}};
                    socket.Emit("UNITY_RESPONSE", response);
                }
                else
                {
                    // echo back failure if we were not able to pick up an item
                    var response = new Dictionary<string, bool> {{"success", false}};
                    socket.Emit("UNITY_RESPONSE", response);
                }
                shouldCaptureNextFrame = true;
                
            });
            socket.Connect();
        }
    bool PickupAtPixel(int x, int y)
    {
        // Convert screen pixel to a ray
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(x, y, 0));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, interactableLayerMask))
        {  
            // Check if we hit something within 1.5 meters (to prevent picking up items that are too far away)
            if (hit.distance > 1.5f)
            {
                ServerLog($"Hit {hit.transform.name} but it's too far away ({hit.distance}m).");
                return false;
            }
            string hitName = hit.transform.name;

            if (hit.collider.CompareTag("Wall")) return false;
            
            SariUIHandler.Instance.UpdateInfoText(hitName);
            
            OutlineController outlineControllerScript = hit.collider.GetComponent<OutlineController>();
            if (outlineControllerScript)
            {
                outlineControllerScript.OnGaze();
            }
            
            // Grab the item, no need to check for "Return" key since this is only called on mouse click
            if (!rightHandUsed)
            {
                var selectedItem =
                    Resources.Load<GameObject>("Prefabs/Products/" + hitName);
                selectedItem.transform.position = Vector3.zero;
                Vector3 handLocation = agentCamera.transform.position 
                                       + agentCamera.transform.forward * 0.2f 
                                       + agentCamera.transform.right * 0.1f 
                                       + agentCamera.transform.up * -0.1f
                                       + agentCamera.transform.up * 1.35f;
                ItemBBoxInfo itemBBoxInfo = hit.collider.GetComponent<ItemBBoxInfo>();
                itemBBoxInfo.DeleteFrontmostItem();
                DisablePhysics(selectedItem);
                selectedItem = Instantiate(selectedItem, handLocation,
                    agentCamera.transform.rotation, agentCamera.transform);
                selectedItem.transform.Rotate(Vector3.up, -60);
                rightHandItem = selectedItem;
                rightHandUsed = true;
            }
            return true;
        }
        // If raycast did not hit anything, return false
        return false;
    }

    void DisablePhysics(GameObject item)
    {
        Rigidbody rb = item.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
        BoxCollider boxCollider = item.GetComponentInChildren<BoxCollider>();
        boxCollider.enabled = false;
        
        MeshCollider[] cols = item.GetComponentsInChildren<MeshCollider>(true);
        foreach (var c in cols)
            c.isTrigger = true;
    }
    void ServerLog(string msg)
    {
        Debug.Log($"socket >> {msg}");
    }
    
    async void OnApplicationQuit()
    {
        if (socket != null && socket.Connected) 
        {
            await socket.DisconnectAsync();
        }
        socket?.Dispose();
    }
}
