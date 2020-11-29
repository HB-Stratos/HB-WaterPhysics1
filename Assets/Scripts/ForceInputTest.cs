using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//This class adds a cursor that is intended for easy force application
public class ForceInputTest : MonoBehaviour
{
    Vector3 cursorPosition = Vector3.zero;
    Vector3 cursorForce = Vector3.zero;
    bool isInMoveMode = true;
    [SerializeField] GameObject cursor = null;
    [SerializeField] GameObject cursorTip = null;
    float movementSpeed = .05f;
    [SerializeField] GameObject vehicleController = null;
    VehicleController vehicleControllerScript;


    // Start is called before the first frame update
    void Start()
    {
        vehicleControllerScript = (VehicleController)vehicleController.GetComponent("VehicleController");
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isInMoveMode = !isInMoveMode;
            Debug.Log("Is in move mode: " + isInMoveMode);
        }

        Vector3 movement = Vector3.zero;

        #region Inputs
        if (Input.GetKey(KeyCode.A))
        {
            movement += new Vector3(0, 0, -1);
        }
        if (Input.GetKey(KeyCode.D))
        {
            movement += new Vector3(0, 0, 1);
        }
        if (Input.GetKey(KeyCode.S))
        {
            movement += new Vector3(-1, 0, 0);
        }
        if (Input.GetKey(KeyCode.W))
        {
            movement += new Vector3(1, 0, 0);
        }
        if (Input.GetKey(KeyCode.Q))
        {
            movement += new Vector3(0, -1, 0);
        }
        if (Input.GetKey(KeyCode.E))
        {
            movement += new Vector3(0, 1, 0);
        }
        #endregion
        //interchange AD and WS
        movement = new Vector3(movement.z, movement.y, movement.x);

        if (isInMoveMode)
        {
            cursorPosition += movement * movementSpeed;
        }
        else
        {
            cursorForce += movement * movementSpeed;
        }

        cursor.transform.position = cursorPosition;
        cursorTip.transform.position = cursor.transform.TransformPoint(cursorForce);

        if (cursorPosition != cursorTip.transform.position)
        {
            Debug.DrawLine(cursorPosition, cursorTip.transform.position, Color.green, .1f);
        }

        if (Input.GetKey(KeyCode.LeftShift))
        {
            vehicleControllerScript.ApplyForceOnVehicle(cursorPosition, cursorForce * 1000 * Mathf.Pow(FloatingTools.Constants.blockSize, 3));
        }

        if (Input.GetKeyDown(KeyCode.Backspace)) FloatingSceneManager.LoadBuilderScene();
    }
}
