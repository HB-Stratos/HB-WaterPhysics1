using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TestVehicle
{

    public static float blockMass = 1050 * Mathf.Pow(FloatingTools.Constants.blockSize, 3); //Mass per block in kg
    public static int[,,] testVehicle =
        {
            {{0,0,0},{0,0,0},{0,1,1},{0,0,0},{0,0,0}},    // THIS IT THE TOP: ^     (X) (0)
            {{0,1,0},{1,1,1},{1,0,0},{1,1,1},{0,1,0}},    // THIS IS THE RIGHT: >   (Z) (1)
            {{1,1,1},{1,0,0},{1,0,0},{1,0,0},{1,1,1}},    // THIS IS UP: +          (Y) (2)
            {{1,1,1},{1,0,0},{1,0,0},{1,0,0},{1,1,1}},    //The first two dimensions are horizontal
            {{0,1,0},{1,1,1},{1,1,1},{1,1,1},{0,1,0}}
        };

    public static void OverrideDefaultVehicle(int[,,] newVehicle)
    {
        testVehicle = newVehicle;
        Debug.Log("new Vehicle registered");
    }



    public static bool[,,] ToBool()
    {

        bool[,,] output = new bool[testVehicle.GetLength(0), testVehicle.GetLength(1), testVehicle.GetLength(2)];

        for (int x = 0; x < testVehicle.GetLength(0); x++)
        {
            for (int z = 0; z < testVehicle.GetLength(1); z++)
            {
                for (int y = 0; y < testVehicle.GetLength(2); y++)
                {

                    if (testVehicle[x, z, y] >= 1)
                    {
                        output[x, z, y] = true;
                    }
                    else
                    {
                        output[x, z, y] = false;
                    }

                }
            }
        }

        return output;
    }

    public static float[,,] ToMassDistribution()
    {

        float[,,] output = new float[testVehicle.GetLength(0), testVehicle.GetLength(1), testVehicle.GetLength(2)];

        for (int x = 0; x < testVehicle.GetLength(0); x++)
        {
            for (int z = 0; z < testVehicle.GetLength(1); z++)
            {
                for (int y = 0; y < testVehicle.GetLength(2); y++)
                {

                    if (testVehicle[x, z, y] >= 1)
                    {
                        output[x, z, y] = blockMass;
                    }
                    else
                    {
                        output[x, z, y] = 0;
                    }

                }
            }
        }

        return output;
    }

}
