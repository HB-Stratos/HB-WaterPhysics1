using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class BuildingCursor : MonoBehaviour
{
    [SerializeField] GameObject buildingCube = null;
    [SerializeField] GameObject placedBlocksParent = null;

    Vector2 mouseVector = Vector2.zero;
    GameObject camRotation = null;
    GameObject camPitch = null;
    float mouseSensitivity = 500;
    float movementSpeed = 5;

    float xRotation = 0;
    float yRotation = 0;

    Vector2 xzMovement = Vector2.zero;
    //float yMovement = 0;

    float timeDelta = 0;

    Dictionary<Vector3, GameObject> placedBlocksDictionary = new Dictionary<Vector3, GameObject>();


    void Start()
    {
        camRotation = transform.GetChild(0).gameObject;
        camPitch = camRotation.transform.GetChild(0).gameObject;
    }
    // Update is called once per frame
    void Update()
    {
        timeDelta += Time.deltaTime;

        xzMovement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        float yMovement = 0;
        if (Input.GetKey(KeyCode.Q)) yMovement -= 1;
        if (Input.GetKey(KeyCode.E)) yMovement += 1;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90, 90);

        camPitch.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);

        yRotation += mouseX;

        camRotation.transform.localRotation = Quaternion.Euler(0, yRotation, 0);

        if (yRotation >= 360) yRotation -= 360;
        else if (yRotation <= -360) yRotation += 360;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E))
        {
            MoveCursor(yMovement);
            timeDelta = 0;
        }
        if (movementSpeed != 0 && timeDelta >= 1 / movementSpeed && (xzMovement.sqrMagnitude != 0 || yMovement != 0))
        {
            MoveCursor(yMovement);
            timeDelta = 0;
        }

        //If the request to place a block is made and there isn't already a block placed at that position
        if (Input.GetKey(KeyCode.LeftShift) && !placedBlocksDictionary.ContainsKey(transform.position))
        {
            GameObject go = Instantiate(buildingCube, transform.position, Quaternion.identity, placedBlocksParent.transform);
            placedBlocksDictionary.Add(transform.position, go);
        }
        if (Input.GetKey(KeyCode.LeftControl) && placedBlocksDictionary.ContainsKey(transform.position))
        {
            Destroy(placedBlocksDictionary[transform.position]);
            placedBlocksDictionary.Remove(transform.position);
        }

        //If it is requested to finalize vehicle and spawn it in the floating simulation
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (placedBlocksDictionary.Count == 0)
            {
                Debug.LogWarning("No Blocks Placed, cannot spawn Vehicle");
            }
            else
            {
                //Find Bounds for Vehicle Array
                Vector3 boundsMin = new Vector3(10000, 10000, 10000);
                Vector3 boundsMax = new Vector3(-10000, -10000, -10000);
                foreach (KeyValuePair<Vector3, GameObject> item in placedBlocksDictionary)
                {
                    boundsMin = Vector3.Min(boundsMin, item.Key);
                    boundsMax = Vector3.Max(boundsMax, item.Key);
                }
                Vector3Int requiredArraySize = Vector3Int.RoundToInt(boundsMax - boundsMin) + Vector3Int.one;

                // Generate Vehicle Array
                int[,,] vehicleArray = new int[requiredArraySize.x, requiredArraySize.z, requiredArraySize.y];


                for (int x = 0; x < vehicleArray.GetLength(0); x++)
                {
                    for (int z = 0; z < vehicleArray.GetLength(2); z++)
                    {
                        for (int y = 0; y < vehicleArray.GetLength(1); y++)
                        {
                            int blockID;

                            if (placedBlocksDictionary.ContainsKey(new Vector3(x, z, y) + boundsMin /*+ new Vector3(1, 0, -1)*/)) blockID = 1;
                            else blockID = 0;

                            //Debug.Log(new Vector3(x, z, y) + boundsMin + "with blocktype" + blockID);

                            vehicleArray[x, y, z] = blockID;
                        }
                    }
                }


                string vehicleArrayPrintout = "{ \n\r";
                for (int x = 0; x < vehicleArray.GetLength(0); x++)
                {
                    vehicleArrayPrintout += "   {";
                    for (int y = 0; y < vehicleArray.GetLength(1); y++)
                    {
                        vehicleArrayPrintout += "{";
                        for (int z = vehicleArray.GetLength(2) - 1; z >= 0; z--)
                        {
                            vehicleArrayPrintout += vehicleArray[x, y, z];
                            if (z != 0) vehicleArrayPrintout += ", ";
                        }

                        vehicleArrayPrintout += "}";
                        if (y != vehicleArray.GetLength(1) - 1) vehicleArrayPrintout += ", ";
                    }

                    vehicleArrayPrintout += "}";
                    if (x != vehicleArray.GetLength(0) - 1) vehicleArrayPrintout += ",\n\r";
                }
                vehicleArrayPrintout += "\n\r }";

                Debug.Log(vehicleArrayPrintout);


                Debug.Log("Spawned Vehicle has " + placedBlocksDictionary.Count + " Blocks and has a Volume of " + vehicleArray.GetLength(0) * vehicleArray.GetLength(1) * vehicleArray.GetLength(2));

                FloatingSceneManager.LoadPhysicsSceneWithVehicle(vehicleArray);

            }
        }

    }


    void MoveCursor(float yMovementIn)
    {
        Vector3 newPosition = transform.position;

        newPosition += AdjustMovementVectorToDirection(new Vector3(Mathf.Sin(yRotation * Mathf.Deg2Rad), 0, Mathf.Cos(yRotation * Mathf.Deg2Rad)));

        newPosition = newPosition.RoundToStepSize(1);

        newPosition += Vector3.up * yMovementIn;

        transform.position = newPosition;
    }

    //TODO fix mess
    Vector3 AdjustMovementVectorToDirection(Vector3 forwardMovement)
    {
        Vector3 output = Vector3.zero;
        Vector3 rightMovement = new Vector3(forwardMovement.z, forwardMovement.y, -forwardMovement.x);
        //Forward/back movement
        output += forwardMovement * xzMovement.y;
        //Right/Left movement
        output += rightMovement * xzMovement.x;

        return output;
    }

}

