using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cainos.PixelArtTopDown_Basic
{
    //when object enter or exit the trigger, put it to the assigned layer and sorting layers base on the direction
    //used in the stairs objects for player to travel between layers

    public class Level2StairsLayerTrigger : MonoBehaviour
    {
        public Direction direction;                                 //direction of the stairs
        [Space]
        public string layerUpper;
        public string sortingLayerUpper;
        [Space]
        public string layerLower;
        public string sortingLayerLower;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (direction == Direction.South && other.transform.position.y < transform.position.y) SetLayerAndSortingLayer(other.gameObject, layerUpper, sortingLayerUpper);
            else
            if (direction == Direction.West && other.transform.position.x < transform.position.x) SetLayerAndSortingLayer(other.gameObject, layerUpper, sortingLayerUpper);
            else
            if (direction == Direction.East && other.transform.position.x > transform.position.x) SetLayerAndSortingLayer(other.gameObject, layerUpper, sortingLayerUpper);

        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (direction == Direction.South && other.transform.position.y < transform.position.y) SetLayerAndSortingLayer(other.gameObject, layerLower, sortingLayerLower);
            else
            if (direction == Direction.West && other.transform.position.x < transform.position.x) SetLayerAndSortingLayer(other.gameObject, layerLower, sortingLayerLower);
            else
            if (direction == Direction.East && other.transform.position.x > transform.position.x) SetLayerAndSortingLayer(other.gameObject, layerLower, sortingLayerLower);
        }

        private void SetLayerAndSortingLayer( GameObject target, string layer, string sortingLayer )
        {
            var depth = target.GetComponent<WorldDepth2D>();
            if (target.GetComponent<Rigidbody2D>() == null ||
                (depth == null && target.GetComponent<SpriteRenderer>() == null)) return;
            target.layer = LayerMask.NameToLayer(layer);
            if (depth != null)
            {
                depth.sortingLayer = sortingLayer;
                depth.Refresh();
            }
            SpriteRenderer[] srs = target.GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer sr in srs)
            {
                sr.sortingLayerName = sortingLayer;
            }
        }

        public enum Direction
        {
            North,
            South,
            West,
            East
        }    
    }
}
