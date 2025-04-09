using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImageRotate : MonoBehaviour
{
    private Transform target;
    public float yOffset = 0f;
    public float customXRotation = 0f;

    private void Start()
    {
        target = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void Update()
    {
        if (target != null)
        {
            Vector3 direction = target.position - transform.position;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                Vector3 euler = targetRotation.eulerAngles;
                euler.x = customXRotation;
                euler.y += yOffset;
                targetRotation = Quaternion.Euler(euler);
                transform.rotation = targetRotation;
            }
        }
    }
}
