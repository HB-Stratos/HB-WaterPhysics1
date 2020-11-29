using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class FloatingTools
{
    //Extensions to Vector3
    /// <summary>
    /// Rounds Vector3 to integer steps.
    /// </summary>
    /// <param name="vector3"></param>
    /// <returns></returns>
    public static Vector3 Round(this Vector3 originalVector)
    {
        return new Vector3(Mathf.Round(originalVector.x), Mathf.Round(originalVector.y), Mathf.Round(originalVector.z));
    }

    /// <summary>
    /// Rounds Vector3 to a given step size.
    /// </summary>
    /// <param name="vector3"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    public static Vector3 RoundToStepSize(this Vector3 originalVector, float stepSize)
    {
        Vector3 roundedVector = originalVector * (1 / stepSize);

        roundedVector = roundedVector.Round();

        return roundedVector * stepSize;
    }

    //This function takes an array and a position in it, from there it outputs the positon vector shifted so the center of the array lies at 0/0/0
    #region TransformArrayPositionToCentered
    public static Vector3 TransformArrayPositionToParentCentered(Vector3 arrayPos, float[,,] vehicle)
    {
        return new Vector3(arrayPos.x - (0.5f * vehicle.GetLength(0) - 0.5f), arrayPos.y - (0.5f * vehicle.GetLength(2) - 0.5f), arrayPos.z - (0.5f * vehicle.GetLength(1) - 0.5f));
    }
    public static Vector3 TransformArrayPositionToCentered(Vector3 arrayPos, bool[,,] vehicle)
    {
        return new Vector3(arrayPos.x - (0.5f * vehicle.GetLength(0) - 0.5f), arrayPos.y - (0.5f * vehicle.GetLength(2) - 0.5f), arrayPos.z - (0.5f * vehicle.GetLength(1) - 0.5f));
    }
    public static Vector3 TransformArrayPositionToCentered(Vector3 arrayPos, int[,,] vehicle)
    {
        return new Vector3(arrayPos.x - (0.5f * vehicle.GetLength(0) - 0.5f), arrayPos.y - (0.5f * vehicle.GetLength(2) - 0.5f), arrayPos.z - (0.5f * vehicle.GetLength(1) - 0.5f));
    }
    #endregion

    public static bool CheckForWaterContact(Vector3 blockPositionInWorldspace)
    {
        //TODO possibly merge with GetSubmergedPercentage, implement accounting for rotation etc, for now it's brutally simple, but easy to upgrade later

        if (blockPositionInWorldspace.y >= 0.5f * Constants.blockSize)
        {
            return false;
        }
        else
        {
            return true;
        }
    }
    public static bool CheckForFullySubmerged(Vector3 blockPositionInWorldspace)
    {
        if (blockPositionInWorldspace.y >= -0.5f * Constants.blockSize)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    public static float GetSubmergedPercentage(Vector3 blockPositionInWorldspace)
    {
        //TODO Implementation of waves etc here

        if (blockPositionInWorldspace.y >= 0.5 * Constants.blockSize) //If the block is above water -> Output 0
        {
            return 0;
        }
        else if (blockPositionInWorldspace.y >= -0.5f * Constants.blockSize) //If the block is partially submerged -> Output percentage of y-axis submerged, does not account for block rotation
        {
            return -(blockPositionInWorldspace.y / Constants.blockSize + 0.5f * Constants.blockSize) + Constants.blockSize; // this is rather confusing, but it works
        }
        else
        {
            return 1;
        }
    }

    //This method finds the Center of Mass within a 3d mass distribution array (the result will be relative to the main vehicle's parent object, i.e. the object that runs the VehicleController script)
    public static Vector3 CalculateCoM(float[,,] massDistribution)
    {
        Vector3 centerOfMass = Vector3.zero;
        float alreadyProcessedMass = 0; //this Variable keeps track how much of the vehice has been calculated, and therefore how big the impact of a new mass should be.

        //looping through all points within the array while keeping track of the x, y and z position of said point
        for (int x = 0; x < massDistribution.GetLength(0); x++)
        {
            for (int z = 0; z < massDistribution.GetLength(1); z++)
            {
                for (int y = 0; y < massDistribution.GetLength(2); y++)
                {
                    //The mass distribution array positions start at 0,0,0, but the parent is placed in the center of the vehicle volume. Therefore each mass point must be offset so the end result is relative to the parent and not 0,0,0 
                    Vector3 newMassToAddPosition = TransformArrayPositionToParentCentered(new Vector3(x, y, z), massDistribution);
                    float newMassToAdd = massDistribution[x, z, y];

                    if (newMassToAdd != 0)
                    {
                        //A linear interpolation is used between the point of all already calculated masses and the new mass. To find out how much the new point should influence the position of the overall center of mass, the ratio between all already calculated masses and the new mass is used. 
                        float interpolation01 = newMassToAdd / (alreadyProcessedMass + newMassToAdd);
                        centerOfMass = Vector3.Lerp(centerOfMass, newMassToAddPosition, interpolation01);
                        //Lastly, the new mass is added to the total masses for the next iteration of the loop.
                        alreadyProcessedMass += newMassToAdd;
                    }

                }
            }
        }

        return centerOfMass;
    }

    public static float CalculateVehicleMass(float[,,] massDistribution)
    {
        float vehicleMass = 0f;
        foreach (float massPoint in massDistribution)
        {
            vehicleMass += massPoint;
        }
        return vehicleMass;
    }

    public static Vector3 GetVectorBetweenTwoPoints(Vector3 initialPoint, Vector3 targetPoint)
    {
        Vector3 outputVector;

        outputVector = targetPoint - initialPoint;

        return outputVector;
    }

    public static float CalculateInertia(Vector3 axis, float[,,] massDistribution, Vector3 CoM)
    {
        float inertia = 0;

        for (int x = 0; x < massDistribution.GetLength(0); x++)
        {
            for (int z = 0; z < massDistribution.GetLength(1); z++)
            {
                for (int y = 0; y < massDistribution.GetLength(2); y++)
                {
                    Vector3 targetPoint = new Vector3(x, y, z);
                    inertia += CalculateInertiaSinglePoint(targetPoint, massDistribution[x, z, y], axis, CoM);
                }
            }
        }

        return inertia;
    }
    public static float CalculateInertiaSinglePoint(Vector3 Point, float mass, Vector3 axis, Vector3 CoM)
    {
        Vector3 projectedMassRadius = FloatingTools.GetVectorBetweenTwoPoints(FloatingTools.ProjectPointOntoAxisWithOffset(axis, Point, CoM), Point);
        float intertia = projectedMassRadius.sqrMagnitude * mass;
        return intertia;
    }
    public static Vector3 ProjectPointOntoAxis(Vector3 axis, Vector3 point) //Axis has to go through 0/0/0
    {
        Vector3 axisNormalized = axis.normalized;
        Vector3 output = Vector3.Dot(point, axisNormalized) * axisNormalized;
        return output;
    }
    public static Vector3 ProjectPointOntoAxisWithOffset(Vector3 axis, Vector3 point, Vector3 offset)
    {
        Vector3 output = ProjectPointOntoAxis(axis, point - offset) + offset;
        return output;
    }

    public static Vector3? TransfromDictionaryIndexToRelativePosition(Vector3 dictionaryKey, Dictionary<Vector3, FloatingBlock> dictionary)
    {
        Vector3? output = null;
        FloatingBlock fb;
        if (dictionary.TryGetValue(dictionaryKey, out fb))
        {
            output = fb.relativePosition;
        }
        if (output == null)
        {
            Debug.LogWarning("Block Key " + dictionaryKey +" does not exist in dictionary, returned Vector3? may be causing issues");
        }

        return output;
    }
    public static class Constants
    {
        public static float blockSize = 1f;
        public static float waterDensity = 1000; //water mass per m³
    }


}


//The Force struct is used for the physics engine, it defines a [force], a Vector3 that describes the direction of the force with the magnitude of the Vector's magnitude. This force is to be applied at the worlspace position of [position]
public struct Force
{
    public Vector3 position;
    public Vector3 force;

    public Force(Vector3 position, Vector3 force)
    {
        this.position = position;
        this.force = force;
    }
}

public class FloatingBlock //This used to be a struct and may become one again as dictionary.Add() is much more performant with value types. However this currently leads to issues 
{
    public bool isFlooded;
    public Vector3 relativePosition; //Position relative to root object, the empty that owns vehicleController
    public int blockType; //0 is air, 1 is solid, more to be added (maybe)
    public float floodedPercentage; //this will probably not be used in the scope of this project, but may be included in later iterations
    public float blockMass;

    public FloatingBlock(bool isFlooded, Vector3 relativePosition, int blockType, float blockMass)
    {
        this.isFlooded = isFlooded;
        this.relativePosition = relativePosition;
        this.blockType = blockType;
        this.blockMass = blockMass;

        floodedPercentage = 0;
    }
    public FloatingBlock(bool isFlooded, Vector3 relativePosition, int blockType, float floodedPercentage, float blockMass) : this(isFlooded, relativePosition, blockType, blockMass)
    {
        this.floodedPercentage = floodedPercentage;
    }
}
