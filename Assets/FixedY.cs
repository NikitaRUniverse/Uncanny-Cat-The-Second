using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FixedY : MonoBehaviour
{
    public float Ypos;
    void Update()
    {
        transform.position = new Vector3(transform.position.x, Ypos, transform.position.z);
    }
}
