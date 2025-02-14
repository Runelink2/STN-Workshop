using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Food : MonoBehaviour {
    public Collider2D gridArea;
    public SpriteRenderer _foodSprite;
    public SpriteRenderer foodSprite {
        get {
            return _foodSprite ? _foodSprite : GetComponent<SpriteRenderer>();
        }
    }
    
    void Start() {
        RandomizePosition();
    }
    
    void OnEnable () {
        if (Snake.instance.food.Contains (this) == false) Snake.instance.food.Add (this);
    }
    
    void OnDisable () {
        if (Snake.instance.food.Contains (this)) Snake.instance.food.Remove (this);
    }
    
    IEnumerable<Vector2Int> GetAllValidGridPositions() {
        int halfX = (int)gridArea.transform.localScale.x / 2;
        int halfY = (int)gridArea.transform.localScale.y / 2;

        for (int x = -halfX; x <= halfX; x++) {
            for (int y = -halfY; y <= halfY; y++) {
                yield return new Vector2Int(x, y);
            }
        }
    }

    void RandomizePosition() {
        var validPositions = GetAllValidGridPositions().Where(pos => !Snake.instance.Occupies(pos.x, pos.y)).ToList();

        if (validPositions.Any()) {
            var randomPosition = validPositions[Random.Range(0, validPositions.Count)];
            SetPosition(transform, randomPosition.x, randomPosition.y);
        } else {
            Debug.LogWarning("No valid positions available for food placement");
        }
    }

    void OnTriggerEnter2D(Collider2D other) {
        RandomizePosition();
    }

    void SetPosition(Transform target, int x, int y) {
        target.localPosition = new Vector2(x, y);
    }
}