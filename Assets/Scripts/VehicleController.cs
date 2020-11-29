using System.Transactions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class VehicleController : MonoBehaviour
{

    //[SerializeField] GameObject testCube = null; // here for testing only, to be removed

    float timeTakenForSimulation = 0f;//for debugging
    string logOutputTimes;
    string logOutputFloodedBlockCount;
    int iterationsBeforeLogPrint = 1000;
    int currentIterations;

    [SerializeField] bool drawDebug = true;
    [SerializeField] bool hasDrag = true;
    [SerializeField] float linearDragStrength = 0.975f;
    [SerializeField] float rotationDragStrength = 0.975f;
    GameObject vehicleParent = null;
    List<Force> forcesToApply = new List<Force>();
    Vector3 currentLinearForce = Vector3.zero;
    Vector3 currentLinearAcceleration = Vector3.zero;
    Vector3 currentLinearVelocity = Vector3.zero;
    Vector3 currentTorqueVector = Vector3.zero;
    Vector3 currentAngularAccleration = Vector3.zero;
    Vector3 currentRotationVelocity = Vector3.zero;
    Vector3 CoM = Vector3.zero;
    float vehicleMass = 0;
    float[,,] vehicleMassDistribution;
    int[,,] vehicleBlockTypeDistribution;

    Dictionary<Vector3, FloatingBlock> allBlocks = new Dictionary<Vector3, FloatingBlock>();
    List<KeyValuePair<Vector3, FloatingBlock>> allVehicleBlocks;
    List<KeyValuePair<Vector3, FloatingBlock>> allFloodedBlocks = new List<KeyValuePair<Vector3, FloatingBlock>>();
    //Dictionary<Vector3, FloatingBlock> floodedBlocks = new Dictionary<Vector3, FloatingBlock>();
    //List<KeyValuePair<Vector3, FloatingBlock>> allNotFloodedBlocks = new List<KeyValuePair<Vector3, FloatingBlock>>();
    bool isInWaterContact = false;
    bool wasInWaterContact = false; // a secondary variable to track if there was a change in water contact
    Vector3? WaterContactPoint = null;
    float secureOutOfWaterHeight = 0; // if the parent is this high above the water then no check for water contact is needed 

    bool isInNoPhysicsMode = false;
    Vector3 currentGravity = Vector3.zero; //gets changed in RecalculateVehicle()
    [SerializeField] bool noPhysicsLoweringEnabled = true;


    void Awake()
    {
        RecalculateVehicle();
        vehicleParent = transform.gameObject;
        if (noPhysicsLoweringEnabled) { HandleNoPhysicsWaterLoweringInitial(); }
        //Time.timeScale = 0.05f; //For testing/visualisation
    }

    void FixedUpdate()
    {
        timeTakenForSimulation = Time.realtimeSinceStartup;

        HandleFloatingSimulation();
        HandleFloatingForceApplication();
        if (noPhysicsLoweringEnabled) { HandleNoPhysicsWaterLoweringLoop(); }
        HandleVehicleMovement();
        if (hasDrag)
        {
            currentLinearVelocity *= linearDragStrength;
            currentRotationVelocity *= rotationDragStrength;
        }

        //Visualisation for testing
        if (drawDebug) foreach (KeyValuePair<Vector3, FloatingBlock> keyPair in allBlocks)
            {
                if (keyPair.Value.isFlooded)
                {
                    Vector3 worldPosOfFloodedBlock = vehicleParent.transform.TransformPoint(keyPair.Value.relativePosition);
                    Debug.DrawLine(worldPosOfFloodedBlock, worldPosOfFloodedBlock + (Vector3.up * 0.2f * FloatingTools.Constants.blockSize), Color.red, 0.02f);
                }
            }

        timeTakenForSimulation = Time.realtimeSinceStartup - timeTakenForSimulation;

        logOutputTimes += "TimeTakenForSimulation " + timeTakenForSimulation + "\n";
        logOutputFloodedBlockCount += "FloodedBlockCount " + allFloodedBlocks.Count + "\n";
        if (currentIterations >= iterationsBeforeLogPrint)
        {
            Debug.Log(logOutputFloodedBlockCount);
            Debug.Log(logOutputTimes);
            currentIterations = 0;
            logOutputTimes = "";
            logOutputFloodedBlockCount = "";
        }
        else currentIterations++;
    }

    void HandleVehicleMovement()
    {
        if (!isInNoPhysicsMode)
        {
            CalculateLinearForces();
            CalculateLinearAcceleration();
            if (drawDebug) Debug.DrawRay(CoM, currentLinearAcceleration, Color.blue, .1f);

            CalculateTorqueForces();
            CalculateAngluarAcceleration();
            if (drawDebug) Debug.DrawRay(CoM, currentAngularAccleration, Color.red, .1f);

            currentLinearVelocity += currentLinearAcceleration * Time.fixedDeltaTime;
            vehicleParent.transform.position += currentLinearVelocity * Time.fixedDeltaTime;

            currentRotationVelocity += currentAngularAccleration * Time.fixedDeltaTime;
            vehicleParent.transform.RotateAround(vehicleParent.transform.TransformPoint(CoM), currentRotationVelocity, -(Mathf.Rad2Deg * currentRotationVelocity.magnitude) * Time.fixedDeltaTime);
        }
        else
        {
            //the below block can be commented out if accumulated movement should be stored when noPhyiscs is on
            ClearAccumulatedMovementSpeeds();
        }
        forcesToApply.Clear();
    }
    void ClearAccumulatedMovementSpeeds()
    {
        currentLinearForce = Vector3.zero;
        currentLinearAcceleration = Vector3.zero;
        currentLinearVelocity = Vector3.zero;
        currentTorqueVector = Vector3.zero;
        currentAngularAccleration = Vector3.zero;
        currentRotationVelocity = Vector3.zero;
    }



    void HandleFloatingSimulation()
    {
        if (vehicleParent.transform.position.y <= secureOutOfWaterHeight)
        {
            wasInWaterContact = isInWaterContact; //syncing up the lagging variable before recalculating the up to time one
            WaterContactPoint = CheckForVehicleInWaterContact();

            if (isInWaterContact && !wasInWaterContact) //if the vehicle just made contact with the water
            {
                HandleInitialWaterContact();
            }

            else if (isInWaterContact && wasInWaterContact) //only execute when vehicle already touched water last frame, aka leave one physics frame break after first water contact code
            {
                StepFloatingCalculation();
            }

            else if (!isInWaterContact && wasInWaterContact) //if vehicle has just left the water
            {
                foreach (KeyValuePair<Vector3, FloatingBlock> keyPair in allFloodedBlocks)
                {
                    keyPair.Value.isFlooded = false;
                }
                allFloodedBlocks.Clear();
            }

            if (isInWaterContact && allFloodedBlocks.Count == 0)
            {
                HandleInitialWaterContact();
            }
        }
    }

    void HandleInitialWaterContact()
    {
        if (WaterContactPoint == null) Debug.LogError("WaterContactPoint was null, this should be impossible");
        Vector3 lowestDetectedBlock = (Vector3)WaterContactPoint;//water contact point can be used as inital because one adjacend block will always be lower
        Vector3 lowestDetectedBlockInWorldspace = vehicleParent.transform.TransformPoint((Vector3)FloatingTools.TransfromDictionaryIndexToRelativePosition((Vector3)WaterContactPoint, allBlocks));

        FloatingBlock lowestDetectedBlockObject = null;

        DetermineLowestAdjacentAirBlock(ref lowestDetectedBlock, ref lowestDetectedBlockInWorldspace, ref lowestDetectedBlockObject);

        //allBlocks[lowestDetectedBlock].isFlooded = true; //this works, but the below method is better
        lowestDetectedBlockObject.isFlooded = true;
        allFloodedBlocks.Add(new KeyValuePair<Vector3, FloatingBlock>(lowestDetectedBlock, lowestDetectedBlockObject));

        //Instantiate(testCube, vehicleParent.transform.TransformPoint(lowestDetectedBlockObject.relativePosition), vehicleParent.transform.rotation); //For testing only
    }

    //This block determines which of the blocks touching the water contacting solid block is the lowest, therefore deteriming the start position for the flooding calcs
    void DetermineLowestAdjacentAirBlock(ref Vector3 lowestDetectedBlock, ref Vector3 lowestDetectedBlockInWorldspace, ref FloatingBlock lowestDetectedBlockObject)
    {
        Vector3[] offsetVectors = new Vector3[6] { new Vector3(1, 0, 0), new Vector3(-1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, -1, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1) };
        for (int i = 0; i < 6; i++)
        {
            Vector3 blockCurrentlyCheckedPostion = (Vector3)WaterContactPoint + offsetVectors[i];

            FloatingBlock blockCurrentlyCheckedObject;
            if (allBlocks.TryGetValue(blockCurrentlyCheckedPostion, out blockCurrentlyCheckedObject))
            {
                Vector3 blockCurrentlyCheckedPositionInWorldspace = vehicleParent.transform.TransformPoint(blockCurrentlyCheckedObject.relativePosition);

                if (blockCurrentlyCheckedPositionInWorldspace.y < lowestDetectedBlockInWorldspace.y)
                {
                    if (blockCurrentlyCheckedObject.blockType == 0)
                    {
                        lowestDetectedBlock = blockCurrentlyCheckedPostion;
                        lowestDetectedBlockInWorldspace = blockCurrentlyCheckedPositionInWorldspace;
                        lowestDetectedBlockObject = blockCurrentlyCheckedObject;
                    }
                }
            }
        }

    }

    void StepFloatingCalculation()
    {
        /*
            This method iterates backwards over all blocks that are marked as flooded
            if the currently checked mid block is not submerged it gets removed as it is no longer flooded
            however if the checked mid block is still submerged, it starts it's check routine, checking each adjacent block for
                if it is in the allBlocks Dictionary and therefore a valid block to be checked
                AND if it is a block that is not already flooded
                AND if it is a block that is air and therefore can be flooded
                AND if that block is actually submerged
            THEN add it to the list of flooded blocks for the next iteration of the above checks
            AND also set the FloatingBlock Object's flooded variable to true
        */
        Vector3[] offsetVectors = new Vector3[6] { new Vector3(1, 0, 0), new Vector3(-1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, -1, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1) };

        for (int i = allFloodedBlocks.Count - 1; i >= 0; i--) //iterate backwards so that RemoveAt(i) doesn't cause issues
        {
            KeyValuePair<Vector3, FloatingBlock> keyPair = allFloodedBlocks[i];

            Vector3 currentMiddleBlockPosition = keyPair.Key;
            Vector3 currentMiddleBlockPositionInWorldspace = vehicleParent.transform.TransformPoint(keyPair.Value.relativePosition);

            //check for if current mid block is still submerged
            if (FloatingTools.CheckForWaterContact(currentMiddleBlockPositionInWorldspace)) // if the current block is submerged
            {
                for (int j = 0; j < 6; j++)
                {
                    Vector3 currentlyCheckedBlockPosition = keyPair.Key + offsetVectors[j];

                    FloatingBlock currentlyCheckedBlockObject;
                    if (allBlocks.TryGetValue(currentlyCheckedBlockPosition, out currentlyCheckedBlockObject))
                    {
                        if (!allBlocks[currentlyCheckedBlockPosition].isFlooded && allBlocks[currentlyCheckedBlockPosition].blockType == 0) //tryGetValue not required as this was checked in previous if
                        {
                            Vector3 currentlyCheckedBlockPositionInWorldspace = vehicleParent.transform.TransformPoint(currentlyCheckedBlockObject.relativePosition);

                            if (FloatingTools.CheckForWaterContact(currentlyCheckedBlockPositionInWorldspace))
                            {
                                currentlyCheckedBlockObject.isFlooded = true;
                                allFloodedBlocks.Add(new KeyValuePair<Vector3, FloatingBlock>(currentlyCheckedBlockPosition, currentlyCheckedBlockObject));
                            }
                        }
                    }
                }
            }
            else
            {
                allFloodedBlocks[i].Value.isFlooded = false;
                allFloodedBlocks.RemoveAt(i); //if the current block is no longer submerged, remove it
            }

        }

    }



    void HandleFloatingForceApplication()
    {

        foreach (KeyValuePair<Vector3, FloatingBlock> keyPair in allVehicleBlocks)
        {
            Force gravityForce = new Force(vehicleParent.transform.TransformPoint(keyPair.Value.relativePosition), currentGravity * keyPair.Value.blockMass);
            ApplyForceOnVehicle(gravityForce);
            if (drawDebug) Debug.DrawRay(vehicleParent.transform.TransformPoint(keyPair.Value.relativePosition), gravityForce.force * 0.001f, Color.green, 0.02f);
        }

        if (isInWaterContact)
        {
            foreach (KeyValuePair<Vector3, FloatingBlock> keyPair in allBlocks)
            {
                if (!keyPair.Value.isFlooded && FloatingTools.CheckForWaterContact(vehicleParent.transform.TransformPoint(keyPair.Value.relativePosition)))
                {
                    Vector3 currentBlockPositionInWorldspace = vehicleParent.transform.TransformPoint(keyPair.Value.relativePosition);
                    Force displacementForce = new Force(currentBlockPositionInWorldspace, Vector3.up * CalculateBlockDisplacement(currentBlockPositionInWorldspace) * FloatingTools.Constants.waterDensity * -currentGravity.y);
                    ApplyForceOnVehicle(displacementForce);
                    //Debug.Log("disp. Force applied was at: " + keyPair.Value.relativePosition + " with magn: " + displacementForce.force.y);
                    if (drawDebug) Debug.DrawRay(currentBlockPositionInWorldspace, displacementForce.force * 0.001f, Color.blue, 0.02f);
                }
            }
        }
    }

    float CalculateBlockDisplacement(Vector3 blockPositionInWorldspace)
    {
        float submergedPecentage = FloatingTools.GetSubmergedPercentage(blockPositionInWorldspace);
        //Displacement is calculated  by taking the area of one cube side times the height, this calculation is inaccurate for rotated blocks just as GetSubmergedPercentage() itself
        float output = Mathf.Pow(FloatingTools.Constants.blockSize, 2) * submergedPecentage * FloatingTools.Constants.blockSize;
        //Debug.Log("Displacement was: " + output);
        return output;
    }



    void HandleNoPhysicsWaterLoweringInitial()
    {
        isInNoPhysicsMode = true;
        vehicleParent.transform.position = new Vector3(vehicleParent.transform.position.x, secureOutOfWaterHeight, vehicleParent.transform.position.z);
        Debug.Log(secureOutOfWaterHeight);
    }

    int physicsStepsOverThreshholdRequired = 20;
    void HandleNoPhysicsWaterLoweringLoop()
    {
        CalculateLinearForces(); //nophysics stops this calculation, so it is repeated here
        if (currentLinearForce.y <= 1 && vehicleParent.transform.position.y > -secureOutOfWaterHeight)
        {
            vehicleParent.transform.position += Vector3.down * 0.5f * Time.fixedDeltaTime;
        }
        else
        {
            physicsStepsOverThreshholdRequired--;
            if (physicsStepsOverThreshholdRequired <= 0)
            {
                isInNoPhysicsMode = false;
                noPhysicsLoweringEnabled = false;
            }
        }
    }
    /** New Method
    Convert RunningCalculationArray into Dictionary
    Vehicle will always spawn ABOVE waterline and is lowered down in noPhyiscs mode until gravitaionalForce == buoyancyForce || vehicleIsFullySubmerged -> Idea: Use Time.timeScale to speed up this process

    Running Calculations:
    Detect Water contact by looping over all blocks of the vehicle and testing for isSubmerged
    Once water contacts is made begin calculation, tree branch from first point of contact to all other submerged points, in case of multiple simultaneous contact points Use the one with lowest coordinate value
    All vehicle contact points that are-
    -submerged try to expand in every direction that is not already occupied with another vehilce contacting point. If new points are found, add them to Dictionary
    -not submerged (leftovers from previous physics frame) get removed from the Dictionary

    Vehicle Damage Handling:
    -make edit to vehicle dictionary, simulation should be able to handle it
    **/


    /**
    Make list of all vehicle block as no key lookups are needed
    hanling for flooded blocks necessary, perhaps only ask for keys in the dic that are below the waterline
    would require generating an array with all blocks that are currently under water, then loop through that, accessing the main dic
    if making this requires a loop over the whole main dic than this is pointless
       **/

    //Note: Calculations do not account for air pressure in upside down open compartments, they will always considered to be flooded up to the water line
    //Note: with high rotation or movement speeds the simulation may lag behind - realistic because water has momentum
    //Note: Calculations do not account for water spray, water is always assumed to be a solid surface, height (waves) is also not supported
    //Note: Calculations only have binary damage per block, it can exist or not exist, partial perforation is not supported
    //WARNING: Edge case handling for vehicle split in the middle required
    //Note: With very large vehicles there may be an issue where the first found water contact point takes too long to 'flood' around the entire vehicle
    //FIXED: isTouchingWater checks should be stopped if vehicle is not touching water
    //FIXED: isTouchingWater checks may lead to issues as they can't be stopped while vehilce is in water to detect vehicle leaving water, yet they are not allowed to affect the vehicle so long as stepped simulation is taking place
    //Note: Simulation will only handle water that is horizontal, angles will not work
    //FIXED: Simulation can break if a vehicle rotates too quickly so that the expanding calculation can't keep up
    //Note: Vehicle can only have one 'part' for now, two seperate Vessels in one calculation space may lead to issues --do not lead to issues
    //Note: Wave implementation may cost performance and currently doesn't exist
    //Note: The amount of allBlocks could be reduced, however this would need a check that would prevent blocks being deleted from inside
    //Note: All Water contact checks do not account for block rotation
    //Note: Blocks currently flood instantly, leading to some suboptimal behavior. Could be improved by incrementing floodedPercentage instead of instantly flooding a block
    //Note: By detecting fully enclosed volumes and deleting those from the dictionay the amount of checks could be reduced. Damage handling would suffer though


    #region Force Input and Vehicle movement
    public void ApplyForceOnVehicle(Vector3 forceOrigin, Vector3 forceDirection) //All force positions are in Worldspace, not local space
    {
        ApplyForceOnVehicle(new Force(forceOrigin, forceDirection));
    }
    public void ApplyForceOnVehicle(Force force)
    {
        forcesToApply.Add(force);
    }


    void CalculateLinearForces()
    {
        currentLinearForce = Vector3.zero;

        foreach (Force force in forcesToApply)
        {
            currentLinearForce += force.force;
        }
    }
    void CalculateTorqueForces()
    {
        currentTorqueVector = Vector3.zero;

        foreach (Force force in forcesToApply)
        {
            currentTorqueVector += Vector3.Cross(force.force, FloatingTools.GetVectorBetweenTwoPoints(CoM, force.position));
        }
    }

    void CalculateLinearAcceleration()
    {
        currentLinearAcceleration = currentLinearForce.normalized * (currentLinearForce.magnitude / vehicleMass);
    }
    void CalculateAngluarAcceleration()
    {
        currentAngularAccleration = currentTorqueVector.normalized * (currentTorqueVector.magnitude / FloatingTools.CalculateInertia(currentTorqueVector, vehicleMassDistribution, CoM));
    }
    #endregion


    void RecalculateVehicle()
    {
        vehicleMassDistribution = TestVehicle.ToMassDistribution();
        vehicleBlockTypeDistribution = TestVehicle.testVehicle;

        CoM = FloatingTools.CalculateCoM(vehicleMassDistribution);
        vehicleMass = FloatingTools.CalculateVehicleMass(vehicleMassDistribution);

        allBlocks = ConvertRunningCalculationArrayToDictionary(GenerateRunningCalculationFloatingArray(vehicleBlockTypeDistribution, vehicleMassDistribution));
        allVehicleBlocks = SeperateVehicleBlocksFromAir(allBlocks);

        secureOutOfWaterHeight = Mathf.Abs(FloatingTools.GetVectorBetweenTwoPoints(Vector3.zero, allBlocks[Vector3.zero].relativePosition).magnitude) + FloatingTools.Constants.blockSize;

        currentGravity = Physics.gravity;
    }


    FloatingBlock[,,] GenerateRunningCalculationFloatingArray(int[,,] blockTypeDistribution, float[,,] massDistribution) // This expands the input array in each axis' positive and negative direction to create a layer of air around the stored Vehicle
    {
        FloatingBlock[,,] output = new FloatingBlock[blockTypeDistribution.GetLength(0) + 2, blockTypeDistribution.GetLength(1) + 2, blockTypeDistribution.GetLength(2) + 2];
        for (int x = 0; x < output.GetLength(0); x++)
        {
            for (int z = 0; z < output.GetLength(1); z++)
            {
                for (int y = 0; y < output.GetLength(2); y++)
                {
                    int blockType;
                    float blockMass;
                    if ((x == 0 || y == 0 || z == 0) || (x == output.GetLength(0) - 1 || z == output.GetLength(1) - 1 || y == output.GetLength(2) - 1)) // if position is in expanded area, outside of blockDistribution
                    {
                        blockType = 0;
                        blockMass = 0;
                    }
                    else
                    {
                        blockType = blockTypeDistribution[x - 1, z - 1, y - 1];
                        blockMass = massDistribution[x - 1, z - 1, y - 1];
                    }

                    Vector3 relativePosition = new Vector3((float)x - (((float)output.GetLength(0) - 1) * 0.5f), (float)y - (((float)output.GetLength(2) - 1) * 0.5f), (float)z - (((float)output.GetLength(1) - 1) * 0.5f));
                    relativePosition *= FloatingTools.Constants.blockSize;

                    output[x, z, y] = new FloatingBlock(false, relativePosition, blockType, blockMass);
                }
            }
        }

        return output;
    }

    Dictionary<Vector3, FloatingBlock> ConvertRunningCalculationArrayToDictionary(FloatingBlock[,,] blockDistribution) //For new calculation Method a Dictionary is required, this may end up being a change to all old code, but for now this is the solution
    {
        Dictionary<Vector3, FloatingBlock> output = new Dictionary<Vector3, FloatingBlock>();

        for (int x = 0; x < blockDistribution.GetLength(0); x++)
        {
            for (int z = 0; z < blockDistribution.GetLength(1); z++)
            {
                for (int y = 0; y < blockDistribution.GetLength(2); y++)
                {
                    output.Add(new Vector3(x, y, z), blockDistribution[x, z, y]);
                }
            }
        }

        return output;
    }
    List<KeyValuePair<Vector3, FloatingBlock>> SeperateVehicleBlocksFromAir(Dictionary<Vector3, FloatingBlock> vehicleDictionary)
    {
        List<KeyValuePair<Vector3, FloatingBlock>> output = new List<KeyValuePair<Vector3, FloatingBlock>>();

        foreach (KeyValuePair<Vector3, FloatingBlock> keyPair in vehicleDictionary)
        {
            if (keyPair.Value.blockType == 1)
            {
                output.Add(keyPair);
            }
        }

        return output;
    }

    Vector3? CheckForVehicleInWaterContact() //The Vector3 Output is the key for the floating block that was idientified to touch the water
    {

        foreach (KeyValuePair<Vector3, FloatingBlock> keyPair in allVehicleBlocks)
        {
            if (FloatingTools.CheckForWaterContact(vehicleParent.transform.TransformPoint(keyPair.Value.relativePosition))) //transforms parent-relative block position to worldspace, then executes check
            {
                isInWaterContact = true;
                return keyPair.Key; // return is dictionary key
            }
        }
        isInWaterContact = false;
        return null;
    }
}
