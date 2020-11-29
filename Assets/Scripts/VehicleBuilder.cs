using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VehicleBuilder : MonoBehaviour
{
    [SerializeField] GameObject testCube = null;
    [SerializeField] GameObject parent = null;

    private void Start()
    {
        GenerateVehicle(TestVehicle.ToBool());
    }

    public void GenerateVehicle(bool[,,] vehicle)
    {

        //still using old methods, but upgrading would take time and isn't necessary right now as it's purely visualisation
        for (int x = 0; x < vehicle.GetLength(0); x++)
        {
            for (int z = 0; z < vehicle.GetLength(1); z++)
            {
                for (int y = 0; y < vehicle.GetLength(2); y++)
                {
                    if (vehicle[x, z, y] == true)
                    {
                        Vector3 newBlockPosition = FloatingTools.Constants.blockSize * (FloatingTools.TransformArrayPositionToCentered(new Vector3(x, y, z), vehicle));
                        GameObject go = Instantiate(testCube);
                        go.transform.position = parent.transform.TransformPoint(newBlockPosition);
                        go.transform.rotation = parent.transform.rotation;
                        go.transform.parent = parent.transform;
                        go.transform.localScale = Vector3.one * FloatingTools.Constants.blockSize;
                    }
                }
            }
        }

    }
}


