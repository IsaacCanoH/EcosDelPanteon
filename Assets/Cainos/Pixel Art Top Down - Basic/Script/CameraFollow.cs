using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cainos.PixelArtTopDown_Basic
{
    //let camera follow target
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float lerpSpeed = 1.0f;

        private Vector3 offset;

        private Vector3 targetPos;

        private void Start()
        {
            if (target == null) return;

            offset = new Vector3(0f, 0f, transform.position.z - target.position.z);
        }

        private void Update()
        {
            if (target == null) return;

            targetPos = target.position + offset;
            float effectiveLerp = Mathf.Max(lerpSpeed, 5f);
            transform.position = Vector3.Lerp(transform.position, targetPos, effectiveLerp * Time.deltaTime);
        }

    }
}
