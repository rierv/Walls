using UnityEngine;
using System.Collections;
using System.Collections.Generic;
[AddComponentMenu("Camera-Control/Smooth Mouse Look")]
public class CharacterController : MonoBehaviour
{

    public enum RotationAxes { MouseXAndY = 0, MouseX = 1, MouseY = 2 }
    public RotationAxes axes = RotationAxes.MouseXAndY;
    public float sensitivityX = 1F;
    public float sensitivityY = 1F;
    public float minimumX = -360F;
    public float maximumX = 360F;
    public float minimumY = -60F;
    public float maximumY = 60F;
    float rotationX = 0F;
    float rotationY = 0F;
   
    Quaternion originalRotation;
    Rigidbody rb;
    public Transform pointer;
    public Camera camera;
    public float speed = 12f;
    public bool moving = true;
    void FixedUpdate()
    {
        if (moving)
        {
            if (Input.GetKey(KeyCode.A))
                rb.AddForce(speed * (-pointer.right*2 + Vector3.up));
            if (Input.GetKey(KeyCode.D))
                rb.AddForce(speed *( pointer.right*2 + Vector3.up));
            if (Input.GetKey(KeyCode.W))
                rb.AddForce(speed * (pointer.forward*2 + Vector3.up));
            if (Input.GetKey(KeyCode.S))
                rb.AddForce(speed * (-pointer.forward*2 + Vector3.up));
        }
        if (axes == RotationAxes.MouseXAndY)
        {
            

            //Gets rotational input from the mouse
            rotationY += Input.GetAxis("Mouse Y") * sensitivityY;
            rotationX += Input.GetAxis("Mouse X") * sensitivityX;


            //Clamp the rotation average to be within a specific value range
            rotationY = ClampAngle(rotationY, minimumY, maximumY);
            rotationX = ClampAngle(rotationX, minimumX, maximumX);

            //Get the rotation you will be at next as a Quaternion
            Quaternion yQuaternion = Quaternion.AngleAxis(rotationY, Vector3.left);
            Quaternion xQuaternion = Quaternion.AngleAxis(rotationX, Vector3.up);

            //Rotate
            camera.transform.localRotation = Quaternion.Lerp(camera.transform.localRotation, originalRotation * xQuaternion * yQuaternion, .5f);
            pointer.localRotation = originalRotation * xQuaternion;
        }
    }
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb)
            rb.freezeRotation = true;
        originalRotation = camera.transform.localRotation;
    }
    public static float ClampAngle(float angle, float min, float max)
    {
        angle = angle % 360;
        if ((angle >= -360F) && (angle <= 360F))
        {
            if (angle < -360F)
            {
                angle += 360F;
            }
            if (angle > 360F)
            {
                angle -= 360F;
            }
        }
        return Mathf.Clamp(angle, min, max);
    }
}