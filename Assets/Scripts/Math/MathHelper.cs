using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MathHelper
{
    /// <summary>
    /// Converts world position into UI space position.
    /// </summary>
    public static Vector3 WorldToUISpace(Camera camera, Canvas parentCanvas, Vector3 worldPoint)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentCanvas.transform as RectTransform,
                                                                camera.WorldToScreenPoint(worldPoint),
                                                                null,
                                                                out Vector2 localPointUI);
        return localPointUI;
    }

    /// <summary>
    /// Converts a value (newValue) from range to another by reference value.
    /// It's used linear conversion.
    /// </summary>
    public static double FromRangeTo(double refValue, double refMin, double refMax, double newMin, double newMax)
    {
        return (((refValue - refMin) * (newMax - newMin)) / (refMax - refMin)) + newMin;
    }

    /// <summary>
    /// Converts a value (newValue) from range [0, 1] inclusive.
    /// It's used linear conversion.
    /// </summary>
    public static double FromRangeTo01(double refValue, double refMin, double refMax)
    {
        return (((refValue - refMin) * (1.0d - 0.0d)) / (refMax - refMin)) + 0.0d;
    }

    /// <summary>
    /// Converts millimeters value to inches value.
    /// </summary>
    public static float FromMillimetersToInches(this float millimeters)
    {
        return millimeters / 25.4f;
    }

    /// <summary>
    /// Converts inches value to millimeters value.
    /// </summary>
    public static float FromInchesToMillimeters(this float inches)
    {
        return inches * 25.4f;
    }

    /// <summary>
    /// To check if two vectors is approximately the same.
    /// </summary>
    public static bool Approximately(Vector3 vector, Vector3 targetVector)
    {
        if (Mathf.Approximately(vector.x, targetVector.x) &&
            Mathf.Approximately(vector.y, targetVector.y) &&
            Mathf.Approximately(vector.z, targetVector.z))
        {
            return true;
        }

        return false;
    }

    public static Vector3 InvertSignZ(this Vector3 vector3)
    {
        if (Mathf.Approximately(vector3.z, 0.0f))
        {
            return vector3;
        }

        return new Vector3(vector3.x, vector3.y, vector3.z * -1.0f);
    }

    /// <summary>
    /// Convert from Unity rotation to ORTHOGONAL tilting angles
    /// </summary>
    /// <remarks>
    /// The calculated angles are within range of (-360,360)
    /// </remarks>
    /// <param name="unityRotation"></param>
    /// <returns></returns>
    public static (float alpha, float beta) ToTiltingAngles(this Quaternion unityRotation)
    {
        // Convert unity rotation to right-handed rotation
        var rightHandedRotation = new Quaternion(-unityRotation.x, -unityRotation.y, unityRotation.z, unityRotation.w);

        float alpha, beta;
        var theta = 0.0f;

        // Decompose XYZ euler angle (NOTE: NOT ZYX as conventional Yaw-Pitch-Roll) from quaternion
        var delta = rightHandedRotation.w * rightHandedRotation.y + rightHandedRotation.x * rightHandedRotation.z;
        if (Mathf.Approximately(Mathf.Abs(delta), 0.5f))
        {
            alpha = 2 * Mathf.Atan2(rightHandedRotation.x, rightHandedRotation.w) * Mathf.Rad2Deg;

            beta = 90 * Mathf.Sign(delta);
        }
        else
        {
            alpha = Mathf.Atan2(2 * (rightHandedRotation.w * rightHandedRotation.x - rightHandedRotation.z * rightHandedRotation.y),
                               (1 - 2 * (rightHandedRotation.y * rightHandedRotation.y + rightHandedRotation.x * rightHandedRotation.x))) * Mathf.Rad2Deg;

            beta = Mathf.Asin(2 * delta) * Mathf.Rad2Deg;

            theta = Mathf.Atan2(2 * (rightHandedRotation.w * rightHandedRotation.z - rightHandedRotation.x * rightHandedRotation.y),
                               (1 - 2 * (rightHandedRotation.y * rightHandedRotation.y + rightHandedRotation.z * rightHandedRotation.z))) * Mathf.Rad2Deg;
        }

        // If 'theta' is out of range [-90, 90], apply a 'MinusPi' operation to have a smaller value in 'theta'
        if (Mathf.Abs(theta) > 90)
        {
            alpha -= 180;
            beta = 180 - beta;
        }

        // Modulize the tilting angles
        alpha %= 360;
        beta %= 360;

        return (alpha, beta);
    }
}
