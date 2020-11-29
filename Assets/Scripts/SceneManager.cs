using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FloatingSceneManager
{
    public static void LoadPhysicsSceneWithVehicle(int[,,] vehicle)
    {
        TestVehicle.OverrideDefaultVehicle(vehicle);
        SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
    }

    public static void LoadBuilderScene()
    {
        SceneManager.LoadScene("BuilderScene", LoadSceneMode.Single);
    }
}
